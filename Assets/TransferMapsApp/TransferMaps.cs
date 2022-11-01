// teadrinker / Martin Eklund 2022
// quick app to transfer maps between UV-set, mainly for blender workflow
//
// https://github.com/teadrinker/transfer-maps
// License: GPL v3

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using B83.Win32;

namespace teadrinker
{
    public class TransferMaps : MonoBehaviour
    {
        public string InputFileForEditorDebug = "";
        public bool showResult = true;
        //public bool saveResult = false;
        public string outDir = "";
        public Camera debugCam;
        public Material debugOut;
        public Shader ProjectToTextureMap;
        public Shader ExpandPixels;
        public Shader ObjImportedShaderOverride;

        [Space]
        public Camera RenderNormalsCam;
        public Material RenderNormalsMat;

        private List<string> log = new List<string>();
        private RenderTexture _rt;
        private Texture _lastTex;
        private GameObject _lastProcessRoot;

        string GetPathNextToExe()
        {		
            string path = Application.dataPath;
            string[] pathArray = path.Split('/');
            path = "";
            for (int i = 0; i < pathArray.Length - 1; i++)
            {
                path += pathArray[i] + "/";
            }

            return path;
        }

        void OnEnable ()
        {
            // must be installed on the main thread to get the right thread id.
            UnityDragAndDropHook.InstallHook();
            UnityDragAndDropHook.OnDroppedFiles += OnFiles;

            Log("");

            var inputFile = InputFileForEditorDebug;

            if(!Application.isEditor) {
                inputFile = "";
                var args = System.Environment.GetCommandLineArgs();
                if(args.Length > 1) {
                    inputFile = args[1];
                }
            }

            if(inputFile != "") 
            {
                if(!System.IO.File.Exists(inputFile)) {
                    LogError("No such file: " + inputFile);
                    return;
                }   
                Process(inputFile);
            }
            else {
                Log("Drop OBJ File to transfer maps");
                Log("");
            }
        }

        void Process(string fullpath)
        {
            try {

                if(_lastProcessRoot != null)
                    Destroy(_lastProcessRoot);


                var outTextureSize = 4096;
                var outMarginIterations = 16; // note, 1 iteration does 4 pixels
                var cfgPath = System.IO.Path.Combine(GetPathNextToExe(), "config.json");
                if(System.IO.File.Exists(cfgPath)) {
                    var cfgText = System.IO.File.ReadAllText(cfgPath);
                    var cfg = MiniJSON.Json.Deserialize(cfgText) as Dictionary<string, object>;
                    if(cfg.TryGetValue("OutputTextureSize", out object o))
                        outTextureSize = (int) (System.Convert.ToDouble(o) + 0.5);
                    if(cfg.TryGetValue("MarginPixels", out object o2))
                        outMarginIterations = (int) (System.Convert.ToDouble(o2) / 4 + 0.5);
                }
                else {
                    // write defaults
                    System.IO.File.WriteAllText(cfgPath, "{\n\"OutputTextureSize\" : 4096, \n\"MarginPixels\" : 64\n}\n");
                }



                var loader = new Dummiesman.OBJLoader(ObjImportedShaderOverride);
                var root = loader.Load(fullpath);
                _lastProcessRoot = root;

                var fov = 0f;
                var camShiftX = 0f;
                var camShiftY = 0f;
                var camPos = Vector3.zero;
                var camTarget = Vector3.zero;
                var camRight = Vector3.zero;
                var udims = new Dictionary<int, List<System.Tuple<Mesh, Texture, bool>>>();
                var refmaps = new List<System.Tuple<string, Mesh, Texture>>();
                var namesToGO = new Dictionary<string, GameObject>();
                var OLDUVs = new Dictionary<string, GameObject>();

                foreach(Transform obj in root.transform) {
                    if(obj.name.Contains("_OLDUV")) {
                        var srcname = obj.name.Split(new string[]{"_OLDUV"}, System.StringSplitOptions.None)[0];

                        GameObject found = null;
                        int foundCount = 0;
                        foreach(Transform obj2 in root.transform) {
                            if(obj2.name.StartsWith(srcname) && !obj2.name.Contains("_OLDUV")) {
                                found = obj2.gameObject;
                                foundCount++;
                            }
                        }

                        if(foundCount == 0)
                        {
                            LogError("ERROR! : Cannot find matching object for OLDUV: " + srcname +" (" + obj.name + ")");
                        }
                        else if(foundCount > 1) 
                        {
                            LogError("ERROR! : Ambigous name for OLDUV (multiple available): " + obj.name);
                            LogError("       (prefix before _OLDUV must be unique)");
                        }
                        else
                        {
                            if(!OLDUVs.ContainsKey(found.name))
							{
                                OLDUVs.Add(found.name, obj.gameObject);
							}
                            else
							{
                                LogError("ERROR! : multiple _OLDUV for object : " + obj.name + " (" + srcname + ") MAKE SURE OBJECTS DONT HAVE MULTIPLE MATERIALS!");

							}
                        }

                    }
                }

                foreach(Transform obj in root.transform) {
                    var name = obj.name;
                    var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                    if(name.StartsWith("cam_", System.StringComparison.InvariantCultureIgnoreCase)) 
                    {
                        var verts = mesh.vertices;
                        var sum = Vector3.zero;
                        for(int i = 0; i < verts.Length; i++)
                            sum += verts[i];
                        var center = sum / verts.Length;
                        if(name.StartsWith("cam_pos_", System.StringComparison.InvariantCultureIgnoreCase)) 
                        {
                            camPos = center;
                            var parts = name.Split('_');

                            if(parts.Length < 3)
                                LogError("ERROR! : invalid cam naming convention! MISSING FOV:" + name +", should be cam_pos_90 (for 90 degree fov)");
                            else
                                fov = System.Single.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture);

                            if (parts.Length >= 4 && System.Single.TryParse(parts[3], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out float tmp))
                            {
                                camShiftX = tmp;
                                Log("Using Lens Shift X: " + camShiftX);
                            }

                            if (parts.Length >= 5 && System.Single.TryParse(parts[4], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out float tmp2))
                            {
                                camShiftY = tmp2;
                                Log("Using Lens Shift Y: " + camShiftY);
                            }

                        }
                        else if(name.StartsWith("cam_right_", System.StringComparison.InvariantCultureIgnoreCase))
                            camRight = center;
                        else if(name.StartsWith("cam_target_", System.StringComparison.InvariantCultureIgnoreCase))
                            camTarget = center;
                        else
                            LogError("ERROR! : invalid camera naming convention! " + name);
                    }
                    else if(name.Contains("_REFLECTIONMAP"))
                    {
                        var outname = obj.name.Split(new string[] { "_REFLECTIONMAP" }, System.StringSplitOptions.None)[0];
                        refmaps.Add(new System.Tuple<string, Mesh, Texture>(outname, mesh, obj.GetComponent<MeshRenderer>().sharedMaterial.mainTexture));
                    }
                    else {

                        if(name.Contains("_OLDUV")) 
                            continue;

                        var uv = mesh.uv;
                        var bbmin = Vector2.one * 999f;
                        var bbmax = Vector2.one *-999f;
                        for(int i = 0; i < uv.Length; i++) {
                            bbmin = Vector2.Min(bbmin, uv[i]);
                            bbmax = Vector2.Max(bbmax, uv[i]);
                        }
                        var udim =  new Vector2Int((int)((bbmax.x + bbmin.x) * 0.5f), (int)((bbmax.y + bbmin.y) * 0.5f));
                        if(bbmin.x < udim.x || bbmin.y < udim.y || bbmax.x > udim.x + 1 || bbmax.y > udim.y + 1)
                        {
                            LogError("ERROR! : UV not within udim! : " + name + ", udim center: "  + udim + ", bound:" + bbmin + " - " + bbmax);
                        }
                        else
                        {
                            List<System.Tuple<Mesh, Texture, bool>> list;
                            var packed = udim.x + 1000 + ((udim.y + 1000)<<16);
                            if(!udims.TryGetValue(packed, out list)) {
                                list = new List<System.Tuple<Mesh, Texture, bool>>();
                                udims.Add(packed, list);
                            }

                            if(OLDUVs.ContainsKey(obj.name)) {
                                var src = OLDUVs[obj.name];
                                var srcMesh = src.GetComponent<MeshFilter>().sharedMesh;
                                var srcTexture = src.GetComponent<MeshRenderer>().sharedMaterial.mainTexture;
                                if(srcMesh.vertexCount != mesh.vertexCount) {
                                    var srcVerts = srcMesh.vertices;
                                    var srcNorm = srcMesh.normals;
                                    var srcUv = srcMesh.uv;
                                    var dstVerts = mesh.vertices;
                                    var dstNorm = mesh.normals;
                                    var uvConverted = new Vector2[dstVerts.Length];
                                    //var srcVertNormUVid = new List<System.Tuple<Vector3, Vector3, int>>();
                                    //for(int i = 0; i < srcVerts.Length; i++)
									//{
                                    //    srcVertNormUVid.Add(new System.Tuple<Vector3, Vector3, int>(srcVerts[i], srcNorm[i], i));
									//}
                                    var largestDiff = 0f;
                                    for(int i = 0; i < dstVerts.Length; i++)
									{
                                        var closestId = -1;
                                        var closestDiff = 999999999999999999999999999999f;
                                        for (int j = 0; j < srcVerts.Length; j++)
										{
                                            var diff = (dstVerts[i] - srcVerts[j]).sqrMagnitude + (dstNorm[i] - srcNorm[j]).sqrMagnitude * 0.005f;
                                            if(diff < closestDiff)
											{
                                                closestDiff = diff;
                                                closestId = j;
											}
                                        }
                                        uvConverted[i] = srcUv[closestId];
                                        if(closestDiff > largestDiff)
										{
                                            largestDiff = closestDiff;
                                        }
                                    }
                                    if(largestDiff > 0.0001f)
                                        LogError("WARNING! : Some vertices where not matched in object:\n    " + obj.name + ", largest diff: " + Mathf.Sqrt(largestDiff));
                                    else 
                                        Log("Successfully matched UVs based on vertex positions and normals:\n    " + obj.name + ", largest diff: " + Mathf.Sqrt(largestDiff));

                                    mesh.uv2 = uvConverted;
                                    mesh.name = obj.name;
                                    list.Add(new System.Tuple<Mesh, Texture, bool>(mesh, srcTexture, true));                    
                                }
                                else
                                {
                                    mesh.uv2 = srcMesh.uv;
                                    mesh.name = obj.name;
                                    list.Add(new System.Tuple<Mesh, Texture, bool>(mesh, srcTexture, true));                    
                                }
                            }
                            else {
                                mesh.name = obj.name;
                                list.Add(new System.Tuple<Mesh, Texture, bool>(mesh, obj.GetComponent<MeshRenderer>().sharedMaterial.mainTexture, false));                    
                            }
                        }
                    }
                }

                bool validCameraTransform = false;
                Material matExpandPixels = new Material(ExpandPixels);
                Material mat = new Material(ProjectToTextureMap);
                if(camPos.sqrMagnitude > 0f && camTarget.sqrMagnitude > 0f) {
                    var forward = (camTarget - camPos).normalized;
                    var rot = camRight.sqrMagnitude == 0 ? Quaternion.LookRotation(forward, Vector3.up ) :
                                                           Quaternion.LookRotation(forward, Vector3.Cross(forward, (camRight - camPos).normalized) );
                    var ma = Matrix4x4.TRS(camPos, rot, Vector3.one);

                    if (debugCam != null)
                        RenderUtils.ApplyCameraParams(debugCam, camPos, rot, fov, camShiftX, camShiftY);

                    RenderUtils.ApplyCameraParams(RenderNormalsCam, camPos, rot, fov, camShiftX, camShiftY);

                    mat.SetMatrix("_ToCameraSpace", ma.inverse);
                    var fovParams = new Vector4(
                        1.0f / Mathf.Tan(0.5f*fov * Mathf.Deg2Rad), 
                        1.0f / Mathf.Tan(0.5f*fov * Mathf.Deg2Rad),
                        -camShiftX, 
                        -camShiftY);  
                    mat.SetVector("_FovParams", fovParams);
                    
                    validCameraTransform = true;
                }

                if(refmaps.Count > 0)
				{
                    LogError("ERROR! : _REFLECTIONMAP : implementation not yet complete!");

                    /*
                    var _rtfloat = new RenderTexture(outTextureSize, outTextureSize, 32, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
                    RenderNormalsCam.targetTexture = _rtfloat;
                    RenderNormalsCam.allowHDR = true;
                    RenderNormalsCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                    RenderNormalsCam.clearFlags = CameraClearFlags.Color;
                    foreach (var refmap in refmaps)
                    {
                        var outName = refmap.Item1 + "_reflectionmap" + ".png";
                        RenderUtils.RenderSingleMesh(RenderUtils.SceneMeshInstance.Create(refmap.Item2), RenderNormalsCam, RenderNormalsMat);
                        var pixels = toTexture2D(_rtfloat).GetPixels();
                        var tris = new List<int>();
                        var particles = new List<Vector3>();
                        var particlesUV = new List<Color>();
                        var ooSize = 1f / outTextureSize;
                        for (int y = 0; y < outTextureSize; y++)
                        {
                            for (int x = 0; x < outTextureSize; x++)
                            {
                                var col = pixels[x + y * outTextureSize];
                                if(col.a != 0f)
								{
                                    float fx = ((float)x) * ooSize;
                                    float fy = ((float)y) * ooSize;
                                    int cnt = particlesUV.Count;
                                    particlesUV.Add(new Color(fx, fy, -1f, -1f));
                                    particlesUV.Add(new Color(fx, fy,  1f, -1f));
                                    particlesUV.Add(new Color(fx, fy,  1f,  1f));
                                    particlesUV.Add(new Color(fx, fy, -1f, 1f));
                                    particles.Add(new Vector3(col.r, col.g, col.b));
                                    particles.Add(new Vector3(col.r, col.g, col.b));
                                    particles.Add(new Vector3(col.r, col.g, col.b));
                                    particles.Add(new Vector3(col.r, col.g, col.b));

                                    tris.Add(cnt);
                                    tris.Add(cnt + 1);
                                    tris.Add(cnt + 2);

                                    tris.Add(cnt + 2);
                                    tris.Add(cnt + 3);
                                    tris.Add(cnt);
                                }
                            }
                        }
                        var mesh = new Mesh();
                        mesh.vertices = particles.ToArray();
                        mesh.colors = particlesUV.ToArray();
                        mesh.indexFormat = particles.Count < 65536 ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
                        mesh.triangles = tris.ToArray();
                        // todo render particles
                        RenderUtils.RenderSingleMesh(RenderUtils.SceneMeshInstance.Create(refmap.Item2), RenderNormalsCam, RenderNormalsMat);


                        SaveTexture(System.IO.Path.Combine(outDir != "" ? outDir : GetPathNextToExe(), outName), _rt);
                    }
                    */
                }

                if (udims.Count > 0)
				{
                    _rt = new RenderTexture(outTextureSize, outTextureSize, 32, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
                    _rt.filterMode = FilterMode.Point;
                    RenderTexture rt2 = null;
                    if(outMarginIterations > 0) {
                        rt2 = new RenderTexture(outTextureSize, outTextureSize, 0, RenderTextureFormat.Default, RenderTextureReadWrite.sRGB);
                        rt2.filterMode = FilterMode.Point;
                    }
                    foreach(var map in udims) {
                        int udimx = (map.Key & 65535) - 1000;
                        int udimy = (map.Key >> 16  ) - 1000;
                        mat.SetVector("_UDIMOffset", new Vector4(udimx, udimy, 0f, 0f));
                        var list = map.Value;


                        RenderTexture prevrt = UnityEngine.RenderTexture.active;
                        UnityEngine.RenderTexture.active = _rt;
                        GL.Clear(true, true, new Color(1f, 0f, 1f, 0f));           
                        for(int i = 0; i < list.Count; i++) {
                            var usingOLDUV = list[i].Item3;
                            if(!usingOLDUV && !validCameraTransform) {
                                LogError("ERROR! : No camera transform, and no OLDUV, object: " + list[i].Item1.name);
                                continue;
                            }
                            Log("TRANSFER OBJECT: " + list[i].Item1.name + (usingOLDUV ? " (using OLDUV)":"") + (udimx!=0||udimy!=0? " udim: " + udimx + "," + udimy:""));
                            _lastTex = list[i].Item2;
                            mat.mainTexture = list[i].Item2;
                            mat.SetFloat("_UseOLDUV", usingOLDUV ? 1f : 0f);
                            mat.SetPass(0);
                            Graphics.DrawMeshNow(list[i].Item1, Matrix4x4.identity);
                        }
                        GL.Clear(true, false, new Color(1f, 0f, 1f, 0f));
                        UnityEngine.RenderTexture.active = prevrt;

                        for(int i = 0; i < outMarginIterations; i++)
                        {
                            Graphics.Blit(_rt, rt2, matExpandPixels);
                            Graphics.Blit(rt2, _rt, matExpandPixels);
                        }

                        debugOut.mainTexture = _rt;

                        var outName = "out_" +udimx+ "_" +udimy+ ".png";
                        Log("");
                        Log("SAVING PNG: " + outName);
                        Log("");
                        SaveTexture(System.IO.Path.Combine(outDir != "" ? outDir : GetPathNextToExe(), outName), _rt);

                    }
                    if(rt2 != null)
                        Destroy(rt2);
				}

            }
            catch(System.Exception e) {
                LogError(e.Message + "\n\n " + e.StackTrace);
            }
        }



        public void SaveTexture(string path, RenderTexture rt) {
            byte[] bytes = toTexture2D(rt).EncodeToPNG();
            System.IO.File.WriteAllBytes(path, bytes);
        }
        Texture2D toTexture2D(RenderTexture rTex)
        {
            Texture2D tex = new Texture2D(rTex.width, rTex.height, TextureFormat.ARGB32, false, false);
            RenderTexture.active = rTex;
            tex.ReadPixels(new Rect(0, 0, rTex.width, rTex.height), 0, 0);
            tex.Apply();
            Destroy(tex);
            return tex;
        }


        void Update() {
            debugOut.mainTexture = showResult ? _rt : _lastTex;
            /*
            if(saveResult) {
                saveResult = false;
                SaveTexture(System.IO.Path.Combine(outDir, "debugOut.png"), _rt);
            }
            */
        }
        void OnDisable()
        {
            UnityDragAndDropHook.UninstallHook();
            if(_rt != null) {
                debugOut.mainTexture = null;
                Destroy(_rt);
            }
        }
        void LogError(string str) {
            Debug.LogError(str);
            log.Add(str);
        }
        void Log(string str) {
            Debug.Log(str);
            log.Add(str);
        }

        void OnFiles(List<string> aFiles, POINT aPos)
        {
            // do something with the dropped file names. aPos will contain the 
            // mouse position within the window where the files has been dropped.
            string str = "Dropped " + aFiles.Count + " files at: " + aPos + "\n\t" +
                aFiles.Aggregate((a, b) => a + "\n\t" + b);
            Log(str);
            Log("");

            foreach(var file in aFiles) {
                Process(file);
            } 
        }

        private void OnGUI()
        {
            if (GUILayout.Button("clear log"))
                log.Clear();
            foreach (var s in log)
                GUILayout.Label(s);
        }
    }
}
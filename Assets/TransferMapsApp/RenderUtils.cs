using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace teadrinker
{
    public static class RenderUtils
    {
        static public void ApplyCameraParams(Camera cam, Vector3 pos, Quaternion rot, float fov, float xshift=0f, float yshift=0f)
		{
            cam.transform.position = pos;
            cam.transform.rotation = rot;
            cam.transform.localScale = Vector3.one;
            cam.fieldOfView = fov;
            SetCameraLensShift(cam, xshift, yshift);
        }
        static public void SetCameraLensShift(Camera cam, float xshift, float yshift)
        {
            cam.ResetProjectionMatrix();
            if(!(xshift == 0f && yshift == 0f))
			{
                Matrix4x4 mat = cam.projectionMatrix;
                mat[0, 2] = xshift;
                mat[1, 2] = yshift;
                cam.projectionMatrix = mat;
			}
        }

        public class SceneMeshInstance
        {
            //public int layer = -1;
            public Matrix4x4 matrix = Matrix4x4.identity;
            public Mesh mesh;
            public static SceneMeshInstance Create(Mesh _mesh) { return new SceneMeshInstance { mesh = _mesh }; }
        }

        public static void GL_LoadProjectionMatrix(Matrix4x4 mat)
        {
            //  https://forum.unity.com/threads/using-graphics-drawmeshnow-with-a-gl-loadortho-is-this-a-valid-method.330707/

            var proj = mat;

            // this messes up the matrix
            //var c3 = proj.GetColumn(3);
            //proj.SetColumn(3, new Vector4(-1, -1, c3.z, c3.w));

            if (Camera.current != null) proj = proj * Camera.current.worldToCameraMatrix.inverse; // this is needed to avoid glitch issues

            GL.LoadProjectionMatrix(proj);
        }

        public static void RenderSingleMesh(SceneMeshInstance mesh, Camera cam, Material mat, int passId = 0, RenderTexture rt = null)
		{
            RenderMeshes(new List<SceneMeshInstance> { mesh }, cam, mat, passId, rt);
        }
        public static void RenderMeshes(List<SceneMeshInstance> scene, Camera cam, Material mat, int passId = 0, RenderTexture rt = null)
        {
            if (scene  == null) { Debug.LogError("RenderMeshes requires mesh list"); return; }
            if (cam    == null) { Debug.LogError("RenderMeshes requires camera"); return; }
            if (mat    == null) { Debug.LogError("RenderMeshes requires mat"); return; }

            if (rt == null)
            {
                rt = cam.targetTexture;
                if (rt == null) { Debug.LogError("RenderMeshes requires RenderTexture"); return; }
            }

            RenderTexture prev = RenderTexture.active;

            RenderTexture.active = rt;
            var projectionMatrix = cam.projectionMatrix;
            var viewMatrix = cam.worldToCameraMatrix;
            GL.Clear((cam.clearFlags & CameraClearFlags.Depth) != 0, (cam.clearFlags & CameraClearFlags.Color) != 0, cam.backgroundColor);
            if (cam.cullingMask != 0)
            {
                GL.PushMatrix();
                mat.SetPass(passId);
                GL.Viewport(cam.pixelRect);

                GL_LoadProjectionMatrix(projectionMatrix);
                //GL.modelview = viewMatrix; // Relying on modelview don't work in Unity editor, causes glitches (restoring GL.modelview to its original value when we are done also does not help!) 

                for (int i = 0; i < scene.Count; i++)
                {
                    //if (((1 << Meshes[i].layer) & cam.cullingMask) != 0)
                    {
                        Graphics.DrawMeshNow(scene[i].mesh, viewMatrix * scene[i].matrix); // does this draw all submeshes? if not need loop here
                    }
                }

                GL.PopMatrix(); // restore
            }
            RenderTexture.active = prev; // restore
        }

    }

}
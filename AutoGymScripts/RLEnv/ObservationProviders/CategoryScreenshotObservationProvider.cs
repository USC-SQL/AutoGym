using System;
using System.IO;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using UnityEngine;

namespace UnityRLEnv
{
    public abstract class CategoryScreenshotObservationProvider : IObservationProvider
    {
        private string screenshotDir;
        private RenderTexture targetTexture;
        private Texture2D screenshotTexture;
        private string categoryShaderName;

        public CategoryScreenshotObservationProvider(string categoryShaderName)
        {
            this.categoryShaderName = categoryShaderName;
        }

        public abstract int? GetCategoryId(GameObject gameObject);

        private void AssignCategoryTags()
        {
            UnityEngine.Object[] renderers = UnityEngine.Object.FindObjectsOfType(typeof(Renderer));
            foreach (UnityEngine.Object rendererObj in renderers)
            {
                Renderer r = (Renderer)rendererObj;
                GameObject go = r.gameObject;
                int? categoryId = GetCategoryId(go);
                if (categoryId.HasValue)
                {
                    r.material.SetOverrideTag("ObjectCategory", categoryId.Value.ToString());
                }
            }
        }

        private void SetReplacementShader()
        {
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera != Camera.main)
                {
                    camera.enabled = false;
                }
            }
            Camera.main.SetReplacementShader(Shader.Find(categoryShaderName), "ObjectCategory");
            Camera.main.backgroundColor = Color.black;
            Camera.main.clearFlags = CameraClearFlags.Color;
            UnityEngine.Object[] canvases = UnityEngine.Object.FindObjectsOfType(typeof(Canvas));
            foreach (UnityEngine.Object canvasObj in canvases)
            {
                Canvas canvas = (Canvas)canvasObj;
                canvas.enabled = false;
            }
        }

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            screenshotDir = Path.Combine(workDir, "Screenshots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }

            targetTexture = new RenderTexture(Screen.width, Screen.height, 16, RenderTextureFormat.ARGB32);
            screenshotTexture = new Texture2D(Screen.width, Screen.height, TextureFormat.ARGB32, false);
         
            AssignCategoryTags();
            SetReplacementShader();
            yield break;
        }

        public string CollectImageObservation()
        {
            string screenshotPath = Path.Combine(screenshotDir, Time.time + ".png");

            AssignCategoryTags();

            Camera camera = Camera.main;
            RenderTexture cameraRT = camera.targetTexture;
            SetReplacementShader();
            camera.targetTexture = targetTexture;
            camera.Render();

            screenshotTexture.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            screenshotTexture.Apply();

            camera.targetTexture = cameraRT;

            using (FileStream fs = File.Open(screenshotPath, FileMode.Create))
            {
                byte[] data = screenshotTexture.EncodeToPNG();
                fs.Write(data, 0, data.Length);
            }

            return screenshotPath;
        }

        public IList<float> CollectObservations()
        {
            return new List<float>();
        }
    }
}

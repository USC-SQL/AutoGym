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
    public class ScreenshotObservationProvider : IObservationProvider
    {
        private string screenshotDir;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            screenshotDir = Path.Combine(workDir, "Screenshots");
            if (!Directory.Exists(screenshotDir))
            {
                Directory.CreateDirectory(screenshotDir);
            }
            yield break;
        }

        public string CollectImageObservation()
        {
            string screenshotPath = Path.Combine(screenshotDir, Time.time + ".png");
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            using (FileStream fs = File.Open(screenshotPath, FileMode.Create))
            {
                byte[] data = screenshot.EncodeToPNG();
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

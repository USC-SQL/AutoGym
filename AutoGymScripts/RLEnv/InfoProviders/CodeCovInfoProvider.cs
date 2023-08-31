using System;
using System.Collections;
using System.Reflection;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class CodeCovInfoProvider : IInfoProvider
    {
        MethodInfo flushPauseMethod;
        MethodInfo clearMethod;
        MethodInfo initialiseTraceMethod;
        PropertyInfo traceProperty;
        PropertyInfo recordingProperty;
        int codeCovFreq;
        int counter;
        int currentPid;
        string acvOutputDir;

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            codeCovFreq = config["code_cov_freq"].ToObject<int>();
            Type altcoverInstanceType = Type.GetType("AltCover.Recorder.Instance, AltCover.Recorder.g");
            if (altcoverInstanceType == null)
            {
                Debug.LogWarning("Code coverage instrumentation not present, code coverage will not be recorded");
                yield break;
            }
            Type altcoverIType = altcoverInstanceType.GetNestedType("I", BindingFlags.NonPublic | BindingFlags.Static);
            if (altcoverIType == null)
            {
                throw new Exception("Failed to find I type");
            }
            flushPauseMethod = altcoverIType.GetMethod("flushPause", BindingFlags.NonPublic | BindingFlags.Static);
            if (flushPauseMethod == null)
            {
                throw new Exception("Failed to find flushPause");
            }
            clearMethod = altcoverIType.GetMethod("clear", BindingFlags.NonPublic | BindingFlags.Static);
            if (clearMethod == null)
            {
                throw new Exception("Failed to find clear");
            }
            initialiseTraceMethod = altcoverIType.GetMethod("initialiseTrace", BindingFlags.NonPublic | BindingFlags.Static);
            if (initialiseTraceMethod == null)
            {
                throw new Exception("Failed to find initialiseTrace");
            }

            traceProperty = altcoverIType.GetProperty("trace", BindingFlags.NonPublic | BindingFlags.Static);
            if (traceProperty == null)
            {
                throw new Exception("Failed to find trace property");
            }
            recordingProperty = altcoverIType.GetProperty("recording", BindingFlags.NonPublic | BindingFlags.Static);
            if (recordingProperty == null)
            {
                throw new Exception("Failed to find recording property");
            }

            currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            acvOutputDir = Path.Combine(workDir, "CodeCoverage");
            if (!Directory.Exists(acvOutputDir))
            {
                Directory.CreateDirectory(acvOutputDir);
            }
        }

        public void AddInfo(JObject info)
        {
            if (flushPauseMethod == null)
            {
                return;
            }
            ++counter;
            if (counter >= codeCovFreq)
            {
                /* I.flushPause() */
                flushPauseMethod.Invoke(null, new object[0]);

                string acvPath = Path.Combine(acvOutputDir, "coverage.json." + Guid.NewGuid().ToString() + ".acv");
                File.Move("coverage.json." + currentPid + ".acv", acvPath);
                info["codecov_acv"] = acvPath;

                /* I.initialiseTrace(I.trace)
                   I.clear()
                   I.recording = true */
                initialiseTraceMethod.Invoke(null, new object[] { traceProperty.GetValue(null) });
                clearMethod.Invoke(null, new object[0]);
                recordingProperty.SetValue(null, true);

                counter = 0;
            }
        }
    }
}

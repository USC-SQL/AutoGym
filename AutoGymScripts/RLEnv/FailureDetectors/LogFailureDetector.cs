using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UnityRLEnv
{
    public class LogFailureDetector : IFailureDetector
    {
        private Queue<string> failureQueue;

        class DetectorLogHandler : ILogHandler
        {
            private LogFailureDetector parent;
            private ILogHandler defaultLogHandler;

            public DetectorLogHandler(LogFailureDetector parent, ILogHandler defaultLogHandler)
            {
                this.parent = parent;
                this.defaultLogHandler = defaultLogHandler;
            }

            public void LogException(Exception exception, UnityEngine.Object context)
            {
                string failure = exception.ToString();
                parent.failureQueue.Enqueue(failure);
                if (defaultLogHandler != null)
                {
                    defaultLogHandler.LogException(exception, context);
                }
            }

            public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
            {
                switch (logType)
                {
                    case LogType.Warning:
                    case LogType.Error:
                    case LogType.Assert:
                    case LogType.Exception:
                        string failure = string.Format(format, args) + "\n" + Environment.StackTrace;
                        parent.failureQueue.Enqueue(failure);
                        break;
                }
                if (defaultLogHandler != null)
                {
                    defaultLogHandler.LogFormat(logType, context, format, args);
                }
            }
        }

        public IEnumerator Initialize(string envId, string workDir, JObject config, MonoBehaviour context)
        {
            failureQueue = new Queue<string>();
            ILogHandler defaultLogHandler;
#if UNITY_EDITOR
            defaultLogHandler = Debug.unityLogger.logHandler;
#else
            defaultLogHandler = null;
#endif
            Debug.unityLogger.logHandler = new DetectorLogHandler(this, defaultLogHandler);
            yield break;
        }

        public void DetectFailures(ISet<string> failuresOut)
        {
            while (failureQueue.Count > 0)
            {
                failuresOut.Add(failureQueue.Dequeue());
            }
        }
    }
}
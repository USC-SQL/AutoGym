using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;

namespace UnityRLEnv
{
    public abstract class RLEnv : MonoBehaviour
    {
#if UNITY_EDITOR
        protected bool DebugMode = true;
#else
        protected bool DebugMode = false;
#endif
        public List<string> DebugConfiguration = new List<string>();

        protected IObservationProvider observationProvider;
        protected IActionProvider actionProvider;
        protected IRewardProvider rewardProvider;
        protected ISet<IFailureDetector> failureDetectors;
        protected ISet<IInfoProvider> infoProviders;

        protected InputSimulator inputSim;
        protected InputManagerSettings inputManagerSettings;
        protected JObject envConfig;

        private string envId;
        private int actionDuration;
        private string workDir;
        private bool trainingMode;
        private int listenPort;
        private Socket listenerSocket;
        private Socket clientSocket;
        private bool isReady;

        public RLEnv()
        {
            observationProvider = null;
            actionProvider = null;
            rewardProvider = null;
            failureDetectors = new HashSet<IFailureDetector>();
            infoProviders = new HashSet<IInfoProvider>();
        }

        protected virtual void Start()
        {
            if (FindObjectsOfType(typeof(RLEnv)).Length > 1)
            {
                Destroy(this);
                return;
            }

            DontDestroyOnLoad(this);

            if (!DebugMode)
            {
                string rlEnvId = Environment.GetEnvironmentVariable("RLENV_ID");
                string rlEnvAddr = Environment.GetEnvironmentVariable("RLENV_ADDR");
                string rlEnvPort = Environment.GetEnvironmentVariable("RLENV_PORT");
                string rlEnvConfig = Environment.GetEnvironmentVariable("RLENV_CONFIG");
                string rlEnvWorkDir = Environment.GetEnvironmentVariable("RLENV_WORKDIR");
                string rlEnvTrainingMode = Environment.GetEnvironmentVariable("RLENV_TRAINING_MODE");
                if (rlEnvId == null)
                {
                    throw new Exception("Missing RLENV_ID");
                }
                if (rlEnvAddr == null)
                {
                    throw new Exception("Missing RLENV_ADDR");
                }
                if (rlEnvPort == null)
                {
                    throw new Exception("Missing RLENV_PORT");
                }
                if (rlEnvConfig == null)
                {
                    throw new Exception("Missing RLENV_CONFIG");
                }
                if (rlEnvWorkDir == null)
                {
                    throw new Exception("Missing RLENV_WORKDIR");
                }
                if (rlEnvTrainingMode == null)
                {
                    throw new Exception("Missing RLENV_TRAINING_MODE");
                }

                if (!int.TryParse(rlEnvPort, out listenPort))
                {
                    throw new Exception("Invalid RLENV_PORT");
                }

                envId = rlEnvId;
                workDir = rlEnvWorkDir;
                trainingMode = bool.Parse(rlEnvTrainingMode);

                using (StreamReader sr = File.OpenText(rlEnvConfig))
                {
                    envConfig = (JObject)JObject.Parse(sr.ReadToEnd())["env_config"];
                    actionDuration = envConfig["action_duration"].ToObject<int>();
                    string inputManagerSettingsPath = envConfig["input_manager_settings_path"].ToObject<string>();
                    inputManagerSettings = new InputManagerSettings(inputManagerSettingsPath, InputManagerMode.KEYBOARD);
                    inputSim = new InputSimulator(inputManagerSettings, this);
                }

                IPAddress ipAddress = IPAddress.Parse(rlEnvAddr);
                IPEndPoint localEndPoint = new IPEndPoint(ipAddress, listenPort);
                listenerSocket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                listenerSocket.Blocking = true;
                listenerSocket.Bind(localEndPoint);
                listenerSocket.Listen(10);
                clientSocket = listenerSocket.Accept();

                isReady = false;
                StartCoroutine(EnvLoop());
            } else
            {
                envId = "env_debug";
                workDir = Path.GetFullPath("env_debug_workdir");
                trainingMode = false;
                envConfig = new JObject();
                foreach (string configLine in DebugConfiguration)
                {
                    int equals = configLine.IndexOf("=");
                    if (equals < 0) { throw new Exception("Invalid debug config line: " + configLine); }
                    string configName = configLine.Substring(0, equals);
                    envConfig[configName] = JToken.Parse(configLine.Substring(equals + 1));
                }
                Debug.Log("Debug work directory: " + workDir);
                actionProvider = new NullActionProvider();
                if (!Directory.Exists(workDir)) { Directory.CreateDirectory(workDir); }
                StartCoroutine(DebugEnvLoop());
            }
        }

        private ISet<int> GetInvalidActions()
        {
            ISet<int> validActions = actionProvider.GetValidActions();
            int numActions = actionProvider.GetActionCount();
            ISet<int> invalidActions = new HashSet<int>();
            for (int action = 0; action < numActions; ++action)
            {
                if (!validActions.Contains(action))
                {
                    invalidActions.Add(action);
                }
            }
            return invalidActions;
        }

        private static void DeterminizeEnvironment()
        {
            UnityEngine.Random.InitState(1234);
        }

        private IEnumerator DoInitEnv()
        {
            if (trainingMode)
            {
                Application.targetFrameRate = 100000;
            }
            DeterminizeEnvironment();

            if (envConfig.ContainsKey("include_state_info") && envConfig["include_state_info"].ToObject<bool>())
            {
                AddInfoProvider(new StateInfoProvider());
            }
           
            if (envConfig.ContainsKey("code_cov_freq"))
            {
                AddInfoProvider(new CodeCovInfoProvider());
            }

            if (envConfig.ContainsKey("log_failure_detector") && envConfig["log_failure_detector"].ToObject<bool>())
            {
                AddFailureDetector(new LogFailureDetector());
            }

            foreach (IFailureDetector failureDetector in failureDetectors)
            {
                yield return StartCoroutine(failureDetector.Initialize(envId, workDir, envConfig, this));
            }

            ConfigureEnv();

            if (observationProvider == null) { throw new Exception("Observation provider not set during initialization"); }
            if (actionProvider == null) { throw new Exception("Action provider not set during initialization"); }
            if (rewardProvider == null) { throw new Exception("Reward provider not set during initialization"); }
            yield return StartCoroutine(observationProvider.Initialize(envId, workDir, envConfig, this));
            yield return StartCoroutine(actionProvider.Initialize(envId, workDir, envConfig, this));
            yield return StartCoroutine(rewardProvider.Initialize(envId, workDir, envConfig, this));
            foreach (IInfoProvider infoProvider in infoProviders)
            {
                yield return StartCoroutine(infoProvider.Initialize(envId, workDir, envConfig, this));
            }

            yield return StartCoroutine(EnterInitialState());

            isReady = true;
        }

        private IEnumerator DebugEnvLoop()
        {
            yield return StartCoroutine(DoInitEnv());
            yield return new WaitForEndOfFrame();

            object obs;
            JObject info;

            Debug.Log("Action space size: " + actionProvider.GetActionCount());

            for (; ; )
            {
                obs = CollectObservations();
                info = CollectInfo();
                Debug.Log("Observation: " + JsonConvert.SerializeObject(obs));
                Debug.Log("Info: " + JsonConvert.SerializeObject(info));

                int waitCounter = 100;
                while (waitCounter > 0)
                {
                    --waitCounter;
                    yield return null;
                }
                yield return new WaitForEndOfFrame();

                ISet<int> validActions = actionProvider.GetValidActions();
                Debug.Log("# valid actions: " + validActions.Count);

                float reward = rewardProvider.EvaluateAction();
                bool done = IsDone();

                Debug.Log("Reward: " + reward);

                if (done)
                {
                    Debug.Log("Done");
                    yield break;
                }
            }
        }

        private object CollectObservations()
        {
            IList<float> obs = observationProvider.CollectObservations();
            string imageObs = observationProvider.CollectImageObservation();
            if (imageObs != null)
            {
                return new
                {
                    vec = obs,
                    img = imageObs
                };
            } else
            {
                return obs;
            }
        }

        private JObject CollectInfo()
        {
            JObject info = new JObject();
            foreach (IInfoProvider infoProvider in infoProviders)
            {
                infoProvider.AddInfo(info);
            }
            ISet<string> failures = new HashSet<string>();
            foreach (IFailureDetector failureDetector in failureDetectors)
            {
                failureDetector.DetectFailures(failures);
            }
            if (failures.Count > 0)
            {
                info["failures"] = string.Join(";;;;;", failures); // This cannot be an array (breaks the DQN implementation)
            }
            return info;
        }

        private IEnumerator EnvLoop()
        {
            StartCoroutine(DoInitEnv());

            do
            {
                yield return new WaitForSeconds(0.5f);
                SendMessage(new
                {
                    ready = false
                });
            } while (!isReady);

            yield return new WaitForEndOfFrame();

            object obs = CollectObservations();
            JObject info = CollectInfo();
            DateTime timerStart = DateTime.Now;
            ISet<int> invalidActions = GetInvalidActions();
            DateTime timerEnd = DateTime.Now;
            TimeSpan timeValidActions = timerEnd - timerStart;
            info["time_valid_actions"] = (int)Math.Round(timeValidActions.TotalMilliseconds);

            SendMessage(new
            {
                ready = true,
                observation = obs,
                info = info,
                numActions = actionProvider.GetActionCount(),
                invalidActions = invalidActions
            });

            byte[] msgLenBuf = new byte[4];
            byte[] msgBuf = new byte[1024];

            for (; ; )
            {
                int nRec = clientSocket.Receive(msgLenBuf, 4, SocketFlags.None);
                if (nRec != 4)
                {
                    throw new Exception("unexpected # bytes received");
                }

                int msgLen = BitConverter.ToInt32(msgLenBuf, 0);
                if (msgLen > msgBuf.Length)
                {
                    msgBuf = new byte[msgLen];
                }

                nRec = clientSocket.Receive(msgBuf, msgLen, SocketFlags.None);
                if (nRec != msgLen)
                {
                    throw new Exception("unexpected # bytes received");
                }

                string strMsg = Encoding.UTF8.GetString(msgBuf, 0, msgLen);
                JObject msg = JObject.Parse(strMsg);
                if (!msg.ContainsKey("wait"))
                {
                    int actionId = msg["action"].ToObject<int>();
                    timerStart = DateTime.Now;
                    bool didPerform = actionProvider.PerformAction(actionId, inputSim, this);
                    timerEnd = DateTime.Now;
                    TimeSpan timePerformAction = timerEnd - timerStart;

                    int numFramesWait = actionDuration;
                    while (numFramesWait > 0)
                    {
                        --numFramesWait;
                        yield return null;
                    }
                    yield return new WaitForEndOfFrame();

                    float reward = rewardProvider.EvaluateAction();
                    bool done = IsDone();

                    if (done)
                    {
                        SendMessage(new
                        {
                            reward = reward,
                            done = true
                        });
                        break;
                    }
                    else
                    {
                        timerStart = DateTime.Now;
                        invalidActions = GetInvalidActions();
                        timerEnd = DateTime.Now;
                        timeValidActions = timerEnd - timerStart;
                        obs = CollectObservations();
                        info = CollectInfo();
                        info["time_valid_actions"] = (int)Math.Round(timeValidActions.TotalMilliseconds);
                        if (didPerform)
                        {
                            info["time_perform_action"] = (int)Math.Round(timePerformAction.TotalMilliseconds);
                        }
                        SendMessage(new
                        {
                            observation = obs,
                            info = info,
                            reward = reward,
                            done = false,
                            invalidActions = invalidActions
                        });
                    }
                }
            }

            yield break;
        }

        private void SendMessage(object msg)
        {
            string s = JsonConvert.SerializeObject(msg);
            byte[] b = Encoding.UTF8.GetBytes(s);
            byte[] l = BitConverter.GetBytes(b.Length);
            int count = clientSocket.Send(l);
            if (count != l.Length)
            {
                throw new Exception("failed to send all of message length");
            }
            count = clientSocket.Send(b);
            if (count != b.Length)
            {
                throw new Exception("failed to send all of message");
            }
        }

        protected void SetObservationProvider(IObservationProvider observationProvider)
        {
            this.observationProvider = observationProvider;
        }

        protected void SetActionProvider(IActionProvider actionProvider)
        {
            this.actionProvider = actionProvider;
        }

        protected void SetRewardProvider(IRewardProvider rewardProvider)
        {
            this.rewardProvider = rewardProvider;
        }

        protected void AddFailureDetector(IFailureDetector failureDetector)
        {
            failureDetectors.Add(failureDetector);
        }

        protected void AddInfoProvider(IInfoProvider infoProvider)
        {
            infoProviders.Add(infoProvider);
        }

        protected abstract void ConfigureEnv();

        protected virtual IEnumerator EnterInitialState()
        {
            yield break;
        }

        protected virtual bool IsDone()
        {
            return false;
        }
    }
}

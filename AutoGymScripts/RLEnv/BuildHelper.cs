#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityActionAnalysis;
using Microsoft.Data.Sqlite;

namespace UnityRLEnv
{
    public static class BuildHelper
    {
        private static string ACTION_ANALYSIS_PREF = "AutoGym/ActionAnalysisPath";
        private static string ALTCOVER_PREF = "AutoGym/AltCoverCommand";
        private static string ALTCOVER_EXPECTED_VERSION = "8.5.841";

        private static bool IsWindows()
        {
            return SystemInfo.operatingSystemFamily == OperatingSystemFamily.Windows;
        }

        private static void BuildEnvInternal(string outputPath, int playerWidth, int playerHeight)
        {
            string dirName = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }

            PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = playerWidth;
            PlayerSettings.defaultScreenHeight = playerHeight;
            PlayerSettings.macRetinaSupport = false;
            PlayerSettings.runInBackground = true;
            PlayerSettings.captureSingleScreen = false;
            PlayerSettings.usePlayerLog = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.allowFullscreenSwitch = false;
            PlayerSettings.forceSingleInstance = false;

            BuildTarget buildTarget = IsWindows() ? BuildTarget.StandaloneWindows64 : BuildTarget.StandaloneLinux64;
            BuildPipeline.BuildPlayer(EditorBuildSettings.scenes, outputPath + (IsWindows() ? ".exe" : ""), buildTarget, BuildOptions.Development);
        }

        // For use when building from command-line
        public static void BuildEnv()
        {
            string outputPath = Environment.GetEnvironmentVariable("ENV_BUILD_OUTPUT_PATH") + ".x86_64";
            int playerWidth = int.Parse(Environment.GetEnvironmentVariable("ENV_PLAYER_WIDTH"));
            int playerHeight = int.Parse(Environment.GetEnvironmentVariable("ENV_PLAYER_HEIGHT"));
            BuildEnvInternal(outputPath, playerWidth, playerHeight);
        }

        private static IEnumerator buildRoutineInstance;

        private static IEnumerator BuildRoutine(bool codeCov)
        {
            if (!EditorPrefs.HasKey(ACTION_ANALYSIS_PREF))
            {
                EditorUtility.DisplayDialog("Error", "AutoGym has not been configured yet, please first run AutoGym -> Configure.", "OK");
                yield break;
            }

            string uaaCmd = EditorPrefs.GetString(ACTION_ANALYSIS_PREF);
            if (!File.Exists(uaaCmd))
            {
                EditorUtility.DisplayDialog("Error", "AutoGym needs to be reconfigured, please run AutoGym -> Configure.", "OK");
                yield break;
            }

            string altcoverCmd = null;
            if (codeCov)
            {
                for (; ; )
                {
                    if (!EditorPrefs.HasKey(ALTCOVER_PREF))
                    {
                        altcoverCmd = EditorUtility.OpenFilePanel("Indicate the path of altcover", "", "");
                        if (altcoverCmd.Length == 0)
                        {
                            yield break;
                        }
                        EditorPrefs.SetString(ALTCOVER_PREF, altcoverCmd);
                    }
                    else
                    {
                        altcoverCmd = EditorPrefs.GetString(ALTCOVER_PREF);
                    }

                    if (!File.Exists(altcoverCmd))
                    {
                        EditorUtility.DisplayDialog("Error", "The indicated path does not exist.", "OK");
                        EditorPrefs.DeleteKey(ALTCOVER_PREF);
                        continue;
                    }

                    ProcessStartInfo altcoverVersionPs = new ProcessStartInfo(altcoverCmd)
                    {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        ArgumentList = {"Version"},
                        RedirectStandardOutput = true
                    };
                    Process altcoverVersionProc = Process.Start(altcoverVersionPs);
                    altcoverVersionProc.WaitForExit();
                    if (altcoverVersionProc.ExitCode != 0)
                    {
                        EditorUtility.DisplayDialog("Error", "Failed to execute altcover.", "OK");
                        EditorPrefs.DeleteKey(ALTCOVER_PREF);
                        continue;
                    }

                    string output = altcoverVersionProc.StandardOutput.ReadToEnd();
                    if (output.Replace("AltCover version ", "").Trim() != ALTCOVER_EXPECTED_VERSION)
                    {
                        EditorUtility.DisplayDialog("Error", "Incorrect version of altcover used, must use version " + ALTCOVER_EXPECTED_VERSION, "OK");
                        EditorPrefs.DeleteKey(ALTCOVER_PREF);
                        continue;
                    }

                    break;
                }
            }

            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                EditorUtility.DisplayDialog("Error",
                    "Asset Serialization Mode must be set to 'Force Text'. Please first go to Project Settings -> Editor and adjust this setting.",
                    "OK");
                yield break;
            }

            // Check first scene in build settings has an RLEnv instance
            {
                EditorBuildSettingsScene firstScene = null;

                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (scene.enabled)
                    {
                        firstScene = scene;
                        break;
                    }
                }

                if (firstScene == null)
                {
                    EditorUtility.DisplayDialog("Error",
                        "Build settings not configured. Please first configure the scenes in the build settings in File -> Build Settings...",
                        "OK");
                    yield break;
                }

                var scn = EditorSceneManager.OpenScene(firstScene.path);
                bool foundRLEnv = false;
                foreach (GameObject go in scn.GetRootGameObjects())
                {
                    if (go.TryGetComponent(out RLEnv env))
                    {
                        UnityEngine.Debug.Log("Found RLEnv of type " + env.GetType().FullName);
                        foundRLEnv = true;
                        break;
                    }
                }

                if (!foundRLEnv)
                {
                    EditorUtility.DisplayDialog("Error",
                        "Did not find a game object with an RLEnv component in the first scene. Please refer to the documentation for how to set up the game for use with AutoGym.",
                        "OK");
                    yield break;
                }
            }

            string outputFolder = EditorUtility.OpenFolderPanel("Indicate the output folder for the built game", "", "");
            if (outputFolder.Length == 0)
            {
                yield break;
            }
            outputFolder = Path.GetFullPath(outputFolder);
            Directory.CreateDirectory(outputFolder);

            string exeName = "game_env";

            ISet<string> ignoreNamespaces = new HashSet<string>();
            ISet<string> ignoreClasses = new HashSet<string>();

            ignoreNamespaces.Add("UnityRLEnv");
            ignoreNamespaces.Add("TMPro");

            string projectDir = Path.GetDirectoryName(Application.dataPath);
            string actionAnalysisCfgPath = Path.Combine(projectDir, "action_analysis_config.json");
            if (File.Exists(actionAnalysisCfgPath))
            {
                using (StreamReader sr = File.OpenText(actionAnalysisCfgPath))
                {
                    JObject aaCfg = JObject.Parse(sr.ReadToEnd());
                    if (aaCfg.ContainsKey("ignoreNamespaces"))
                    {
                        JArray arr = (JArray)aaCfg["ignoreNamespaces"];
                        foreach (JToken elem in arr)
                        {
                            ignoreNamespaces.Add(elem.ToString());
                        }
                    }
                    if (aaCfg.ContainsKey("ignoreClasses"))
                    {
                        JArray arr = (JArray)aaCfg["ignoreClasses"];
                        foreach (JToken elem in arr)
                        {
                            ignoreClasses.Add(elem.ToString());
                        }
                    }
                }
            }

            string projAssemblyDir = Path.Combine(projectDir, "Library", "ScriptAssemblies");
            string unityAssemblyDir = Path.GetDirectoryName(typeof(GameObject).Assembly.Location);
            var analysisConfig = new
            {
                assemblyPath = Path.Combine(projAssemblyDir, "Assembly-CSharp.dll"),
                databaseOutputDirectory = outputFolder,
                scriptOutputDirectory = Path.Combine(projectDir, "Assets"),
                assemblySearchDirectories = new List<string> { projAssemblyDir, unityAssemblyDir },
                ignoreNamespaces = ignoreNamespaces,
                ignoreClasses = ignoreClasses
            };

            string aaConfigTmpPath = Path.Combine(outputFolder, "analysis_config.json");
            using (StreamWriter sw = new StreamWriter(File.OpenWrite(aaConfigTmpPath)))
            {
                sw.Write(JsonConvert.SerializeObject(analysisConfig, Formatting.Indented));
            }

            ProcessStartInfo analysisStartInfo = new ProcessStartInfo(uaaCmd)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList = { aaConfigTmpPath }
            };
            Process analysisProcess = new Process();
            analysisProcess.StartInfo = analysisStartInfo;
            analysisProcess.EnableRaisingEvents = true;
            analysisProcess.OutputDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    UnityEngine.Debug.Log(e.Data);
                }
            };
            analysisProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data != null)
                {
                    UnityEngine.Debug.LogError(e.Data);
                }
            };
            analysisProcess.Start();
            analysisProcess.BeginOutputReadLine();
            analysisProcess.BeginErrorReadLine();
            while (!analysisProcess.HasExited)
            {
                yield return null;
            }

            if (analysisProcess.ExitCode != 0)
            {
                EditorUtility.DisplayDialog("Error", "The action analysis tool exited with failure status.", "OK");
                yield break;
            }

            string buildDir = Path.Combine(outputFolder, "build");
            BuildEnvInternal(Path.Combine(buildDir, exeName), 640, 480);

            string buildAssemblyDir = Path.Combine(buildDir, exeName + "_Data", "Managed");

            if (codeCov)
            {
                UnityEngine.Debug.Log("Performing code coverage instrumentation");

                string instrumentationDir = Path.Combine(buildDir, "Instrumentation");
                Directory.CreateDirectory(instrumentationDir);
                File.Move(Path.Combine(buildAssemblyDir, "Assembly-CSharp.dll"), Path.Combine(instrumentationDir, "Assembly-CSharp.dll"));
                File.Move(Path.Combine(buildAssemblyDir, "Assembly-CSharp.pdb"), Path.Combine(instrumentationDir, "Assembly-CSharp.pdb"));
                ProcessStartInfo altcoverStartInfo = new ProcessStartInfo(altcoverCmd)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    ArgumentList = {"--single", "--linecover", "--reportFormat=json", "--inplace",
                                    "--typeFilter=(UnityActionAnalysis|UnityRLEnv).*", "--pathFilter=.*(Unity.SourceGenerators).*", "--save"},
                    WorkingDirectory = instrumentationDir
                };
                Process altcoverProcess = Process.Start(altcoverStartInfo);
                altcoverProcess.WaitForExit();
                if (altcoverProcess.ExitCode != 0)
                {
                    EditorPrefs.DeleteKey(ALTCOVER_PREF);
                    EditorUtility.DisplayDialog("Error", "The code coverage instrumentation failed.", "OK");
                    yield break;
                }

                ProcessStartInfo fixupStartInfo = new ProcessStartInfo(uaaCmd)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    ArgumentList = {"--altcover-fixup", Path.Combine(instrumentationDir, "AltCover.Recorder.g.dll"), buildAssemblyDir }
                };
                Process fixupProcess = Process.Start(fixupStartInfo);
                fixupProcess.WaitForExit();
                if (fixupProcess.ExitCode != 0)
                {
                    EditorUtility.DisplayDialog("Error", "The code coverage instrumentation fix-up failed.", "OK");
                    yield break;
                }

                File.Move(Path.Combine(instrumentationDir, "Assembly-CSharp.dll"), Path.Combine(buildAssemblyDir, "Assembly-CSharp.dll"));
                File.Move(Path.Combine(instrumentationDir, "Assembly-CSharp.pdb"), Path.Combine(buildAssemblyDir, "Assembly-CSharp.pdb"));
                File.Move(Path.Combine(instrumentationDir, "AltCover.Recorder.g.dll"), Path.Combine(buildAssemblyDir, "AltCover.Recorder.g.dll"));

                File.Move(Path.Combine(instrumentationDir, "coverage.json"), Path.Combine(buildDir, "coverage.json"));
                File.Move(Path.Combine(instrumentationDir, "coverage.json.acv"), Path.Combine(buildDir, "coverage.json.acv"));
            }

            UnityEngine.Debug.Log("Instrumenting input APIs");
            var instrumentConfig = new
            {
                assemblyPath = Path.Combine(buildAssemblyDir, "Assembly-CSharp.dll"),
                assemblySearchDirectories = new List<string> { buildAssemblyDir },
                ignoreNamespaces = ignoreNamespaces,
                ignoreClasses = ignoreClasses
            };
            string instrumentConfigPath = Path.Combine(outputFolder, "instrument_config.json");
            using (StreamWriter sw = new StreamWriter(File.OpenWrite(instrumentConfigPath)))
            {
                sw.Write(JsonConvert.SerializeObject(instrumentConfig, Formatting.Indented));
            }
            ProcessStartInfo instrumentStartInfo = new ProcessStartInfo(uaaCmd)
            {
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true,
                UseShellExecute = false,
                ArgumentList = { "--instrument", instrumentConfigPath }
            };
            Process instrumentProcess = new Process();
            instrumentProcess.StartInfo = instrumentStartInfo;
            instrumentProcess.Start();
            instrumentProcess.WaitForExit();

            if (instrumentProcess.ExitCode != 0)
            {
                EditorUtility.DisplayDialog("Error", "The instrumentation tool exited with failure status.", "OK");
                yield break;
            }

            string gameExe = Directory.EnumerateFiles(buildDir, exeName + ".*").FirstOrDefault();
            if (string.IsNullOrEmpty(gameExe))
            {
                EditorUtility.DisplayDialog("Error", "Failed to build game.", "OK");
                yield break;
            }

            int numActions;

            string dbPath = Path.Combine(outputFolder, "paths.db");
            using (var connection = new SqliteConnection("Data Source=" + dbPath))
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                connection.Open();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "select count(*) from paths";
                    using (var reader = cmd.ExecuteReader())
                    {
                        reader.Read();
                        int numPaths = reader.GetInt32(0);
                        numActions = numPaths + 1; // number of paths + 1 no-op action
                    }
                }
            }

            string inputManagerAssetPath = Path.Combine(outputFolder, "InputManager.asset");
            File.Copy(Path.Combine(projectDir, "ProjectSettings", "InputManager.asset"),
                      inputManagerAssetPath);

            string gameName = Path.GetFileName(outputFolder).Replace(" ", "_").ToLower();

            var envConfig = new Dictionary<string, object>
            {
                { "num_actions", numActions },
                { "num_observation_features", 0 },
                { "observation_includes_image", true },
                { "observation_stack", 4 },
                { "image_resize_to", new List<int> { 84, 84 } },
                { "include_state_info", true },
                { "pre_init", false },
                { "symex_actions", true },
                { "symex_database_path", dbPath },
                { "input_manager_settings_path", inputManagerAssetPath },
                { "action_duration", 10 },
                { "time_limit", 300 }
            };
            if (codeCov)
            {
                envConfig.Add("code_cov_freq", 20);
            }

            var randomAgentConfig = new
            {
                config_name = gameName + "_random_aa",
                game_exe = gameExe,
                num_envs = 1,
                num_steps = 150000,
                tensorboard_log_path = Path.Combine(outputFolder, "tensorboard_logs"),
                tensorboard_log_name = gameName + "_random_aa",
                info_db_path = Path.Combine(outputFolder, "info", gameName + "_random_aa.db"),
                trainer = "random",
                env_config = envConfig,
                trainer_config = new Dictionary<string, object>
                {
                    { "checkpoint_path", Path.Combine(outputFolder, "checkpoints", gameName + "_random_aa.json") }
                }
            };

            var dqnAgentConfig = new
            {
                config_name = gameName + "_dqn_s84x4_count_aa",
                game_exe = gameExe,
                num_envs = 1,
                num_steps = 150000,
                tensorboard_log_path = Path.Combine(outputFolder, "tensorboard_logs"),
                tensorboard_log_name = gameName + "_dqn_s84x4_count_aa",
                info_db_path = Path.Combine(outputFolder, "info", gameName + "_dqn_s84x4_count_aa.db"),
                trainer = "dqn",
                env_config = envConfig,
                trainer_config = new Dictionary<string, object>
                {
                    { "learning_rate", 0.0001 },
                    { "discount_factor", 0.99 },
                    { "buffer_size", 5000 },
                    { "batch_size", 64 },
                    { "target_update_freq", 500 },
                    { "eps_initial", 1.0 },
                    { "eps_final", 0.05 },
                    { "eps_annealing_duration", 0.33 },
                    { "checkpoint_path", Path.Combine(outputFolder, "checkpoints", gameName + "_dqn_s84x4_count_aa") },
                    { "count_reward", true }
                }
            };

            string randomAgentConfigPath = Path.Combine(outputFolder, gameName + "_random_aa.json");
            using (StreamWriter sw = new StreamWriter(File.OpenWrite(randomAgentConfigPath)))
            {
                sw.Write(JsonConvert.SerializeObject(randomAgentConfig, Formatting.Indented));
            }

            string dqnAgentConfigPath = Path.Combine(outputFolder, gameName + "_dqn_s84x4_count_aa.json");
            using (StreamWriter sw = new StreamWriter(File.OpenWrite(dqnAgentConfigPath)))
            {
                sw.Write(JsonConvert.SerializeObject(dqnAgentConfig, Formatting.Indented));
            }

            EditorUtility.DisplayDialog("Success", "Environment build successful.", "OK");
        }

        private static void BuildUpdate()
        {
            if (!buildRoutineInstance.MoveNext())
            {
                EditorApplication.update -= BuildUpdate;
            }
        }

        [MenuItem("AutoGym/Build Gym Environment")]
        public static void BuildMenu()
        {
            // Run build procedure as a coroutine to avoid locking up the editor
            buildRoutineInstance = BuildRoutine(false);
            EditorApplication.update += BuildUpdate;
        }

        [MenuItem("AutoGym/Build Gym Environment (with Code Coverage)")]
        public static void BuildMenuCodeCov()
        {
            buildRoutineInstance = BuildRoutine(true);
            EditorApplication.update += BuildUpdate;
        }

        [MenuItem("AutoGym/Configure")]
        public static void ConfigureMenu()
        {
            string uaaCmd = EditorUtility.OpenFilePanel("Indicate the path of the UnityActionAnalysis tool", "", "");
            if (uaaCmd.Length == 0)
            {
                return;
            }

            if (!File.Exists(uaaCmd))
            {
                EditorUtility.DisplayDialog("Error", "The indicated path does not exist.", "OK");
                return;
            }

            EditorPrefs.SetString(ACTION_ANALYSIS_PREF, uaaCmd);
        }
    }
}
#endif

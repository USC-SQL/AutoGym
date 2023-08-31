using System;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Microsoft.Z3;
using Microsoft.Data.Sqlite;

namespace UnityActionAnalysis
{
    public class ActionManager : IDisposable
    {
        private Dictionary<MethodInfo, SymexMethod> symexMethods;
        private IPreconditionFuncs pfuncs;
        private Dictionary<int, MethodInfo> methodsById;
        private Dictionary<int, SymexPath> pathsById;
        private Context z3;

        public int ActionCount
        {
            get
            {
                return pathsById.Count;
            }
        }

        public ActionManager()
        {
        }

        public IEnumerable<bool> LoadActions(string dbPath)
        {
            using (var connection = new SqliteConnection("Data Source=" + dbPath))
            {
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_e_sqlite3());
                connection.Open();

                methodsById = new Dictionary<int, MethodInfo>();
                {
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "select id, signature from methods";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int methodId = reader.GetInt32(0);
                                string methodSig = reader.GetString(1);
                                MethodInfo method = SymexHelpers.GetMethodFromSignature(methodSig);
                                methodsById.Add(methodId, method);
                            }
                        }
                    }
                }

                z3 = new Context(new Dictionary<string, string>() { { "model", "true" } });
                symexMethods = new Dictionary<MethodInfo, SymexMethod>();
                pfuncs = (IPreconditionFuncs)Type.GetType("UnityActionAnalysis.PreconditionFuncs").GetConstructor(new Type[0]).Invoke(new object[0]);
                pathsById = new Dictionary<int, SymexPath>();

                int progressUpdateInterval = 100;
                var selectPathsCommand = connection.CreateCommand();
                selectPathsCommand.CommandText = "select id, pathindex, method, condition from paths";
                int numPathsRead = 0;
                using (var pathsReader = selectPathsCommand.ExecuteReader())
                {
                    while (pathsReader.Read())
                    {
                        int pathId = pathsReader.GetInt32(0);
                        int pathIndex = pathsReader.GetInt32(1);
                        int methodId = pathsReader.GetInt32(2);
                        string pathCondition = pathsReader.GetString(3);
                        BoolExpr[] parsedPathCond = z3.ParseSMTLIB2String(pathCondition);

                        MethodInfo method = methodsById[methodId];
                        SymexMethod m;
                        if (!symexMethods.TryGetValue(method, out m))
                        {
                            m = new SymexMethod(method);
                            symexMethods.Add(method, m);
                        }

                        Dictionary<int, Symcall> symcalls = new Dictionary<int, Symcall>();

                        var selectSymcallsCommand = connection.CreateCommand();
                        selectSymcallsCommand.CommandText = "select symcallid, method from symbolicmethodcalls where pathid = $pathId";
                        selectSymcallsCommand.Parameters.AddWithValue("$pathId", pathId);
                        using (var symcallsReader = selectSymcallsCommand.ExecuteReader())
                        {
                            while (symcallsReader.Read())
                            {
                                int symcallId = symcallsReader.GetInt32(0);
                                int symcallMethodId = symcallsReader.GetInt32(1);
                                MethodInfo symcallMethod = methodsById[symcallMethodId];

                                var selectArgsCommand = connection.CreateCommand();
                                selectArgsCommand.CommandText =
                                    "select value from smcarguments where symcallid = $symcallId and pathid = $pathId order by argindex";
                                selectArgsCommand.Parameters.AddWithValue("$symcallId", symcallId);
                                selectArgsCommand.Parameters.AddWithValue("$pathId", pathId);
                                List<SymexValue> args = new List<SymexValue>();
                                using (var selectArgsReader = selectArgsCommand.ExecuteReader())
                                {
                                    while (selectArgsReader.Read())
                                    {
                                        string value = selectArgsReader.GetString(0);
                                        args.Add(SymexValue.Parse(value));
                                    }
                                }
                                Symcall symcall = new Symcall(symcallMethod, args);
                                symcalls.Add(symcallId, symcall);
                            }

                            SymexPath p = new SymexPath(pathId, pathIndex, parsedPathCond, symcalls, m, z3);
                            m.paths.Add(p);
                            pathsById.Add(pathId, p);
                        }

                        ++numPathsRead;
                        if (numPathsRead % progressUpdateInterval == 0)
                        {
                            yield return false;
                        }
                    }
                }
            }
            yield return true;
        }

        public Dictionary<int, List<GameAction>> DetermineValidActions()
        {
            var start = DateTime.Now;
            Dictionary<int, List<GameAction>> actions = new Dictionary<int, List<GameAction>>();
            var gameObjects = UnityEngine.Object.FindObjectsOfType(typeof(GameObject));
            foreach (var o in gameObjects)
            {
                GameObject gameObject = (GameObject)o;
                if (!gameObject.activeInHierarchy)
                {
                    continue;
                }
                foreach (MonoBehaviour component in gameObject.GetComponents<MonoBehaviour>())
                {
                    if (component == null || !component.isActiveAndEnabled)
                    {
                        continue;
                    }
                    Type componentType = component.GetType();
                    foreach (MethodInfo m in componentType.GetMethods(BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly))
                    {
                        SymexMethod sm;
                        if (!symexMethods.TryGetValue(m, out sm))
                        {
                            continue;
                        }
                        foreach (SymexPath p in sm.paths)
                        {
                            if (p.CheckFeasible(component, pfuncs))
                            {
                                List<GameAction> actionInstances;
                                if (!actions.TryGetValue(p.pathId, out actionInstances))
                                {
                                    actionInstances = new List<GameAction>();
                                    actions.Add(p.pathId, actionInstances);
                                }
                                actionInstances.Add(new GameAction(p, component));
                            }
                        }
                    }
                }
            }
            return actions;
        }

        public void Dispose()
        {
            if (z3 != null)
            {
                z3.Dispose();
            }
        }
    }
}

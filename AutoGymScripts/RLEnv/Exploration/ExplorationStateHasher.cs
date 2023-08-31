using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;

namespace UnityRLEnv
{
    class HashedObj
    {
        public readonly int hash;
        public readonly Func<object> debugSerializer;

        public HashedObj(int hash, Func<object> debugSerializer)
        {
            this.hash = hash;
            this.debugSerializer = debugSerializer;
        }

        public static HashedObj Combine(IEnumerable<HashedObj> objs)
        {
            return new HashedObj(
                hash: HashUtil.CombineHashCodes(objs.Select(obj => obj.hash)),
                debugSerializer: () => new List<object>(objs.Select(obj => obj.debugSerializer())));
        }

        public static HashedObj CombineOrderIndependent(IEnumerable<HashedObj> objs)
        {
            return new HashedObj(
                hash: HashUtil.CombineHashCodesOrderIndependent(objs.Select(obj => obj.hash)),
                debugSerializer: () =>
                {
                    List<HashedObj> sorted = new List<HashedObj>(objs);
                    sorted.Sort((x, y) => x.hash - y.hash);
                    return new List<object>(sorted.Select(obj => obj.debugSerializer()));
                });
        }
    }

    public class ExplorationStateHasher : IDisposable
    {
        private string stateDumpDir;
        private int stateDumpCounter;

        public ExplorationStateHasher(string stateDumpDir = null)
        {
            this.stateDumpDir = stateDumpDir;
            stateDumpCounter = 1;
        }

        private static bool IgnoreGameObject(GameObject go)
        {
            return go.name.StartsWith("Unity.RecordedPlayback") || go.name.Equals("StartRecordedPlaybackFromEditor");
        }

        private static HashedObj HashGameObject(GameObject gameObject)
        {
            List<HashedObj> childHashes = new List<HashedObj>(gameObject.transform.childCount);
            var childCount = gameObject.transform.childCount;
            for (int i = 0; i != childCount; ++i)
            {
                GameObject child = gameObject.transform.GetChild(i).gameObject;
                if (child.activeInHierarchy && !IgnoreGameObject(child))
                {
                    childHashes.Add(HashGameObject(child));
                }
            }

            Component[] components = gameObject.GetComponents(typeof(Component));
            List<HashedObj> componentHashes = new List<HashedObj>(components.Length);

            foreach (Component c in components)
            {
                if (c == null)
                {
                    continue;
                }
                string name = c.GetType().Name;
                componentHashes.Add(new HashedObj(HashUtil.HashString(name), () => name));
            }

            return HashedObj.Combine(
               new List<HashedObj> {
                    HashedObj.CombineOrderIndependent(componentHashes),
                    HashedObj.CombineOrderIndependent(childHashes) });
        }

        struct SceneInfo
        {
            public Scene scn;
            public List<GameObject> roots;

            public HashedObj ComputeHash()
            {
                return HashedObj.CombineOrderIndependent(roots.Select(HashGameObject));
            }
        }

        public int ComputeCurrentHash()
        {
            UnityEngine.Object[] allGameObjects = GameObject.FindObjectsOfType(typeof(GameObject));
            Dictionary<Scene, HashSet<GameObject>> rootGameObjects = new Dictionary<Scene, HashSet<GameObject>>();

            foreach (UnityEngine.Object go in allGameObjects)
            {
                if (go == null)
                {
                    continue;
                }
                var root = ((GameObject)go).transform.root.gameObject;
                if (root.activeInHierarchy && !IgnoreGameObject(root))
                {
                    Scene scn = root.scene;
                    HashSet<GameObject> roots;
                    if (!rootGameObjects.TryGetValue(scn, out roots))
                    {
                        roots = new HashSet<GameObject>();
                        rootGameObjects.Add(scn, roots);
                    }
                    roots.Add(root);
                }
            }

            List<SceneInfo> scenes = new List<SceneInfo>();
            foreach (KeyValuePair<Scene, HashSet<GameObject>> p in rootGameObjects)
            {
                if (p.Key.name == "DontDestroyOnLoad")
                {
                    continue;
                }
                SceneInfo info = new SceneInfo()
                {
                    scn = p.Key,
                    roots = new List<GameObject>(p.Value)
                };
                info.roots.Sort((a, b) => a.transform.GetSiblingIndex() - b.transform.GetSiblingIndex());
                scenes.Add(info);
            }

            scenes.Sort((a, b) => a.scn.buildIndex - b.scn.buildIndex);

            HashedObj result = HashedObj.CombineOrderIndependent(scenes.Select(s => s.ComputeHash()));
            if (!string.IsNullOrEmpty(stateDumpDir))
            {
                object debugSer = result.debugSerializer();
                using (StreamWriter sw = new StreamWriter(File.OpenWrite(Path.Combine(stateDumpDir, (stateDumpCounter++) + ".json"))))
                {
                    sw.Write(JsonConvert.SerializeObject(new
                    {
                        stateHash = result.hash,
                        value = debugSer
                    }, Formatting.Indented));
                }
            }
            return result.hash;
        }

        public void Dispose()
        {
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text;

namespace UnityActionAnalysis
{
    public struct GameConfiguration
    {
        public string assemblyFileName;
        public string outputDatabase;
        public string outputPrecondFuncs;
        public List<string> assemblySearchDirectories;
        public ISet<string> ignoreNamespaces;
        public ISet<string> ignoreClasses;
        public bool slicingOpt;

        public GameConfiguration(string assemblyFileName, string outputDatabase, string outputPrecondFuncs, 
            List<string> assemblySearchDirs, ISet<string> ignoreNamespaces, ISet<string> ignoreClasses, bool slicingOpt)
        {
            this.assemblyFileName = assemblyFileName;
            this.outputDatabase = outputDatabase;
            this.outputPrecondFuncs = outputPrecondFuncs;
            assemblySearchDirectories = assemblySearchDirs;
            this.ignoreNamespaces = ignoreNamespaces;
            this.ignoreClasses = ignoreClasses;
            this.slicingOpt = slicingOpt;
        }

        public bool IsTypeIgnored(string fullTypeName)
        {
            if (ignoreClasses.Contains(fullTypeName))
            {
                return true;
            }
            foreach (string ignNs in ignoreNamespaces)
            {
                if (fullTypeName.StartsWith(ignNs + "."))
                {
                    return true;
                }
            }
            return false;
        }
    }

}

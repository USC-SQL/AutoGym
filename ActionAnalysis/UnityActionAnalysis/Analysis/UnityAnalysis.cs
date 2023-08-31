using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Diagnostics;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;

namespace UnityActionAnalysis
{
    public class UnityAnalysis
    {
        private CSharpDecompiler csd;
        private Dictionary<IMethod, ILFunction> ilFunctions;
        private MethodPool pool;

        public UnityAnalysis(CSharpDecompiler csd)
        {
            this.csd = csd;
            ilFunctions = new Dictionary<IMethod, ILFunction>();
            pool = new MethodPool();
        }

        public bool DoesInvokeInputAPI(IMethod m)
        {
            ReachableMethods rm = new ReachableMethods(m, pool);
            foreach (IMethod method in rm.FindReachableMethods())
            {
                if (UnityConfiguration.IsInputAPI(method))
                {
                    return true;
                }
            }
            return false;
        }

        public ITypeDefinition FindMonoBehaviourType()
        {
            return (ITypeDefinition)csd.TypeSystem.FindType(new FullTypeName("UnityEngine.MonoBehaviour"));
        }

        public IEnumerable<ITypeDefinition> FindMonoBehaviourComponents()
        {
            ITypeDefinition monoBehaviour = FindMonoBehaviourType();
            foreach (ITypeDefinition type in csd.TypeSystem.MainModule.TypeDefinitions)
            {
                if (type.Namespace != "UnityActionAnalysis" && type.IsDerivedFrom(monoBehaviour))
                {
                    yield return type;
                }
            }
            yield break;
        }

        public IEnumerable<IMethod> FindMonoBehaviourMethods(Predicate<IMethod> mbMethodPredicate)
        {
            ITypeDefinition monoBehaviour = FindMonoBehaviourType();
            foreach (ITypeDefinition type in FindMonoBehaviourComponents())
            {
                foreach (IMethod method in type.Methods)
                {
                    if (mbMethodPredicate(method))
                    {
                        yield return method;
                    }
                }
            }
            yield break;
        }
    }
}

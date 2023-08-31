using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection.PortableExecutable;
using System.Reflection.Metadata;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Microsoft.Z3;

namespace UnityActionAnalysis
{
    public class ActionAnalysis
    {
        public static CSharpDecompiler LoadAssembly(GameConfiguration gameConfig, string assemblyFileName)
        {
            var peFile = new PEFile(assemblyFileName,
                new FileStream(assemblyFileName, FileMode.Open, FileAccess.Read),
                streamOptions: PEStreamOptions.PrefetchEntireImage);
            var assemblyResolver = new UniversalAssemblyResolver(assemblyFileName, false,
                peFile.DetectTargetFrameworkId(),
                peFile.DetectRuntimePack(),
                PEStreamOptions.PrefetchEntireImage,
                MetadataReaderOptions.None);
            foreach (string searchDir in gameConfig.assemblySearchDirectories)
            {
                assemblyResolver.AddSearchDirectory(searchDir);
            }

            var settings = new DecompilerSettings();
            return new CSharpDecompiler(peFile, assemblyResolver, settings);
        }

        public static Mono.Cecil.ModuleDefinition LoadAssemblyCecil(GameConfiguration gameConfig, string assemblyFileName)
        {
            DefaultAssemblyResolver assemblyResolver = new DefaultAssemblyResolver();
            foreach (string searchDir in gameConfig.assemblySearchDirectories)
            {
                assemblyResolver.AddSearchDirectory(searchDir);
            }
            return Mono.Cecil.ModuleDefinition.ReadModule(assemblyFileName, new ReaderParameters() {
                AssemblyResolver = assemblyResolver
            });
        }

        public static void PrepareAssembly(Mono.Cecil.ModuleDefinition module, CSharpDecompiler decompiler, GameConfiguration gameConfig)
        {
            // Put in empty event handling functions to simplify analysis where needed
            UnityAnalysis ua = new UnityAnalysis(decompiler);
            foreach (var typeDef in ua.FindMonoBehaviourComponents())
            {
                if (gameConfig.IsTypeIgnored(typeDef.FullName)) 
                {
                    continue;
                }

                ISet<string> methodsToAdd = new HashSet<string>();

                var moduleTypeDef = module.GetType(typeDef.FullName);
                if (typeDef.GetMethods(m => m.Name == "OnMouseDown" && m.Parameters.Count == 0).Any() 
                || typeDef.GetMethods(m => m.Name == "OnMouseUp" && m.Parameters.Count == 0).Any()
                || typeDef.GetMethods(m => m.Name == "OnMouseDrag" && m.Parameters.Count == 0).Any()
                || typeDef.GetMethods(m => m.Name == "OnMouseUpAsButton" && m.Parameters.Count == 0).Any())
                {
                    if (!typeDef.GetMethods(m => m.Name == "OnMouseDown" && m.Parameters.Count == 0).Any())
                    {
                        methodsToAdd.Add("OnMouseDown");
                    }
                    if (!typeDef.GetMethods(m => m.Name == "OnMouseUp" && m.Parameters.Count == 0).Any())
                    {
                        methodsToAdd.Add("OnMouseUp");
                    }
                }

                if (typeDef.GetMethods(m => m.Name == "OnMouseEnter" && m.Parameters.Count == 0).Any()
                || typeDef.GetMethods(m => m.Name == "OnMouseExit" && m.Parameters.Count == 0).Any())
                {
                    if (!typeDef.GetMethods(m => m.Name == "OnMouseEnter" && m.Parameters.Count == 0).Any())
                    {
                        methodsToAdd.Add("OnMouseEnter");
                    }
                    if (!typeDef.GetMethods(m => m.Name == "OnMouseExit" && m.Parameters.Count == 0).Any())
                    {
                        methodsToAdd.Add("OnMouseExit");
                    }
                }

                foreach (string methodToAdd in methodsToAdd)
                {
                    var methodDef = new Mono.Cecil.MethodDefinition(methodToAdd, 0, module.TypeSystem.Void);
                    methodDef.Body.GetILProcessor().Emit(OpCodes.Ret);
                    moduleTypeDef.Methods.Add(methodDef);
                }
            }
        }

        public static void RemoveTryCatch(Mono.Cecil.ModuleDefinition module, GameConfiguration config)
        {
            foreach (Mono.Cecil.TypeDefinition type in module.Types)
            {
                if (type.Namespace == "UnityActionAnalysis" || config.IsTypeIgnored(type.FullName))
                {
                    continue;
                }
                foreach (Mono.Cecil.MethodDefinition method in type.Methods)
                {
                    ISet<Instruction> instToRemove = new HashSet<Instruction>(); 
                    var methodBody = method.Body;
                    if (methodBody == null)
                    {
                        continue;
                    }
                    foreach (var eh in methodBody.ExceptionHandlers)
                    {
                        switch (eh.HandlerType) {
                            case ExceptionHandlerType.Catch:
                            case ExceptionHandlerType.Fault:
                            case ExceptionHandlerType.Filter:
                                for (Instruction inst = eh.HandlerStart.Previous; 
                                    inst != eh.HandlerEnd; inst = inst.Next)
                                {
                                    instToRemove.Add(inst);
                                }
                                break;
                            case ExceptionHandlerType.Finally:
                                instToRemove.Add(eh.HandlerStart.Previous);
                                instToRemove.Add(eh.HandlerEnd.Previous);
                                break;
                            default:
                                throw new Exception("unexpected HandlerType " + eh.HandlerType);
                        } 
                        
                    }
                    foreach (Instruction inst in instToRemove)
                    {
                        methodBody.Instructions.Remove(inst);
                    }
                    methodBody.ExceptionHandlers.Clear();
                }
            }   
        }

        public static List<IMethod> FindEntrypoints(UnityAnalysis ua, GameConfiguration gameConfig)
        {
            ISet<string> targetBehaviourMethods = new HashSet<string>() {
                "Update", "FixedUpdate", "LateUpdate",
                "OnMouseOver", "OnMouseDown", "OnMouseUp",
                "OnMouseEnter", "OnMouseExit", "OnMouseDrag",
                "OnMouseUpAsButton"
            };

            List<IMethod> targets = new List<IMethod>();
            foreach (IMethod method in ua.FindMonoBehaviourMethods(m =>
                !gameConfig.IsTypeIgnored(m.DeclaringType.FullName) &&
                m.Parameters.Count == 0 && targetBehaviourMethods.Contains(m.Name)))
            {
                if (ua.DoesInvokeInputAPI(method) || method.Name.StartsWith("OnMouse"))
                {
                    targets.Add(method);
                }
            }

            return targets;
        }

        private static void DoAnalysis(GameConfiguration gameConfig, string mainAssemblyFileName)
        {
            SymexMachine.SetUpGlobals();

            var decompiler = LoadAssembly(gameConfig, mainAssemblyFileName);
            var ua = new UnityAnalysis(decompiler);

            List<IMethod> targets = FindEntrypoints(ua, gameConfig);

            var databaseFile = gameConfig.outputDatabase;
            if (File.Exists(databaseFile))
            {
                File.Delete(databaseFile);
            }

            using (var db = new DatabaseUtil(databaseFile))
            using (var z3 = new Context(new Dictionary<string, string>() { { "model", "true" } }))
            {
                string pfuncsFile = gameConfig.outputPrecondFuncs;
                if (File.Exists(pfuncsFile))
                {
                    File.Delete(pfuncsFile);
                }

                PreconditionFuncsGen pfg = new PreconditionFuncsGen();

                foreach (IMethod method in targets)
                {
                    Console.WriteLine("Processing " + method.FullName + "(" + string.Join(",", method.Parameters.Select(param => param.Type.FullName)) + ")");
                    MethodPool methodPool = new MethodPool();
                    InputAnalysis inputAnalysis = new InputAnalysis(method, methodPool);
                    Console.WriteLine("\tRunning input analysis");
                    InputAnalysisResult iaResult = inputAnalysis.PerformAnalysis();
                    LeadsToInputAnalysis ltAnalysis = new LeadsToInputAnalysis(method, iaResult, methodPool);
                    LeadsToInputAnalysisResult ltResult = ltAnalysis.PerformAnalysis();
                    SymexMachine m = new SymexMachine(decompiler, method, methodPool, new UnityConfiguration(iaResult, ltResult));
                    try
                    {
                        Console.WriteLine("\tRunning symbolic execution");
                        m.Run();
                        Console.WriteLine("\tWriting path information to database");
                        db.AddPaths(method, m);
                        Console.WriteLine("\tGenerating code");
                        pfg.ProcessMethod(method, m);
                    } catch (SymexUnsupportedException e) 
                    {
                        Console.WriteLine("\tFailed to symbolically execute method (skipping): " + e.Message);
                    } finally
                    {
                        m.Dispose();
                    }
                }

                pfg.Finish();

                Console.WriteLine("Generating file " + pfuncsFile);
                using (var pfuncsStream = File.OpenWrite(pfuncsFile))
                using (var pfuncsOut = new StreamWriter(pfuncsStream))
                {
                    pfg.Write(pfuncsOut);
                }
            }

            Console.WriteLine("Wrote database " + databaseFile);
        }

        private static void PrepareAssemblyForAnalysis(GameConfiguration gameConfig, string tempAssemblyFileName)
        {
            var decompiler = LoadAssembly(gameConfig, gameConfig.assemblyFileName);
            using (var module = LoadAssemblyCecil(gameConfig, gameConfig.assemblyFileName))
            {
                // Add a reference to the Input API assembly if it not present before starting analysis
                // (Analysis requires the Input API types to be present)
                string inputAssemblyName = "UnityEngine.InputLegacyModule";
                bool containsInputAssembly = false;
                foreach (var assemblyRef in module.AssemblyReferences)
                {
                    if (assemblyRef.Name == inputAssemblyName)
                    {
                        containsInputAssembly = true;
                        break;
                    }
                }
                if (!containsInputAssembly)
                {
                    module.AssemblyReferences.Add(new Mono.Cecil.AssemblyNameReference(inputAssemblyName, new System.Version(0,0,0,0)));
                }

                PrepareAssembly(module, decompiler, gameConfig);
                RemoveTryCatch(module, gameConfig);

                if (gameConfig.slicingOpt)
                {
                    UnityAnalysis ua = new UnityAnalysis(decompiler);
                    List<IMethod> entryPoints = FindEntrypoints(ua, gameConfig);
                    InputSlicer.PerformInputSlice(module, decompiler, entryPoints);
                }

                module.Write(tempAssemblyFileName);
            }
        }

        public static void Run(GameConfiguration gameConfig)
        {
            Console.WriteLine("Performing analysis of " + Path.GetFileName(gameConfig.assemblyFileName));

            var assemblyFileName = gameConfig.assemblyFileName;
            if (InputInstrumentation.IsInstrumented(assemblyFileName, gameConfig))
            {
                assemblyFileName = assemblyFileName + ".orig";
                Console.WriteLine("Info: Assembly is instrumented, using original copy at " + assemblyFileName);
                if (!File.Exists(assemblyFileName))
                {
                    throw new Exception("Original assembly copy does not exist");
                }
                if (InputInstrumentation.IsInstrumented(assemblyFileName, gameConfig))
                {
                    throw new Exception("Original assembly copy is instrumented as well, aborting");
                }
                gameConfig.assemblyFileName = assemblyFileName;
            }

            var tempAssemblyFileName = assemblyFileName + ".tmp.dll";
            PrepareAssemblyForAnalysis(gameConfig, tempAssemblyFileName);
            DoAnalysis(gameConfig, tempAssemblyFileName);
        }
    }
}

// Developed for AltCover version 8.5.841
// Fixes compatibility with Unity and introduces other functionality needed for measuring code coverage during game play

using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CommandLine;

namespace AltCoverInstrFixup
{
    public class Program
    {

        public class Options
        {
            [Value(0, MetaName = "assembly", HelpText = "Path to AltCover.Recorder.g.dll")]
            public string AltCoverAssemblyPath { get; set; }

            [Value(1, MetaName = "managed", HelpText = "Path to Managed folder")]
            public string ManagedAssemblyDirPath {get; set;}

            [Option("altcover-fixup",
                Required = false,
                Default = false)]
            public bool AltcoverFixup { get; set; } // always true
        }

        private static MethodDefinition FindFirstMethodWithName(TypeDefinition typeDef, string name)
        {
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.Name == name)
                {
                    return methodDef;
                }
            }
            throw new Exception("Failed to find method '" + name + "' in " + typeDef.FullName);
        }

        private static MethodDefinition FindZeroArgToString(TypeDefinition typeDef)
        {
            foreach (MethodDefinition methodDef in typeDef.Methods)
            {
                if (methodDef.Name == "ToString" && methodDef.Parameters.Count == 0)
                {
                    return methodDef;
                }
            }
            throw new Exception("Failed to find ToString() in " + typeDef.FullName);
        }

        private static MethodDefinition FindThreeArgConcat(TypeDefinition stringType)
        {
            foreach (MethodDefinition methodDef in stringType.Methods)
            {
                if (methodDef.Name == "Concat" && methodDef.Parameters.Count == 3 && 
                    methodDef.Parameters.All(t => t.ParameterType.FullName == "System.String"))
                {
                    return methodDef;
                }
            }
            throw new Exception("Failed to find three-arg concat in " + stringType.FullName);
        }

        public static void Run(Options opts)
        {
            string recorderTypeName = "<StartupCode$AltCover-Recorder>.$Recorder";
            string tracerTypeName = "<StartupCode$AltCover-Recorder>.$Tracer";
            string instanceTypeName = "AltCover.Recorder.Instance";
            ModuleDefinition module = ModuleDefinition.ReadModule(opts.AltCoverAssemblyPath);
            TypeDefinition recorder = module.GetType(recorderTypeName);
            if (recorder == null)
            {
                throw new Exception("Could not find " + recorderTypeName);
            }
            bool foundRecorderCctor = false;
            foreach (MethodDefinition method in recorder.Methods)
            {
                if (method.IsStatic && method.IsConstructor)
                {
                    Mono.Collections.Generic.Collection<Instruction> insts = method.Body.Instructions;
                    bool foundInst = false;
                    for (int i = 0, n = insts.Count; i < n; ++i)
                    {
                        Instruction inst = insts[i];
                        string s = inst.ToString();
                        if (s.EndsWith("ldstr \".mutex\""))
                        {
                            --i;
                            insts.RemoveAt(i);
                            insts.RemoveAt(i);
                            insts.RemoveAt(i);
                            Instruction newObjInst = insts[i];
                            MethodReference methodRef = (MethodReference)newObjInst.Operand;
                            newObjInst.Operand = new MethodReference(".ctor", returnType: methodRef.ReturnType, methodRef.DeclaringType)
                            {
                                HasThis = true,
                                Parameters =
                                {
                                    methodRef.Parameters[0]
                                }
                            };
                            foundInst = true;
                            break;
                        }
                    }
                    if (!foundInst)
                    {
                        throw new Exception("Could not find mutex construction");
                    }

                    foundRecorderCctor = true;
                    break;
                }
            }
            if (!foundRecorderCctor)
            {
                throw new Exception("Could not find " + recorderTypeName + "..cctor");
            }

            ModuleDefinition systemModule = ModuleDefinition.ReadModule(Path.Join(opts.ManagedAssemblyDirPath, "System.dll"));
            ModuleDefinition mscorlibModule = ModuleDefinition.ReadModule(Path.Join(opts.ManagedAssemblyDirPath, "mscorlib.dll"));
            TypeDefinition process = systemModule.GetType("System.Diagnostics.Process");
            TypeDefinition int32 = mscorlibModule.GetType("System.Int32");
            TypeDefinition stringType = mscorlibModule.GetType("System.String");
            MethodDefinition getCurrentProcess = FindFirstMethodWithName(process, "GetCurrentProcess");
            MethodDefinition getId = FindFirstMethodWithName(process, "get_Id");

            MethodDefinition acvFormatFunc = null;
            foreach (TypeDefinition t in module.GetType(tracerTypeName).NestedTypes)
            {
                if (t.Name == "Connect@58-2")
                {
                    foreach (MethodDefinition md in t.Methods)
                    {
                        if (md.Name == "Invoke")
                        {
                            acvFormatFunc = md;
                        }
                    }
                    break;
                }
            }
            if (acvFormatFunc == null)
            {
                throw new Exception("Failed to find acv format function");
            }
            {
                VariableDefinition vdi = new VariableDefinition(module.ImportReference(int32));
                VariableDefinition vds = new VariableDefinition(module.ImportReference(stringType));
                acvFormatFunc.Body.Variables.Clear();
                acvFormatFunc.Body.Variables.Add(vdi);
                acvFormatFunc.Body.Variables.Add(vds);

                Mono.Collections.Generic.Collection<Instruction> insts = acvFormatFunc.Body.Instructions;
                insts.Clear();
                var processor = acvFormatFunc.Body.GetILProcessor();
                insts.Add(processor.Create(OpCodes.Nop));
                insts.Add(processor.Create(OpCodes.Ldstr, "coverage.json."));
                insts.Add(processor.Create(OpCodes.Call, module.ImportReference(getCurrentProcess)));
                insts.Add(processor.Create(OpCodes.Callvirt, module.ImportReference(getId)));
                insts.Add(processor.Create(OpCodes.Stloc, vdi));
                insts.Add(processor.Create(OpCodes.Ldloca_S, vdi));
                insts.Add(processor.Create(OpCodes.Call, module.ImportReference(FindZeroArgToString(int32))));
                insts.Add(processor.Create(OpCodes.Ldstr, ".acv"));
                insts.Add(processor.Create(OpCodes.Call, module.ImportReference(FindThreeArgConcat(stringType))));
                insts.Add(processor.Create(OpCodes.Stloc, vds));
                Instruction branchToInst = processor.Create(OpCodes.Ldloc, vds);
                insts.Add(processor.Create(OpCodes.Br_S, branchToInst));
                insts.Add(branchToInst);
                insts.Add(processor.Create(OpCodes.Ret));
            }

            TypeDefinition instance = module.GetType(instanceTypeName);
            if (instance == null)
            {
                throw new Exception("Could not find " + instanceTypeName);
            }
            bool foundInstanceReportFile = false;
            foreach (PropertyDefinition prop in instance.Properties)
            {
                if (prop.Name == "ReportFile")
                {
                    MethodDefinition reportFileGetter = prop.GetMethod;
                    var processor = reportFileGetter.Body.GetILProcessor();
                    var insts = reportFileGetter.Body.Instructions;
                    insts.Clear();

                    insts.Add(processor.Create(OpCodes.Nop));
                    insts.Add(processor.Create(OpCodes.Ldstr, "coverage.json"));
                    insts.Add(processor.Create(OpCodes.Ret));

                    foundInstanceReportFile = true;
                    break;
                }
            }
            if (!foundInstanceReportFile)
            {
                throw new Exception("Could not find " + instanceTypeName + ".ReportFile");
            }

            module.Write(opts.AltCoverAssemblyPath + ".tmp");
            module.Dispose();
            string origPath = opts.AltCoverAssemblyPath + ".orig";
            if (!File.Exists(origPath))
            {
                File.Move(opts.AltCoverAssemblyPath!, origPath);
            }
            File.Move(opts.AltCoverAssemblyPath + ".tmp", opts.AltCoverAssemblyPath!);
        }
    }
}

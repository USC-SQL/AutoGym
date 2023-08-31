using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using ICSharpCode.Decompiler.TypeSystem;
using ICSharpCode.Decompiler.IL;
using Microsoft.Z3;
using UnityActionAnalysis.Operations;

namespace UnityActionAnalysis
{
    public class ConfigData
    {
        public Dictionary<(string, string), int> symcallIds;
        public int? pathId;

        public ConfigData()
        {
            symcallIds = new Dictionary<(string, string), int>();
        }

        public ConfigData(ConfigData o)
        {
            symcallIds = new Dictionary<(string, string), int>(o.symcallIds);
            pathId = o.pathId;
        }
    }

    public class UnityConfiguration : Configuration
    {
        private InputAnalysisResult iaResult;
        private LeadsToInputAnalysisResult ltResult;

        public UnityConfiguration(InputAnalysisResult iaResult, LeadsToInputAnalysisResult ltResult)
        {
            this.iaResult = iaResult;
            this.ltResult = ltResult;
        }

        public override void InitializeStates()
        {
            string entrypointName = SymexMachine.Instance.Entrypoint.Name;
            switch (entrypointName) {
                case "Update":
                case "FixedUpdate":
                case "LateUpdate":
                    break;
                case "OnMouseUp":
                    {
                        var z3 = SymexMachine.Instance.Z3;
                        SymexState state = SymexMachine.Instance.States[0];
                        IType inputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityEngine.Input"));
                        IType boolType = SymexMachine.Instance.CSD.TypeSystem.FindType(KnownTypeCode.Boolean);
                        IMethod getMouseButton = inputType.GetMethods(m => m.Name == "GetMouseButton").First();
                        BitVecSort mouseButtonArgSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.Parameters[0].Type);
                        BitVecSort inputStateSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.ReturnType);

                        var mouseButtonZero = z3.MkBV(0, mouseButtonArgSort.Size);
                        MakeSymcallForInput(getMouseButton, new List<Expr>{mouseButtonZero}, state, out Expr mouseButtonState, 
                            out int _, out bool _);
                        BitVecExpr instanceMouseWasDown = (BitVecExpr)state.MakeSymbolicValue(boolType, "special:instancemousewasdown");

                        SymexState negState = state.Fork();
                        negState.pathCondition.Add(z3.MkNot(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size))));
                        negState.execStatus = ExecutionStatus.ABORTED;

                        state.pathCondition.Add(z3.MkNot(z3.MkEq(instanceMouseWasDown, z3.MkBV(0, instanceMouseWasDown.SortSize))));
                        state.pathCondition.Add(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size)));
                        break;
                    }
                case "OnMouseDrag":
                    {
                        var z3 = SymexMachine.Instance.Z3;
                        SymexState state = SymexMachine.Instance.States[0];
                        IType inputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityEngine.Input"));
                        IType boolType = SymexMachine.Instance.CSD.TypeSystem.FindType(KnownTypeCode.Boolean);
                        IMethod getMouseButton = inputType.GetMethods(m => m.Name == "GetMouseButton").First();
                        BitVecSort mouseButtonArgSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.Parameters[0].Type);
                        BitVecSort inputStateSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.ReturnType);
                        
                        var mouseButtonZero = z3.MkBV(0, mouseButtonArgSort.Size);
                        MakeSymcallForInput(getMouseButton, new List<Expr>{mouseButtonZero}, state, out Expr mouseButtonState, 
                            out int _, out bool _);
                        BitVecExpr instanceMouseWasDown = (BitVecExpr)state.MakeSymbolicValue(boolType, "special:instancemousewasdown");

                        SymexState negState = state.Fork();
                        negState.pathCondition.Add(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size)));
                        negState.execStatus = ExecutionStatus.ABORTED;

                        state.pathCondition.Add(z3.MkNot(z3.MkEq(instanceMouseWasDown, z3.MkBV(0, instanceMouseWasDown.SortSize))));
                        break;
                    }
                case "OnMouseOver":
                case "OnMouseDown":
                case "OnMouseEnter":
                case "OnMouseExit":
                case "OnMouseUpAsButton":
                    {
                        var z3 = SymexMachine.Instance.Z3;
                        SymexState state = SymexMachine.Instance.States[0];
                        IType inputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityEngine.Input"));
                        IType instrInputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityActionAnalysis.InstrInput"));
                        IType vectorType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityEngine.Vector3"));
                        IType floatType = SymexMachine.Instance.CSD.TypeSystem.FindType(KnownTypeCode.Single);
                        IType boolType = SymexMachine.Instance.CSD.TypeSystem.FindType(KnownTypeCode.Boolean);
                        IMethod getMouseButton = inputType.GetMethods(m => m.Name == "GetMouseButton").First();
                        IMethod instrGetMouseButton = instrInputType.GetMethods(m => m.Name == "GetMouseButton").First();
                        BitVecSort mouseButtonArgSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.Parameters[0].Type);
                        var mouseButtonZero = z3.MkBV(0, mouseButtonArgSort.Size);
                        BitVecSort inputStateSort = (BitVecSort)SymexMachine.Instance.SortPool.TypeToSort(getMouseButton.ReturnType);
                        DatatypeSort mpSort = (DatatypeSort)SymexMachine.Instance.SortPool.TypeToSort(vectorType);
                        FuncDecl vecX = Helpers.FindFieldAccessor(mpSort, vectorType.GetFields(fld => fld.Name == "x").First());
                        FuncDecl vecY = Helpers.FindFieldAccessor(mpSort, vectorType.GetFields(fld => fld.Name == "y").First());
                        IMethod getMousePosition = inputType.GetProperties(p => p.Name == "mousePosition").First().Getter;
                        MakeSymcallForInput(getMousePosition, new List<Expr>(), state, out Expr mousePos, out int symId, out bool firstCall);
                        Debug.Assert(firstCall);
                        RealExpr mouseX = (RealExpr)vecX.Apply(mousePos).Simplify(); 
                        RealExpr mouseY = (RealExpr)vecY.Apply(mousePos).Simplify();
                        RealExpr instanceMouseBoundsMinX = (RealExpr)state.MakeSymbolicValue(floatType, "special:instancemouseboundsminx");
                        RealExpr instanceMouseBoundsMaxX = (RealExpr)state.MakeSymbolicValue(floatType, "special:instancemouseboundsmaxx");
                        RealExpr instanceMouseBoundsMinY = (RealExpr)state.MakeSymbolicValue(floatType, "special:instancemouseboundsminy");
                        RealExpr instanceMouseBoundsMaxY = (RealExpr)state.MakeSymbolicValue(floatType, "special:instancemouseboundsmaxy");
                        BoolExpr mouseInBoundsCond = z3.MkAnd(
                            z3.MkGe(mouseX, instanceMouseBoundsMinX),
                            z3.MkLe(mouseX, instanceMouseBoundsMaxX),
                            z3.MkGe(mouseY, instanceMouseBoundsMinY),
                            z3.MkLe(mouseY, instanceMouseBoundsMaxY));
                        BoolExpr mouseRangeCond = z3.MkAnd(
                            z3.MkGe(mouseX, z3.MkReal(0)),
                            z3.MkLe(mouseX, z3.MkReal(1)), 
                            z3.MkGe(mouseY, z3.MkReal(0)), 
                            z3.MkLe(mouseY, z3.MkReal(1)));
                        switch (entrypointName) {
                            case "OnMouseOver":
                                {
                                    SymexState negState = state.Fork();
                                    negState.pathCondition.Add(z3.MkNot(mouseInBoundsCond));
                                    negState.pathCondition.Add(mouseRangeCond);
                                    negState.execStatus = ExecutionStatus.ABORTED;

                                    state.pathCondition.Add(mouseInBoundsCond);
                                    break;   
                                }
                            case "OnMouseDown":
                                {
                                    MakeSymcallForInput(getMouseButton, new List<Expr>{mouseButtonZero}, state, out Expr mouseButtonState, 
                                        out int _, out bool _);
                                    MakeSymcallForInput(instrGetMouseButton, new List<Expr>{mouseButtonZero}, state, out Expr prevMouseButtonState, 
                                        out int _, out bool _);
                                    
                                    SymexState negState = state.Fork();
                                    negState.pathCondition.Add(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size)));
                                    negState.execStatus = ExecutionStatus.ABORTED;
                                    
                                    state.pathCondition.Add(z3.MkEq(prevMouseButtonState, z3.MkBV(0, inputStateSort.Size)));
                                    state.pathCondition.Add(z3.MkNot(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size))));
                                    state.pathCondition.Add(mouseInBoundsCond);
                                    break;
                                }
                            case "OnMouseEnter":
                                {
                                    BitVecExpr instanceMouseDidEnter = (BitVecExpr)state.MakeSymbolicValue(boolType, "special:instancemousedidenter");

                                    SymexState negState = state.Fork();
                                    negState.pathCondition.Add(z3.MkNot(mouseInBoundsCond));
                                    negState.pathCondition.Add(mouseRangeCond);
                                    negState.execStatus = ExecutionStatus.ABORTED;

                                    state.pathCondition.Add(z3.MkEq(instanceMouseDidEnter, z3.MkBV(0, instanceMouseDidEnter.SortSize)));
                                    state.pathCondition.Add(mouseInBoundsCond);
                                    break;
                                }
                            case "OnMouseExit":
                                {
                                    BitVecExpr instanceMouseDidEnter = (BitVecExpr)state.MakeSymbolicValue(boolType, "special:instancemousedidenter");

                                    SymexState negState = state.Fork();
                                    negState.pathCondition.Add(mouseInBoundsCond);
                                    negState.execStatus = ExecutionStatus.ABORTED;

                                    state.pathCondition.Add(z3.MkNot(z3.MkEq(instanceMouseDidEnter, z3.MkBV(0, instanceMouseDidEnter.SortSize))));
                                    state.pathCondition.Add(z3.MkNot(mouseInBoundsCond));
                                    state.pathCondition.Add(mouseRangeCond);
                                    break;
                                }
                            case "OnMouseUpAsButton":
                                {
                                    MakeSymcallForInput(getMouseButton, new List<Expr>{mouseButtonZero}, state, out Expr mouseButtonState, 
                                        out int _, out bool _);
                                    BitVecExpr instanceMouseWasDown = (BitVecExpr)state.MakeSymbolicValue(boolType, "special:instancemousewasdown");

                                    SymexState negState = state.Fork();
                                    negState.pathCondition.Add(z3.MkNot(mouseInBoundsCond));
                                    negState.pathCondition.Add(mouseRangeCond);
                                    negState.execStatus = ExecutionStatus.ABORTED;

                                    state.pathCondition.Add(z3.MkNot(z3.MkEq(instanceMouseWasDown, z3.MkBV(0, instanceMouseWasDown.SortSize))));
                                    state.pathCondition.Add(z3.MkEq(mouseButtonState, z3.MkBV(0, inputStateSort.Size)));
                                    state.pathCondition.Add(mouseInBoundsCond);
                                    break;
                                }
                            default:
                                Debug.Fail("unhandled entrypoint " + entrypointName);
                                break;
                        }
                        break;
                    }
                default:
                    throw new Exception("unexpected symex entrypoint " + entrypointName);
            }
        }

        public override bool IsMethodSummarized(IMethod method)
        {
            if (!method.HasBody || IsInputAPI(method))
            {
                return true;
            }
            if (method.ParentModule != SymexMachine.Instance.CSD.TypeSystem.MainModule)
            {
                return true;
            }

            ILInstruction entryPoint = SymexMachine.Instance.MethodPool.MethodEntryPoint(method).GetInstruction();
            string methodSig = AnalysisHelpers.MethodSignature(method);
            if (ltResult.methodResults.TryGetValue(methodSig, out LeadsToInputAnalysisMethodResult res))
            {
                return res.leadsToInputPoints.Count == 0;
            } else
            {
                return true;
            }
        }

        private void MakeSymcallForInput(IMethod method, List<Expr> arguments, SymexState state, out Expr value, out int symId, out bool firstCall)
        {
            ConfigData cdata = (ConfigData)state.customData;
            string args = string.Join(";", arguments.Select(arg => JsonSerializer.Serialize(state.SerializeExpr(arg))));
            var p = (method.ReflectionName, args);
            if (!cdata.symcallIds.TryGetValue(p, out symId))
            {
                symId = state.symcallCounter++;
                cdata.symcallIds.Add(p, symId);
                firstCall = true;
            } else
            {
                firstCall = false;
            }
            value = MakeSymcall(method, arguments, symId, state, firstCall);
        }

        public override void ApplyMethodSummary(IMethod method, List<Expr> arguments, Variable resultVar, SymexState state)
        {
            if (IsInputAPI(method))
            {
                Context z3 = SymexMachine.Instance.Z3;
                if (method.FullName.StartsWith("UnityEngine.Input.GetAxis")) {
                    MakeSymcallForInput(method, arguments, state, out Expr inputState, out int _, out bool firstCall);
                    state.MemoryWrite(resultVar.address, inputState);
                    if (firstCall)
                    {
                        var inputStateSort = (RealSort)inputState.Sort;
                        var zero = z3.MkReal(0);

                        Solver solv = z3.MkSolver();
                        foreach (BoolExpr cond in state.pathCondition)
                        {
                            solv.Assert(cond);
                        }

                        List<BoolExpr> axisCases = new List<BoolExpr> {
                            z3.MkGt((RealExpr)inputState, zero),
                            z3.MkLt((RealExpr)inputState, zero),
                            z3.MkEq((RealExpr)inputState, zero)
                        };
                        List<BoolExpr> satCases = new List<BoolExpr>(axisCases.Where(cond => {
                            bool result;
                            solv.Push();
                            solv.Assert(cond);
                            Helpers.AssertAssumptions(solv, z3);
                            result = solv.Check() == Status.SATISFIABLE;
                            solv.Pop();
                            return result;
                        }));

                        solv.Dispose();

                        for (int i = 1; i < satCases.Count; ++i) {
                            SymexState st = state.Fork();
                            st.pathCondition.Add(satCases[i]);
                        }
                        state.pathCondition.Add(satCases[0]);
                    }
                } else if (method.FullName == "UnityEngine.Input.get_mousePosition") 
                {
                    MakeSymcallForInput(method, arguments, state, out Expr inputState, out int _, out bool firstCall);
                    state.MemoryWrite(resultVar.address, inputState);
                    if (firstCall)
                    {
                        DatatypeSort dsort = (DatatypeSort)inputState.Sort;
                        IType vectorType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName(dsort.Name.ToString().Split(",")[0]));
                        FuncDecl vecX = Helpers.FindFieldAccessor(dsort, vectorType.GetFields(fld => fld.Name == "x").First());
                        FuncDecl vecY = Helpers.FindFieldAccessor(dsort, vectorType.GetFields(fld => fld.Name == "y").First());
                        RealExpr mouseX = (RealExpr)vecX.Apply(inputState).Simplify(); 
                        RealExpr mouseY = (RealExpr)vecY.Apply(inputState).Simplify();
                        const int gridWidth = 4;
                        const int gridHeight = 4;
                        List<BoolExpr> satCases = new List<BoolExpr>();

                        Solver solv = z3.MkSolver();
                        foreach (BoolExpr cond in state.pathCondition) {
                            solv.Assert(cond);
                        }

                        for (int i = 0; i < gridHeight; ++i) {
                            for (int j = 0; j < gridWidth; ++j) {
                                int stateIndex = i*gridWidth + j;
                                var minX = z3.MkReal(i, gridWidth);
                                var maxX = z3.MkReal(i+1, gridWidth);
                                var minY = z3.MkReal(j, gridHeight);
                                var maxY = z3.MkReal(j+1, gridHeight);
                                BoolExpr cellCase = z3.MkAnd(
                                    z3.MkGe(mouseX, minX),
                                    z3.MkLt(mouseX, maxX),
                                    z3.MkGe(mouseY, minY),
                                    z3.MkLt(mouseY, maxY));
                                solv.Push();
                                solv.Assert(cellCase);
                                Helpers.AssertAssumptions(solv, z3);
                                if (solv.Check() == Status.SATISFIABLE)
                                {
                                    satCases.Add(cellCase);
                                }
                                solv.Pop();
                            }
                        }

                        solv.Dispose();

                        for (int i = 1; i < satCases.Count; ++i) {
                            SymexState st = state.Fork();
                            st.pathCondition.Add(satCases[i]);
                        }
                        state.pathCondition.Add(satCases[0]);
                    }
                } else
                {
                    /* button and key inputs */
                    IType inputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityEngine.Input"));
                    IType instrInputType = SymexMachine.Instance.CSD.TypeSystem.FindType(new FullTypeName("UnityActionAnalysis.InstrInput"));
                    IMethod inputStateMethod;
                    IMethod instrInputStateMethod;
                    bool isDownCheck = false;
                    bool isUpCheck = false;
                    switch (method.FullName)
                    {
                        case "UnityEngine.Input.GetButton":
                        case "UnityEngine.Input.GetButtonDown":
                        case "UnityEngine.Input.GetButtonUp":
                            {
                                inputStateMethod = inputType.GetMethods(m => m.Name == "GetButton").First();
                                instrInputStateMethod = instrInputType.GetMethods(m => m.Name == "GetButton").First();
                                switch (method.Name) {
                                    case "GetButtonDown":
                                        isDownCheck = true;
                                        break;
                                    case "GetButtonUp":
                                        isUpCheck = true;
                                        break;
                                }
                                break;
                            }
                        case "UnityEngine.Input.GetKey":
                        case "UnityEngine.Input.GetKeyDown":
                        case "UnityEngine.Input.GetKeyUp":
                            {
                                IType keyParamType = method.Parameters[0].Type;
                                inputStateMethod = inputType.GetMethods(m => m.Name == "GetKey" && m.Parameters[0].Type.Equals(keyParamType)).First();
                                instrInputStateMethod = instrInputType.GetMethods(m => m.Name == "GetKey" && m.Parameters[0].Type.Equals(keyParamType)).First();
                                switch (method.Name) {
                                    case "GetKeyDown":
                                        isDownCheck = true;
                                        break;
                                    case "GetKeyUp":
                                        isUpCheck = true;
                                        break;
                                }
                                break;
                            }
                        case "UnityEngine.Input.GetMouseButton":
                        case "UnityEngine.Input.GetMouseButtonDown":
                        case "UnityEngine.Input.GetMouseButtonUp":
                            {
                                inputStateMethod = inputType.GetMethods(m => m.Name == "GetMouseButton").First();
                                instrInputStateMethod = instrInputType.GetMethods(m => m.Name == "GetMouseButton").First();
                                switch (method.Name)
                                {
                                    case "GetMouseButtonDown":
                                        isDownCheck = true;
                                        break;
                                    case "GetMouseButtonUp":
                                        isUpCheck = true;
                                        break;
                                }
                                break;
                            }
                        default:
                            throw new SymexUnsupportedException("Unsupported input API " + method.FullName);
                    }
                    Expr inputState;
                    Expr prevInputState;
                    bool firstCall;
                    MakeSymcallForInput(inputStateMethod, arguments, state, out inputState, out int _, out firstCall);
                    MakeSymcallForInput(instrInputStateMethod, arguments, state, out prevInputState, out int _, out bool _);
                    BitVecSort inputStateSort = (BitVecSort)inputState.Sort;
                    if (isDownCheck || isUpCheck) {
                        BoolExpr upCondPrevInput = z3.MkNot(z3.MkEq(prevInputState, z3.MkBV(0, inputStateSort.Size)));
                        BoolExpr upCondInput = z3.MkEq(inputState, z3.MkBV(0, inputStateSort.Size));
                        BoolExpr downCondPrevInput = z3.MkEq(prevInputState, z3.MkBV(0, inputStateSort.Size));
                        BoolExpr downCondInput = z3.MkNot(z3.MkEq(inputState, z3.MkBV(0, inputStateSort.Size)));

                        BoolExpr invOpCondPrevInput;
                        BoolExpr invOpCondInput;
                        BoolExpr opCondPrevInput;
                        BoolExpr opCondInput;

                        if (isDownCheck) {
                            invOpCondPrevInput = upCondPrevInput;
                            invOpCondInput = upCondInput;
                            opCondPrevInput = downCondPrevInput;
                            opCondInput = downCondInput;
                        } else { /* isUpCheck */
                            invOpCondPrevInput = downCondPrevInput;
                            invOpCondInput = downCondInput;
                            opCondPrevInput = upCondPrevInput;
                            opCondInput = upCondInput;
                        }

                        Solver solv = z3.MkSolver();
                        foreach (BoolExpr cond in state.pathCondition) {
                            solv.Assert(cond);
                        }

                        solv.Push();
                        solv.Assert(invOpCondPrevInput);
                        solv.Assert(invOpCondInput);
                        Helpers.AssertAssumptions(solv, z3);
                        if (solv.Check() == Status.SATISFIABLE)
                        {
                            SymexState invOpState = state.Fork();
                            invOpState.pathCondition.Add(invOpCondPrevInput);
                            invOpState.pathCondition.Add(invOpCondInput);
                            invOpState.execStatus = ExecutionStatus.ABORTED;
                        }
                        solv.Pop();

                        List<Action<SymexState>> satCases = new List<Action<SymexState>>();

                        solv.Push();
                        {
                            BoolExpr falseCond = z3.MkNot(z3.MkAnd(opCondPrevInput, opCondInput));
                            solv.Assert(falseCond);
                            Helpers.AssertAssumptions(solv, z3);
                            if (solv.Check() == Status.SATISFIABLE)
                            {
                                satCases.Add(st => {
                                    st.pathCondition.Add(falseCond);
                                    st.MemoryWrite(resultVar.address, z3.MkBV(0, inputStateSort.Size));
                                });
                            }
                        }
                        solv.Pop();

                        solv.Push();
                        {
                            solv.Assert(opCondPrevInput);
                            solv.Assert(opCondInput);
                            Helpers.AssertAssumptions(solv, z3);
                            if (solv.Check() == Status.SATISFIABLE)
                            {
                                satCases.Add(st => {
                                    st.pathCondition.Add(opCondPrevInput);
                                    st.pathCondition.Add(opCondInput);
                                    st.MemoryWrite(resultVar.address, z3.MkBV(1, inputStateSort.Size));
                                });
                            }
                        }
                        solv.Pop();
                        solv.Dispose();
                        for (int i = 1; i < satCases.Count; ++i) {
                            satCases[i](state.Fork());
                        }
                        satCases[0](state);
                    } else {
                        state.MemoryWrite(resultVar.address, inputState);

                        if (firstCall) {
                            List<BoolExpr> inputCases = new List<BoolExpr>() {
                                z3.MkEq(inputState, z3.MkBV(0, inputStateSort.Size)),
                                z3.MkNot(z3.MkEq(inputState, z3.MkBV(0, inputStateSort.Size)))
                            };

                            Solver solv = z3.MkSolver();
                            foreach (BoolExpr cond in state.pathCondition)
                            {
                                solv.Assert(cond);
                            }

                            List<BoolExpr> satCases = new List<BoolExpr>(inputCases.Where(cond => {
                                bool result;
                                solv.Push();
                                solv.Assert(cond);
                                Helpers.AssertAssumptions(solv, z3);
                                result = solv.Check() == Status.SATISFIABLE;
                                solv.Pop();
                                return result;
                            }));

                            solv.Dispose();

                            for (int i = 1; i < satCases.Count; ++i) {
                                SymexState st = state.Fork();
                                st.pathCondition.Add(satCases[i]);
                            }
                            state.pathCondition.Add(satCases[0]);
                        }
                    }
                }
            } else
            {
                base.ApplyMethodSummary(method, arguments, resultVar, state);
            }
        }

        public static bool IsInputAPI(IMethod method)
        {
            return method.DeclaringType.FullName == "UnityEngine.Input" && method.ReturnType.Kind != TypeKind.Void;
        }

        public override bool ShouldAbortBranchCase(BranchCase branchCase, ILInstruction branchInst, SymexState state)
        {
            IMethod method = branchCase.IP.GetCurrentMethod();
            string methodSig = AnalysisHelpers.MethodSignature(method);
            LeadsToInputAnalysisMethodResult ltMethodResult = ltResult.methodResults[methodSig];
            if (ltMethodResult.leadsToInputPoints.Contains(branchInst))
            {
                return false;
            }
            if (state.frameStack.Count > 0)
            {
                foreach (FrameStackElement fse in state.frameStack)
                {
                    var callInst = Helpers.FindEnclosingStatement(fse.opQueue.Peek().Instruction);
                    var m = Helpers.GetInstructionFunction(callInst).Method;
                    string sig = AnalysisHelpers.MethodSignature(m);
                    var mRes = ltResult.methodResults[sig];
                    if (mRes.leadsToInputPoints.Contains(callInst))
                    {
                        return false;
                    }
                }
            }
            ILInstruction target = branchCase.IP.GetInstruction();
            return !ltMethodResult.leadsToInputPoints.Contains(target);
        }

        public override object NewStateCustomData()
        {
            return new ConfigData();
        }

        public override object CloneStateCustomData(object data)
        {
            return new ConfigData((ConfigData)data);
        }
    }
}

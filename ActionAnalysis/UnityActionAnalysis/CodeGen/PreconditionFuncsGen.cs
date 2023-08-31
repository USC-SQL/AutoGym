using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.CodeDom;
using System.CodeDom.Compiler;
using ICSharpCode.Decompiler.TypeSystem;
using Microsoft.Z3;

namespace UnityActionAnalysis
{
    public class PreconditionFuncsGen
    {
        private CodeCompileUnit pfuncsUnit;
        private CodeTypeDeclaration pfuncsClass;

        private Dictionary<string, string> fieldAccessors;
        private List<CodeMemberField> fieldAccessorFields;
        private List<CodeStatement> fieldAccessorAssignStmts;

        private Dictionary<string, string> methodAccessors;
        private List<CodeMemberField> methodAccessorFields;
        private List<CodeStatement> methodAccessorAssignStmts;

        private List<CodeMemberMethod> pathMethods;
        private List<CodeStatement> pathFuncAssignStmts;

        public PreconditionFuncsGen()
        {
            pfuncsUnit = new CodeCompileUnit();
            CodeNamespace ns = new CodeNamespace("UnityActionAnalysis");
            ns.Imports.Add(new CodeNamespaceImport("System"));
            ns.Imports.Add(new CodeNamespaceImport("System.Reflection"));
            ns.Imports.Add(new CodeNamespaceImport("System.Collections.Generic"));
            ns.Imports.Add(new CodeNamespaceImport("UnityEngine"));

            pfuncsClass = new CodeTypeDeclaration("PreconditionFuncs");
            pfuncsClass.BaseTypes.Add(new CodeTypeReference("IPreconditionFuncs"));
            pfuncsClass.IsClass = true;
            pfuncsClass.TypeAttributes = TypeAttributes.Public;
            ns.Types.Add(pfuncsClass);
            pfuncsUnit.Namespaces.Add(ns);

            fieldAccessors = new Dictionary<string, string>();
            fieldAccessorFields = new List<CodeMemberField>();
            fieldAccessorAssignStmts = new List<CodeStatement>();
            methodAccessors = new Dictionary<string, string>();
            methodAccessorFields = new List<CodeMemberField>();
            methodAccessorAssignStmts = new List<CodeStatement>();
            pathMethods = new List<CodeMemberMethod>();
            pathFuncAssignStmts = new List<CodeStatement>();
        }

        private CodeTypeReference MakeCodeTypeReference(IType type)
        {
            CodeTypeReference typeRef = new CodeTypeReference(type.FullName);
            foreach (IType arg in type.TypeArguments)
            {
                typeRef.TypeArguments.Add(MakeCodeTypeReference(arg));
            }
            return typeRef;
        }

        private CodeExpression FieldAccess(CodeExpression targetObject, IField field)
        {
            string accessor;
            if (!fieldAccessors.TryGetValue(field.FullName, out accessor))
            {
                accessor = "f_" + field.FullName.Replace(".", "_");

                CodeMemberField accessorField = new CodeMemberField();
                accessorField.Attributes = MemberAttributes.Private;
                accessorField.Name = accessor;
                accessorField.Type = new CodeTypeReference(typeof(FieldInfo));
                fieldAccessorFields.Add(accessorField);



                CodeStatement assignStmt =
                    new CodeAssignStatement(
                        new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), accessor),
                        new CodeMethodInvokeExpression(new CodeTypeOfExpression(MakeCodeTypeReference(field.DeclaringType)), "GetField",
                            new CodePrimitiveExpression(field.Name), new CodeVariableReferenceExpression("bindingFlags")));

                fieldAccessors.Add(field.FullName, accessor);
                fieldAccessorAssignStmts.Add(assignStmt);
            }

            return new CodeMethodInvokeExpression(
                new CodeTypeReferenceExpression("CompileHelpers"),
                "GetFieldValue",
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), accessor),
                targetObject);
        }

        private CodeExpression MethodInvoke(IMethod method, CodeExpression targetObject, params CodeExpression[] args)
        {
            string accessor;
            string sig = Helpers.GetMethodSignature(method);
            if (!methodAccessors.TryGetValue(sig, out accessor))
            {
                int i = 0;
                do
                {
                    accessor = "m_" + method.FullName.Replace(".", "_") + (i > 0 ? "_" + i : "");
                    ++i;
                } while (methodAccessors.ContainsValue(accessor));

                CodeMemberField accessorField = new CodeMemberField();
                accessorField.Attributes = MemberAttributes.Private;
                accessorField.Name = accessor;
                accessorField.Type = new CodeTypeReference(typeof(MethodInfo));
                methodAccessorFields.Add(accessorField);

                CodeExpression[] paramTypes = new CodeExpression[method.Parameters.Count];
                for (int idx = 0, n = method.Parameters.Count; idx < n; ++idx)
                {
                    paramTypes[idx] = new CodeTypeOfExpression(method.Parameters[idx].Type.FullName);
                }

                CodeTypeOfExpression typeOfExpression = new CodeTypeOfExpression(MakeCodeTypeReference(method.DeclaringType));
                CodeStatement assignStmt =
                    new CodeAssignStatement(
                        new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), accessor),
                        new CodeMethodInvokeExpression(typeOfExpression, "GetMethod",
                            new CodePrimitiveExpression(method.Name), new CodeVariableReferenceExpression("bindingFlags"), 
                            new CodePrimitiveExpression(null), 
                            new CodeArrayCreateExpression(new CodeTypeReference(typeof(Type)), paramTypes),
                            new CodePrimitiveExpression(null)));

                methodAccessors.Add(sig, accessor);
                methodAccessorAssignStmts.Add(assignStmt);
            }

            CodeExpression[] coercedArgs = new CodeExpression[args.Length];
            for (int i = 0, n = args.Length; i < n; ++i)
            {
                IType paramType = method.Parameters[i].Type;
                coercedArgs[i] = new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ChangeType", args[i], 
                    new CodeTypeOfExpression(paramType.FullName));
            }

            return new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), accessor), "Invoke", 
                targetObject, new CodeArrayCreateExpression(new CodeTypeReference(typeof(object)), coercedArgs));
        }

        private CodeExpression ResolveSymcall(SymbolicMethodCall smc, IMethod m, SymexState s)
        {
            List<CodeExpression> args = new List<CodeExpression>();
            for (int i = 0, n = smc.args.Count; i < n; ++i)
            {
                args.Add(Compile(smc.args[i], m, s));
            }
            if (smc.method.IsStatic)
            {
                CodeExpression[] argVals = new CodeExpression[smc.args.Count];
                for (int i = 0; i < args.Count; ++i)
                {
                    argVals[i] = args[i];
                }
                return MethodInvoke(smc.method, new CodePrimitiveExpression(null), argVals);
            } else
            {
                CodeExpression thisVal = args[0];
                CodeExpression[] argVals = new CodeExpression[args.Count - 1];
                for (int i = 1; i < args.Count; ++i)
                {
                    argVals[i - 1] = args[i];
                }
                return MethodInvoke(smc.method, thisVal, argVals);
            }
        }

        private CodeExpression ResolveVariable(string varName, IMethod m, SymexState s)
        {
            bool staticField;
            CodeExpression value;
            IType valueType;
            string[] parts = varName.Split(':');
            int idx;
            if ((staticField = varName.StartsWith("staticfield:")) || varName.StartsWith("frame:0:this:instancefield:"))
            {
                IField f;
                if (staticField)
                {
                    string field = parts[1];
                    int dotIndex = field.LastIndexOf('.');
                    string typeName = field.Substring(0, dotIndex);
                    string fieldName = field.Substring(dotIndex + 1);
                    IType type = Helpers.FindType(SymexMachine.Instance.CSD, typeName);
                    f = type.GetFields(fld => fld.Name == fieldName).First();
                    idx = 2;
                } else
                {
                    string fieldName = parts[4];
                    IType instanceType = m.DeclaringType;
                    f = instanceType.GetFields(fld => fld.Name == fieldName).First();
                    idx = 5;
                }
                value = FieldAccess(new CodeVariableReferenceExpression("instance"), f);
                valueType = f.Type;
            } else if (varName.StartsWith("symcall:"))
            {
                int symcallId = int.Parse(parts[1]);
                idx = 2;
                var smc = s.symbolicMethodCalls[symcallId];
                value = ResolveSymcall(smc, m, s);
                valueType = smc.method.ReturnType;
            } else if (varName.StartsWith("special:")) 
            {
                switch (varName)
                {
                    case "special:instancemouseboundsminx":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseBoundsMinX", new CodeVariableReferenceExpression("instance"));
                    case "special:instancemouseboundsmaxx":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseBoundsMaxX", new CodeVariableReferenceExpression("instance"));
                    case "special:instancemouseboundsminy":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseBoundsMinY", new CodeVariableReferenceExpression("instance"));
                    case "special:instancemouseboundsmaxy":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseBoundsMaxY", new CodeVariableReferenceExpression("instance"));
                    case "special:instancemousedidenter":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseDidEnter", new CodeVariableReferenceExpression("instance"));
                    case "special:instancemousewasdown":
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "InstanceMouseWasDown", new CodeVariableReferenceExpression("instance"));
                    default:
                        throw new ResolutionException("unexpected special variable " + varName, varName);
                }
            } else
            {
                throw new ResolutionException("cannot resolve variable " + varName, varName);
            }

            while (idx < parts.Length)
            {
                if (parts[idx] == "instancefield")
                {
                    string fieldName = parts[idx + 1];
                    IField f = valueType.GetFields(fld => fld.Name == fieldName).First();
                    value = FieldAccess(value, f);
                    valueType = f.Type;
                    idx += 2;
                } else if (parts[idx] == "length") 
                {
                    value = new CodeMethodInvokeExpression(
                        new CodeTypeReferenceExpression("CompileHelpers"),
                        "GetArrayLength",
                        value);
                    idx += 2;
                } else if (parts[idx] == "heapid")
                {
                    throw new ResolutionException("resolving heap-allocated objects not supported", varName);
                }
                else
                {
                    throw new Exception("expected instancefield, got " + parts[idx] + " (for " + varName + ")");
                }
            }

            return value;
        }

        private CodeExpression Compile(Expr expr, IMethod m, SymexState s)
        {
            switch (expr.FuncDecl.DeclKind)
            {
                case Z3_decl_kind.Z3_OP_NOT:
                    {
                        return new CodeBinaryOperatorExpression(
                            Compile(expr.Arg(0), m, s), CodeBinaryOperatorType.IdentityEquality, new CodePrimitiveExpression(false));
                    }
                case Z3_decl_kind.Z3_OP_AND:
                    {
                        if (expr.NumArgs > 2)
                        {
                            throw new ResolutionException("more than 2 args not supported in and expression", expr.ToString());
                        }
                        return new CodeBinaryOperatorExpression(
                            Compile(expr.Arg(0), m, s), CodeBinaryOperatorType.BooleanAnd, Compile(expr.Arg(1), m, s));
                    }
                case Z3_decl_kind.Z3_OP_OR:
                    {
                        if (expr.NumArgs > 2)
                        {
                            throw new ResolutionException("more than 2 args not supported in or expression", expr.ToString());
                        }
                        return new CodeBinaryOperatorExpression(
                            Compile(expr.Arg(0), m, s), CodeBinaryOperatorType.BooleanOr, Compile(expr.Arg(1), m, s));
                    }
                case Z3_decl_kind.Z3_OP_EQ:
                    {
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "Equals", Compile(expr.Arg(0), m, s), Compile(expr.Arg(1), m, s));
                    }
                case Z3_decl_kind.Z3_OP_ITE:
                    {
                        var cond = Compile(expr.Arg(0), m, s);
                        var trueVal = Compile(expr.Arg(1), m, s);
                        var falseVal = Compile(expr.Arg(2), m, s);
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "IfThenElse", cond, trueVal, falseVal);
                    }
                case Z3_decl_kind.Z3_OP_GT:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.GreaterThan,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_GE:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.GreaterThanOrEqual,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_LT:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.LessThan,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_LE:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.LessThanOrEqual,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_BUREM:
                case Z3_decl_kind.Z3_OP_BSREM:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.Modulus,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BAND:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.BitwiseAnd,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BOR:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.BitwiseOr,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BXOR:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "Xor",
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BSHL:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "Shl",
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BASHR:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeMethodInvokeExpression(
                            new CodeTypeReferenceExpression("CompileHelpers"), "Shr",
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_BADD:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.Add,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_ULT:
                case Z3_decl_kind.Z3_OP_SLT:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.LessThan,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_ULEQ:
                case Z3_decl_kind.Z3_OP_SLEQ:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.LessThanOrEqual,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_UGT:
                case Z3_decl_kind.Z3_OP_SGT:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.GreaterThan,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_UGEQ:
                case Z3_decl_kind.Z3_OP_SGEQ:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.GreaterThanOrEqual,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_ADD:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.Add,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_BSUB:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.Subtract,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_SUB:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.Subtract,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_BMUL:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.Multiply,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_MUL:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.Multiply,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_BUDIV:
                case Z3_decl_kind.Z3_OP_BSDIV:
                    {
                        var val1 = Compile(expr.Arg(0), m, s);
                        var val2 = Compile(expr.Arg(1), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val1),
                            CodeBinaryOperatorType.Divide,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val2));
                    }
                case Z3_decl_kind.Z3_OP_DIV:
                    {
                        var val1 = Compile(expr.Arg(expr.NumArgs == 3 ? 1u : 0u), m, s);
                        var val2 = Compile(expr.Arg(expr.NumArgs == 3 ? 2u : 1u), m, s);
                        return new CodeBinaryOperatorExpression(
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val1),
                            CodeBinaryOperatorType.Divide,
                            new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToDouble", val2));
                    }
                case Z3_decl_kind.Z3_OP_CONCAT:
                    {
                        // concat only used to extend a value with 0s, so ignore the first argument
                        var val = Compile(expr.Arg(1), m, s);
                        return new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val);
                    }
                case Z3_decl_kind.Z3_OP_EXTRACT:
                    {
                        var val = Compile(expr.Arg(0), m, s);
                        return new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("CompileHelpers"), "ToUlong", val);
                    }
                case Z3_decl_kind.Z3_OP_BNUM:
                    {
                        var val = ulong.Parse(expr.ToString());
                        return new CodePrimitiveExpression(val);
                    }
                case Z3_decl_kind.Z3_OP_TRUE:
                    {
                        return new CodePrimitiveExpression(true);
                    }
                case Z3_decl_kind.Z3_OP_FALSE:
                    {
                        return new CodePrimitiveExpression(false);
                    }
                case Z3_decl_kind.Z3_OP_UNINTERPRETED:
                    {
                        return ResolveVariable(expr.FuncDecl.Name.ToString(), m, s);
                    }
                case Z3_decl_kind.Z3_OP_ANUM:
                case Z3_decl_kind.Z3_OP_AGNUM:
                    if (expr.IsReal) 
                    {
                        var strExpr = expr.ToString();
                        int divIndex = strExpr.IndexOf("/");
                        if (divIndex >= 0)
                        {
                            double numer = double.Parse(strExpr.Substring(0, divIndex));
                            double denom = double.Parse(strExpr.Substring(divIndex+1));
                            return new CodeBinaryOperatorExpression(
                                new CodePrimitiveExpression(numer),
                                CodeBinaryOperatorType.Divide,
                                new CodePrimitiveExpression(denom));
                        } else
                        {
                            return new CodePrimitiveExpression(double.Parse(strExpr));
                        }
                    } else 
                    {
                        Reference r = Reference.FromExpr(expr);
                        if (r.address == null)
                        {
                            return new CodePrimitiveExpression(null);
                        } else if (r.address.heap && r.address.components.Count == 0)
                        {
                            HeapObject obj = s.objects[r.address.root];
                            if (obj.value.TryGetValue("_string", out Expr strExpr))
                            {
                                var str = strExpr.ToString();
                                str = str.Substring(1, str.Length-2);
                                return new CodePrimitiveExpression(str);
                            } else 
                            {
                                throw new ResolutionException("resolution of objects unsupported", expr.ToString());    
                            }
                        } else
                        {
                            throw new ResolutionException("resolution of objects unsupported", expr.ToString());
                        }
                    }
                default:
                    throw new ResolutionException("unsupported expression of kind " + expr.FuncDecl.DeclKind, expr.ToString());
            }
        }

        private CodeExpression And(List<CodeExpression> predicates)
        {
            if (predicates.Count == 0)
            {
                return new CodePrimitiveExpression(true);
            } else
            {
                CodeExpression current = predicates[0];
                for (int i = 1; i < predicates.Count; ++i)
                {
                    current = new CodeBinaryOperatorExpression(current, CodeBinaryOperatorType.BooleanAnd, predicates[i]);
                }
                return current;
            }
        }

        public void ProcessMethod(IMethod method, SymexMachine m)
        {
            PrettyPrint pp = new PrettyPrint(m, m.Z3);
            pathFuncAssignStmts.Add(new CodeAssignStatement(
                   new CodeVariableReferenceExpression("pathFuncs"), new CodeObjectCreateExpression("List<Func<MonoBehaviour, bool>>")));
            int pathIndex = 1;
            ISet<string> warnings = new HashSet<string>();
            foreach (SymexState s in m.States)
            {
                List<BoolExpr> preconditions = new List<BoolExpr>();
                List<CodeExpression> predicates = new List<CodeExpression>();
                foreach (BoolExpr cond in s.pathCondition)
                {
                    if (Helpers.ContainsInputVariable(cond, s))
                    {
                        continue;
                    }
                    preconditions.Add(cond);

                    try
                    {
                        predicates.Add(Compile(cond, method, s));
                    } 
                    catch (ResolutionException e)
                    {
                        string warning = "warning: failed to compile '" + pp.ExprToPrettyString(cond, s) + "': " + e.Message;
                        warnings.Add(warning);
                    }
                }
                CodeExpression compiledPathCond = And(predicates);
                
                string methodName = method.FullName.Replace('.', '_') + "_Path" + pathIndex;
                CodeMemberMethod codeMethod = new CodeMemberMethod();
                codeMethod.Attributes = MemberAttributes.Public | MemberAttributes.Final;
                codeMethod.Parameters.Add(new CodeParameterDeclarationExpression(new CodeTypeReference("MonoBehaviour"), "instance"));
                codeMethod.Name = methodName;
                codeMethod.ReturnType = new CodeTypeReference(typeof(bool));
                codeMethod.Statements.Add(new CodeMethodReturnStatement(compiledPathCond));
                StringWriter sw = new StringWriter();
                sw.Write("Path ID: " + ((ConfigData)(s.customData)).pathId + "\n\n");
                pp.WritePrettyPathCondition(s, sw);
                codeMethod.Comments.Add(new CodeCommentStatement(sw.ToString()));
                pathMethods.Add(codeMethod);

                pathFuncAssignStmts.Add(new CodeExpressionStatement(
                    new CodeMethodInvokeExpression(new CodeVariableReferenceExpression("pathFuncs"), "Add",
                        new CodeMethodReferenceExpression(new CodeThisReferenceExpression(), methodName))));

                ++pathIndex;
            }
            if (warnings.Count > 0)
            {
                Console.WriteLine("\t" + warnings.Count + " warnings encountered during compilation:");
                Console.WriteLine(string.Join("\n", warnings.Select(warn => "\t" + warn)));
            }

            pathFuncAssignStmts.Add(new CodeExpressionStatement(new CodeMethodInvokeExpression(
                new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "preconditions"),
                "Add",
                new CodeMethodInvokeExpression(new CodeTypeReferenceExpression("SymexHelpers"),
                    "GetMethodFromSignature", new CodePrimitiveExpression(Helpers.GetMethodSignature(method))),
                new CodeVariableReferenceExpression("pathFuncs"))));
        }

        public void Finish()
        {
            foreach (CodeMemberField fieldAccessorField in fieldAccessorFields)
            {
                pfuncsClass.Members.Add(fieldAccessorField);
            }
            foreach (CodeMemberField methodAccessorField in methodAccessorFields)
            {
                pfuncsClass.Members.Add(methodAccessorField);
            }

            CodeMemberField preconditionsField = new CodeMemberField();
            preconditionsField.Attributes = MemberAttributes.Private;
            preconditionsField.Name = "preconditions";
            preconditionsField.Type = new CodeTypeReference("Dictionary<MethodInfo, List<Func<MonoBehaviour, bool>>>");

            CodeMemberProperty preconditions = new CodeMemberProperty();
            preconditions.Attributes = MemberAttributes.Public | MemberAttributes.Final;
            preconditions.Name = "Preconditions";
            preconditions.Type = preconditionsField.Type;
            preconditions.HasGet = true;
            preconditions.GetStatements.Add(new CodeMethodReturnStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "preconditions")));

            pfuncsClass.Members.Add(preconditionsField);
            pfuncsClass.Members.Add(preconditions);

            CodeConstructor ctor = new CodeConstructor();
            ctor.Attributes = MemberAttributes.Public;
            ctor.Statements.Add(new CodeVariableDeclarationStatement(new CodeTypeReference("BindingFlags"), "bindingFlags",
                new CodeBinaryOperatorExpression(
                    new CodeBinaryOperatorExpression(
                        new CodeBinaryOperatorExpression(
                            new CodeFieldReferenceExpression(new CodeTypeReferenceExpression("BindingFlags"), "Public"), CodeBinaryOperatorType.BitwiseOr,
                            new CodeFieldReferenceExpression(new CodeTypeReferenceExpression("BindingFlags"), "NonPublic")),
                        CodeBinaryOperatorType.BitwiseOr,
                        new CodeFieldReferenceExpression(new CodeTypeReferenceExpression("BindingFlags"), "Instance")),
                    CodeBinaryOperatorType.BitwiseOr,
                    new CodeFieldReferenceExpression(new CodeTypeReferenceExpression("BindingFlags"), "Static"))));
            foreach (CodeStatement assignStmt in fieldAccessorAssignStmts)
            {
                ctor.Statements.Add(assignStmt);
            }
            foreach (CodeStatement assignStmt in methodAccessorAssignStmts)
            {
                ctor.Statements.Add(assignStmt);
            }
            ctor.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(new CodeThisReferenceExpression(), "preconditions"),
                new CodeObjectCreateExpression(preconditionsField.Type)));
            ctor.Statements.Add(new CodeVariableDeclarationStatement(new CodeTypeReference("List<Func<MonoBehaviour, bool>>"), "pathFuncs"));
            foreach (CodeStatement assignStmt in pathFuncAssignStmts)
            {
                ctor.Statements.Add(assignStmt);
            }
            pfuncsClass.Members.Add(ctor);

            foreach (CodeMemberMethod pathMethod in pathMethods)
            {
                pfuncsClass.Members.Add(pathMethod);
            }
        }

        public void Write(StreamWriter sw)
        {
            CodeDomProvider provider = CodeDomProvider.CreateProvider("CSharp");
            CodeGeneratorOptions options = new CodeGeneratorOptions();
            options.BracingStyle = "C";
            provider.GenerateCodeFromCompileUnit(pfuncsUnit, sw, options);
        }
    }
}

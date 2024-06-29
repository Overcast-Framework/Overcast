using LLVMSharp.Interop;
using Overcast.CodeAnalysis.LLVMC.Data;
using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Parsing.Expressions;
using Overcast.CodeAnalysis.Parsing.Statements;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static Overcast.OCHelper;

namespace Overcast.CodeAnalysis.LLVMC
{
    public unsafe class IREmitter
    {
        // LLVM-Specific Data and Variables
        private LLVMOpaqueModule* _Module;
        private LLVMOpaqueBuilder* _Builder;
        private LLVMOpaqueValue* _PrintFunc;
        private LLVMOpaqueType* _PrintFuncType;
        private LLVMOpaqueTargetMachine* TargetMachine;

        // Additional Variables
        private bool Initialized = false;

        // Dictionaries and Data Tables
        private Dictionary<string, StructData> StructTable = new Dictionary<string, StructData>();
        private Dictionary<string, VariableData> VariableTable = new Dictionary<string, VariableData>();
        private Dictionary<string, FunctionData> FunctionTable = new Dictionary<string, FunctionData>();
        private Dictionary<string, Pointer<LLVMOpaqueValue>> VarStructAllocaTable = new Dictionary<string, Pointer<LLVMOpaqueValue>>();

        // Stacks
        private Stack<Pointer<LLVMOpaqueValue>> ValueStack = new Stack<Pointer<LLVMOpaqueValue>>();
        private Stack<Pointer<LLVMOpaqueValue>> StructStack = new Stack<Pointer<LLVMOpaqueValue>>(); // stores struct alloca values.

        // Visit Functions(statements & expressions)

        public unsafe object? Visit(Statement statement) => statement switch
        {
            FunctionDeclarationStatement fnDeclStmt => this.Visit(fnDeclStmt),
            VariableDeclarationStatement varDeclStmt => this.Visit(varDeclStmt),
            StructDeclarationStatement structDeclarationStatement => this.Visit(structDeclarationStatement),
            VariableSetStatement varSetStmt => this.Visit(varSetStmt),
            IfStatement ifStatement => this.Visit(ifStatement),
            ExpressionStatement exprStmt => this.Visit(exprStmt.Expr),
            ReturnStatement retStmt => this.Visit(retStmt),
            StructMemberSetStatement structMemberSetStatement => this.Visit(structMemberSetStatement),
            _ => throw new IREmitterException("Statement unsupported.")
        };

        public unsafe object? Visit(Expression expression) => expression switch
        {
            StringLiteralExpr strExpr => this.Visit(strExpr),
            IntLiteralExpr intExpr => this.Visit(intExpr),
            InvokeFunctionExpr invExpr => this.Visit(invExpr),
            VariableExpr varExpr => this.Visit(varExpr),
            BinaryExpression binaryExpr => this.Visit(binaryExpr),
            StructObjCreationExpr structObjCreationExpr => this.Visit(structObjCreationExpr),
            StructMemberAccessExpr structMemberAccessExpr => this.Visit(structMemberAccessExpr),
            ReferenceExpr refExpr => this.Visit(refExpr),
            _ => throw new Exception($"Expression unsupported. ({expression.GetType()})")
        };

        // Util Functions

        public LLVMOpaqueType* GetLLVMType(OCType type)
        {
            if (type is IdentifierType identifierType)
            {
                try
                {
                    return identifierType.LLVMType();
                }
                catch (ParserException e)
                {
                    if (StructTable.ContainsKey(identifierType.Name))
                    {
                        return StructTable[identifierType.Name].StructLLVMType;
                    }
                    throw new IREmitterException("Unknown type when getting LLVM type");
                }
            }
            else if (type is Overcast.CodeAnalysis.Parsing.PointerType pointerType)
            {
                return LLVM.PointerType(GetLLVMType(pointerType.OfType), 0);
            }
            else
            {
                throw new IREmitterException("Unsupported type when getting LLVM type");
            }
        }

        public LLVMOpaqueValue* GetStructAllocaVal(Expression expr) // oh boy.....
        {
            if(expr is StructObjCreationExpr)
            {
                Visit(expr);
                return StructStack.Pop();
            }
            else if(expr is InvokeFunctionExpr)
            {
                var invFnEx = (InvokeFunctionExpr)expr;
                if (FunctionTable.ContainsKey(invFnEx.FunctionName))
                {
                    if (StructTable.ContainsKey(FunctionTable[invFnEx.FunctionName].ReturnTypeOC.GetBaseType().Name))
                    {
                        Visit(expr);
                        return StructStack.Pop();
                    }
                }
            }
            else if(expr is VariableExpr)
            {
                return VarStructAllocaTable[(expr as VariableExpr).Name];
            }

            return null;
        }

        // Statement Visit function implementations

        public unsafe Pointer<LLVMOpaqueValue> Visit(FunctionDeclarationStatement statement)
        {
            var retType = GetLLVMType(statement.ReturnType);
            var llvmParamList = new LLVMOpaqueType*[statement.Parameters.Count];

            for (int i = 0; i < llvmParamList.Length; i++)
            {
                var param = statement.Parameters[i];
                var paramType = param.Type.LLVMType();
                llvmParamList[i] = paramType;
            }

            fixed (LLVMOpaqueType** pParamTypes = llvmParamList)
            {
                var funcType = LLVM.FunctionType(retType, pParamTypes, (uint)llvmParamList.Length, 0);
                var directFunc = LLVM.AddFunction(_Module, StrToSByte(statement.Name), funcType);

                var fnData = new FunctionData(statement.Name, statement.Parameters, retType, funcType, directFunc);
                fnData.ReturnTypeOC = statement.ReturnType;
                FunctionTable.Add(statement.Name, fnData);

                LLVM.PositionBuilderAtEnd(_Builder, LLVM.AppendBasicBlock(directFunc, StrToSByte("entry")));

                for (int i = 0; i < llvmParamList.Length; i++)
                {
                    var param = statement.Parameters[i];
                    var paramTy = param.Type.LLVMType();
                    var paramVal = LLVM.GetParam(directFunc, (uint)i);

                    var alloca = LLVM.BuildAlloca(_Builder, paramTy, StrToSByte(param.Name));
                    LLVM.BuildStore(_Builder, paramVal, alloca);

                    VariableTable.Add(param.Name, new VariableData(param.Name, paramTy, alloca));
                }

                foreach (var innerStmt in statement.Block.statements)
                {
                    this.Visit(innerStmt);
                }

                VariableTable.Clear();

                if (retType == LLVM.VoidType())
                {
                    LLVM.BuildRetVoid(_Builder);
                }

                LLVM.VerifyFunction(directFunc, LLVMVerifierFailureAction.LLVMPrintMessageAction);

                return directFunc;
            }

            throw new IREmitterException("Could not create function from declaration statement");
        }

        public unsafe object? Visit(StructDeclarationStatement stmt)
        {
            LLVMOpaqueType*[] structTypes = new LLVMOpaqueType*[stmt.Members.Count];

            for (int i = 0; i < structTypes.Length; i++)
                structTypes[i] = GetLLVMType(stmt.Members[i].Type);

            fixed (LLVMOpaqueType** pStrTypes = structTypes)
            {
                var cStructType = LLVM.StructType(pStrTypes, (uint)structTypes.Length, 0);
                StructTable.Add(stmt.Name, new StructData(stmt.Name, stmt.Members, cStructType));
                LLVM.AddGlobal(_Module, cStructType, StrToSByte(stmt.Name));
            }

            return stmt;
        }

        public unsafe object? Visit(ReturnStatement stmt)
        {
            Visit(stmt.Value);
            var retValue = ValueStack.Pop();

            LLVM.BuildRet(_Builder, retValue);

            return stmt;
        }

        public unsafe object? Visit(IfStatement stmt)
        {
            var function = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_Builder));

            Visit(stmt.conditandB);
            Visit(stmt.conditandA);

            var condA = ValueStack.Pop();
            var condB = ValueStack.Pop();

            LLVMOpaqueValue* condValue = null;

            switch (stmt._operator)
            {
                case ConditionOperator.GT:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntSGT, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.LT:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntSLT, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.GE:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntSGE, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.LE:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntSLE, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.EQEQ:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntEQ, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.NEQ:
                    condValue = LLVM.BuildICmp(_Builder, LLVMIntPredicate.LLVMIntNE, condA, condB, StrToSByte("cmp"));
                    break;
            }

            var thenBB = LLVM.AppendBasicBlock(function, StrToSByte("then"));
            var elseBB = LLVM.AppendBasicBlock(function, StrToSByte("else"));
            var mergeBB = LLVM.AppendBasicBlock(function, StrToSByte("ifcont"));

            LLVM.BuildCondBr(_Builder, condValue, thenBB, elseBB);

            LLVM.PositionBuilderAtEnd(_Builder, thenBB);

            foreach (var s in stmt.trueBlock.statements)
            {
                Visit(s);
            }

            LLVM.BuildBr(_Builder, mergeBB);

            LLVM.PositionBuilderAtEnd(_Builder, elseBB);
            if (stmt.elseBlock != null)
            {
                foreach (var s in stmt.elseBlock.statements)
                {
                    Visit(s);
                }
            }
            LLVM.BuildBr(_Builder, mergeBB);
            LLVM.PositionBuilderAtEnd(_Builder, mergeBB);

            return stmt;
        }

        public unsafe object? Visit(VariableSetStatement stmt)
        {
            if (VariableTable.ContainsKey(stmt.Name))
            {
                var allocaVal = VariableTable[stmt.Name].Alloca;
                var strAllocaRes = GetStructAllocaVal(stmt.Value);

                if (strAllocaRes != null)
                {
                    VarStructAllocaTable[stmt.Name] = strAllocaRes;
                    LLVM.BuildStore(_Builder, strAllocaRes, allocaVal);
                }

                Visit(stmt.Value);
                LLVM.BuildStore(_Builder, ValueStack.Pop(), allocaVal);

                return stmt;
            }

            throw new Exception("No alloca variable value found for variable " + stmt.Name);
        }

        public unsafe Pointer<LLVMOpaqueValue> Visit(VariableDeclarationStatement stmt)
        {
            var alloca = LLVM.BuildAlloca(_Builder, GetLLVMType(stmt.Type), StrToSByte(stmt.Name));
            Visit(stmt.PrimaryValue);
            var val = ValueStack.Pop();

            var strAllocaRes = GetStructAllocaVal(stmt.PrimaryValue);

            if (strAllocaRes != null)
            {
                VarStructAllocaTable.Add(stmt.Name, strAllocaRes);
                LLVM.BuildStore(_Builder, strAllocaRes, alloca);
            }

            LLVM.BuildStore(_Builder, val, alloca);

            VariableTable.Add(stmt.Name, new VariableData(stmt.Name, GetLLVMType(stmt.Type), alloca));

            return alloca;
        }

        // Expression Visit function implementations

        public unsafe object? Visit(IntLiteralExpr expr)
        {
            ValueStack.Push(LLVM.ConstInt(LLVM.Int32Type(), (ulong)expr.Value, 0));
            return expr;
        }

        public unsafe object? Visit(StringLiteralExpr expr)
        {
            ValueStack.Push(LLVM.BuildGlobalStringPtr(_Builder, StrToSByte(expr.Value), StrToSByte("str_tmp")));
            return expr;
        }

        public unsafe object? Visit(VariableExpr expr)
        {
            var varData = VariableTable[expr.Name];
            ValueStack.Push(LLVM.BuildLoad2(_Builder, varData.Type, varData.Alloca, StrToSByte("value_of" + expr.Name)));
            return expr;
        }

        public unsafe object? Visit(InvokeFunctionExpr expr)
        {
            if (FunctionTable.ContainsKey(expr.FunctionName) || expr.FunctionName == "print")
            {
                Pointer<LLVMOpaqueValue> funcValue = LLVM.GetNamedFunction(_Module, StrToSByte(expr.FunctionName));
                var arguments = new LLVMOpaqueValue*[expr.Arguments.Count];

                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    this.Visit(expr.Arguments[i]);
                    arguments[i] = ValueStack.Pop();
                }

                fixed (LLVMOpaqueValue** pArgs = arguments)
                {
                    LLVMOpaqueValue* result = null;
                    if (expr.FunctionName != "print")
                    {
                        FunctionData fnData = FunctionTable[expr.FunctionName];
                        Pointer<LLVMOpaqueType> funcType = fnData.FunctionType;

                        if (fnData.ReturnType != LLVM.VoidType())
                            result = LLVM.BuildCall2(_Builder, funcType, funcValue, pArgs, (uint)arguments.Length, StrToSByte("invoke_" + expr.FunctionName));
                        else
                            result = LLVM.BuildCall2(_Builder, funcType, funcValue, pArgs, (uint)arguments.Length, StrToSByte(""));
                    }
                    else
                    {
                        try
                        {
                            result = LLVM.BuildCall2(_Builder, _PrintFuncType, _PrintFunc, pArgs, (uint)arguments.Length, StrToSByte("printf"));
                        }
                        catch
                        {
                            Console.WriteLine(SByteToStr(LLVM.PrintModuleToString(_Module)));
                        }
                    }

                    if (result != null)
                        ValueStack.Push(result);
                    else
                        throw new Exception("Could not create function call");
                }
            }
            else
            {
                throw new Exception("Function does not exist.");
            }
            return expr;
        }

        public unsafe LLVMOpaqueValue* GetStructMemberGEP(StructMemberAccessExpr expr)
        {
            Visit(expr.ObjectName);
            var evalExpr = ValueStack.Pop();

            if (expr.ObjectName is VariableExpr) // this means that evalExpr = LLVM.BuildLoad(.., struct alloca value, ...)
            {
                var strType = LLVM.TypeOf(evalExpr);
                var strDecl = StructTable.Single(e => e.Value.StructLLVMType == strType).Value;

                var varName = ((VariableExpr)expr.ObjectName).Name;

                var memberGEP = LLVM.BuildStructGEP2(_Builder, strType, VarStructAllocaTable[varName], (uint)strDecl.Indices[expr.MemberName], StrToSByte("struct_memberGEP"));
                return memberGEP;
            }

            throw new Exception("Couldn't get the GEP of the struct member.");
        }

        /// <summary>
        /// More efficient version, when more data is known.
        /// </summary>
        /// <param name="expr"></param>
        /// <param name="strType"></param>
        /// <param name="strDecl"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public unsafe LLVMOpaqueValue* GetStructMemberGEP(StructMemberAccessExpr expr, LLVMOpaqueType* strType, StructData strData)
        {
            Visit(expr.ObjectName);
            var evalExpr = ValueStack.Pop();

            if (expr.ObjectName is VariableExpr) // this means that evalExpr = LLVM.BuildLoad(.., struct alloca value, ...)
            {
                var varName = ((VariableExpr)expr.ObjectName).Name;

                var memberGEP = LLVM.BuildStructGEP2(_Builder, strType, VarStructAllocaTable[varName], (uint)strData.Indices[expr.MemberName], StrToSByte("struct_memberGEP"));
                return memberGEP;
            }

            throw new Exception("Couldn't get the GEP of the struct member.");
        }

        public unsafe object? Visit(StructMemberAccessExpr expr)
        {
            Visit(expr.ObjectName);
            var evalExpr = ValueStack.Pop();

            if (expr.ObjectName is VariableExpr) // this means that evalExpr = LLVM.BuildLoad(.., struct alloca value, ...)
            {
                var strType = LLVM.TypeOf(evalExpr);
                var strDecl = StructTable.Single(e => e.Value.StructLLVMType == strType).Value;

                var varName = ((VariableExpr)expr.ObjectName).Name;
                ValueStack.Push(LLVM.BuildLoad2(_Builder, GetLLVMType(strDecl.Properties[strDecl.Indices[expr.MemberName]].Type), GetStructMemberGEP(expr, strType, strDecl), StrToSByte("struct_member")));
            }

            return expr;
        }

        public unsafe object? Visit(StructObjCreationExpr expr)
        {
            var str = StructTable[expr.StructName];
            var strAlloca = LLVM.BuildAlloca(_Builder, str.StructLLVMType, StrToSByte("struct_" + expr.StructName + "Instance"));

            LLVMOpaqueType*[] structTypes = new LLVMOpaqueType*[str.Properties.Count];

            for (int i = 0; i < expr.Args.Count; i++)
            {
                var memberPtr = LLVM.BuildStructGEP2(_Builder, str.StructLLVMType, strAlloca, (uint)i, StrToSByte("struct_memberGEP"));
                Visit(expr.Args[i]);
                LLVM.BuildStore(_Builder, ValueStack.Pop(), memberPtr);
            }

            ValueStack.Push(strAlloca);
            StructStack.Push(strAlloca);
            return expr;
        }

        // Usage Functions

        /// <summary>
        /// Initializes the LLVM IR Emitter under the Native Asm backend.
        /// </summary>
        /// <exception cref="IREmitterException"></exception>
        public void InitializeNatAsm()
        {
            // Set the initialization value to true, if it's already true(initialized), throw an exception.
            Initialized = Initialized == false ? true : throw new IREmitterException("IR Emitter is already initialized");

            // Initialize LLVM, and the native backend.
            LLVM.InitializeNativeAsmPrinter();
            LLVM.InitializeNativeAsmParser();
            LLVM.InitializeNativeTarget();

            // Find the first available target
            var target = LLVM.GetFirstTarget();

            // Create a target machine
            TargetMachine = LLVM.CreateTargetMachine
                (
                    target, 
                    StrToSByte("generic"), 
                    StrToSByte("generic"), 
                    StrToSByte(""), 
                    LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, 
                    LLVMRelocMode.LLVMRelocDefault, 
                    LLVMCodeModel.LLVMCodeModelDefault
                );
        }

        public Pointer<LLVMOpaqueModule> GenerateIR(List<Statement> statements, string moduleName)
        {
            if (Initialized)
            {
                // Create the LLVM Module and Builder.
                _Module = LLVM.ModuleCreateWithName(StrToSByte(moduleName));
                _Builder = LLVM.CreateBuilder();

                // Create the pass manager(and the options)
                var FPM = LLVM.CreateFunctionPassManagerForModule(_Module);
                LLVM.AddAnalysisPasses(TargetMachine, FPM);

                LLVMPassBuilderOptionsRef pbo = LLVM.CreatePassBuilderOptions();
                pbo.SetSLPVectorization(true);
                pbo.SetLoopVectorization(true);
                pbo.SetCallGraphProfile(true);

                LLVM.InitializeFunctionPassManager(FPM);

                // Declare the printf function in our LLVM IR.
                fixed (LLVMOpaqueType** printfParams = new LLVMOpaqueType*[] { LLVM.PointerType(LLVM.Int8Type(), 0) })
                {
                    _PrintFuncType = LLVM.FunctionType(LLVM.Int32Type(), printfParams, 1, 1);
                    _PrintFunc = LLVM.AddFunction(_Module, StrToSByte("printf"), _PrintFuncType);
                }

                // And generate the LLVM IR from our AST.
                foreach (Statement statement in statements)
                {
                    Visit(statement);
                }

                // Run the passes on the module to optimize it.
                LLVM.RunPasses(_Module, StrToSByte("mem2reg"), TargetMachine, pbo);
                LLVM.RunPasses(_Module, StrToSByte("gvn"), TargetMachine, pbo);
                LLVM.RunPasses(_Module, StrToSByte("instcombine"), TargetMachine, pbo);
                LLVM.RunPasses(_Module, StrToSByte("early-cse"), TargetMachine, pbo);

                return _Module;
            }

            throw new IREmitterException("The IR Emitter must be first initialized to use.");
        }
    }

    public class IREmitterException : Exception
    {
        public IREmitterException(string? message) : base(message)
        {
        }
    }
}

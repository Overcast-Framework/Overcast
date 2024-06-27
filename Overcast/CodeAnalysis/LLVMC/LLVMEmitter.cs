using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using LLVMSharp;
using LLVMSharp.Interop;
using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Parsing.Expressions;
using Overcast.CodeAnalysis.Parsing.Statements;
using static System.Runtime.InteropServices.JavaScript.JSType;
using static Overcast.OCHelper;

namespace Overcast.CodeAnalysis.LLVMC
{
    public unsafe class LLVMEmitter
    {
        private LLVMOpaqueModule* _module;
        private LLVMOpaqueBuilder* _builder;
        private LLVMOpaqueValue* _printf;
        private LLVMOpaqueType* _printfType;

        private Dictionary<string, Pointer<LLVMOpaqueType>> FunctionTable = new Dictionary<string, Pointer<LLVMOpaqueType>>();
        private Dictionary<string, Pointer<LLVMOpaqueValue>> VarAllocaTable = new Dictionary<string, Pointer<LLVMOpaqueValue>>();
        private Dictionary<string, Pointer<LLVMOpaqueType>> StructTable = new Dictionary<string, Pointer<LLVMOpaqueType>>();
        private Dictionary<string, VariableDeclarationStatement> VariableDeclarations = new Dictionary<string, VariableDeclarationStatement>();
        private Dictionary<string, FunctionDeclarationStatement> FunctionDeclarations = new Dictionary<string, FunctionDeclarationStatement>();
        private Dictionary<string, StructDeclarationStatement> StructDeclarations = new Dictionary<string, StructDeclarationStatement>();
        private Dictionary<Pointer<LLVMOpaqueType>, StructDeclarationStatement> StructTypeDeclarations = new Dictionary<Pointer<LLVMOpaqueType>, StructDeclarationStatement>();
        private Dictionary<string, Pointer<LLVMOpaqueValue>> StructVAllocaTable = new Dictionary<string, Pointer<LLVMOpaqueValue>>();

        private Stack<Pointer<LLVMOpaqueValue>> ValueStack = new Stack<Pointer<LLVMOpaqueValue>>();

        private LLVMPassManagerRef FPM;

        private LLVMOpaqueTargetMachine* TargetMachine;

        public bool Initialized = false;

        public LLVMPassBuilderOptionsRef pbo;

        public unsafe void Initialize_NatAsm(string moduleName)
        {
            //LLVM.InitializeCore(LLVM.GetGlobalPassRegistry());
            LLVM.InitializeNativeAsmPrinter();
            LLVM.InitializeNativeAsmParser();
            LLVM.InitializeNativeTarget();

            _module = LLVM.ModuleCreateWithName(StrToSByte(moduleName));
            _builder = LLVM.CreateBuilder();

            var target = LLVM.GetFirstTarget();

            TargetMachine = LLVM.CreateTargetMachine(target, StrToSByte("generic"), StrToSByte("generic"), StrToSByte(""), LLVMCodeGenOptLevel.LLVMCodeGenLevelDefault, LLVMRelocMode.LLVMRelocDefault, LLVMCodeModel.LLVMCodeModelDefault);

            FPM = LLVM.CreateFunctionPassManagerForModule(_module);
            LLVM.AddAnalysisPasses(TargetMachine, FPM);

            pbo = LLVM.CreatePassBuilderOptions();
            pbo.SetSLPVectorization(true);
            pbo.SetLoopVectorization(true);
            pbo.SetCallGraphProfile(true);

            LLVM.InitializeFunctionPassManager(FPM);

            Initialized = true;
        }

        #region Statement Visitation

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
            _ => throw new Exception("Statement unsupported.")
        };

        public unsafe object? Visit(StructMemberSetStatement stmt)
        {
            Visit(stmt.Accessee.ObjectName);
            var evalExpr = ValueStack.Pop();

            if(stmt.Accessee.ObjectName is VariableExpr)
            {
                var strType = LLVM.TypeOf(evalExpr);
                var strDecl = StructTypeDeclarations[strType];
                var varName = ((VariableExpr)stmt.Accessee.ObjectName).Name;

                for (int i = 0; i < strDecl.Members.Count; i++)
                {
                    var member = strDecl.Members[i];
                    if (member.Name == stmt.Accessee.MemberName)
                    {
                        var memberGEP = LLVM.BuildStructGEP2(_builder, strType, StructVAllocaTable[varName], (uint)i, StrToSByte("struct_memberGEP"));
                        Visit(stmt.Value);
                        LLVM.BuildStore(_builder, ValueStack.Pop(), memberGEP);
                        break;
                    }
                }
            }

            return stmt;
        }

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
                        return StructTable[identifierType.Name];
                    }
                    throw new Exception("Unknown type when getting LLVM type");
                }
            }
            else if (type is Overcast.CodeAnalysis.Parsing.PointerType pointerType)
            {
                return LLVM.PointerType(GetLLVMType(pointerType.OfType), 0);
            }
            else
            {
                throw new Exception("Unsupported type when getting LLVM type");
            }
        }

        public unsafe object? Visit(StructDeclarationStatement stmt)
        {
            LLVMOpaqueType*[] structTypes = new LLVMOpaqueType*[stmt.Members.Count];

            for(int i = 0; i < structTypes.Length; i++)
                structTypes[i] = GetLLVMType(stmt.Members[i].Type);

            fixed (LLVMOpaqueType** pStrTypes = structTypes)
            {
                var cStructType = LLVM.StructType(pStrTypes, (uint)structTypes.Length, 0);
                StructTable.Add(stmt.Name, cStructType);
                StructDeclarations.Add(stmt.Name, stmt);
                StructTypeDeclarations.Add(cStructType, stmt);
                LLVM.AddGlobal(_module, cStructType, StrToSByte(stmt.Name));
            }

            return stmt;
        }

        public unsafe object? Visit(ReturnStatement stmt)
        {
            Visit(stmt.Value);
            var retValue = ValueStack.Pop();

            LLVM.BuildRet(_builder, retValue);

            return stmt;
        }

        public unsafe object? Visit(IfStatement stmt)
        {
            var function = LLVM.GetBasicBlockParent(LLVM.GetInsertBlock(_builder));

            Visit(stmt.conditandB);
            Visit(stmt.conditandA);

            var condA = ValueStack.Pop();
            var condB = ValueStack.Pop();

            LLVMOpaqueValue* condValue = null;

            switch(stmt._operator)
            {
                case ConditionOperator.GT:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGT, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.LT:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLT, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.GE:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSGE, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.LE:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntSLE, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.EQEQ:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntEQ, condA, condB, StrToSByte("cmp"));
                    break;
                case ConditionOperator.NEQ:
                    condValue = LLVM.BuildICmp(_builder, LLVMIntPredicate.LLVMIntNE, condA, condB, StrToSByte("cmp"));
                    break;
            }

            var thenBB = LLVM.AppendBasicBlock(function, StrToSByte("then"));
            var elseBB = LLVM.AppendBasicBlock(function, StrToSByte("else"));
            var mergeBB = LLVM.AppendBasicBlock(function, StrToSByte("ifcont"));

            LLVM.BuildCondBr(_builder, condValue, thenBB, elseBB);

            LLVM.PositionBuilderAtEnd(_builder, thenBB);
            
            foreach(var s in stmt.trueBlock.statements)
            {
                Visit(s);
            }

            LLVM.BuildBr(_builder, mergeBB);

            LLVM.PositionBuilderAtEnd(_builder, elseBB);
            if (stmt.elseBlock != null)
            {
                foreach (var s in stmt.elseBlock.statements)
                {
                    Visit(s);
                }
            }
            LLVM.BuildBr(_builder, mergeBB);
            LLVM.PositionBuilderAtEnd(_builder, mergeBB);

            return stmt;
        }

        public unsafe object? Visit(VariableSetStatement stmt)
        {
            if(VarAllocaTable.ContainsKey(stmt.Name))
            {
                var allocaVal = VarAllocaTable[stmt.Name];

                Visit(stmt.Value);
                LLVM.BuildStore(_builder, ValueStack.Pop(), allocaVal);

                return stmt;
            }

            throw new Exception("No alloca variable value found for variable " + stmt.Name);
        }

        public unsafe Pointer<LLVMOpaqueValue> Visit(VariableDeclarationStatement stmt)
        {
            var alloca = LLVM.BuildAlloca(_builder, GetLLVMType(stmt.Type), StrToSByte(stmt.Name));
            Visit(stmt.PrimaryValue);
            var val = ValueStack.Pop();

            if (stmt.PrimaryValue is StructObjCreationExpr)
            {
                StructVAllocaTable.Add(stmt.Name, val);
            }
            if(stmt.PrimaryValue is InvokeFunctionExpr)
            {
                var ife = (InvokeFunctionExpr)stmt.PrimaryValue;
                var fn = FunctionDeclarations[ife.FunctionName];

                if(StructTable.ContainsKey(fn.ReturnType.GetBaseType().ToString()))
                {
                    if(LLVM.IsAAllocaInst(val) != null)
                    {
                        StructVAllocaTable.Add(stmt.Name, val);
                    }
                }
            }

            LLVM.BuildStore(_builder, val, alloca);

            VarAllocaTable.Add(stmt.Name, alloca);
            VariableDeclarations.Add(stmt.Name, stmt);

            return alloca;
        }

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
                var directFunc = LLVM.AddFunction(_module, StrToSByte(statement.Name), funcType);

                FunctionTable.Add(statement.Name, funcType);
                FunctionDeclarations.Add(statement.Name, statement);

                LLVM.PositionBuilderAtEnd(_builder, LLVM.AppendBasicBlock(directFunc, StrToSByte("entry")));

                for (int i = 0; i < llvmParamList.Length; i++)
                {
                    var param = statement.Parameters[i];
                    var paramVal = LLVM.GetParam(directFunc, (uint)i);

                    var alloca = LLVM.BuildAlloca(_builder, param.Type.LLVMType(), StrToSByte(param.Name));
                    LLVM.BuildStore(_builder, paramVal, alloca);

                    VarAllocaTable[param.Name] = alloca;

                    // also create a fake variable declaration

                    VariableDeclarations[param.Name] = new VariableDeclarationStatement(param.Name, null!, param.Type);
                }

                foreach (var innerStmt in statement.Block.statements)
                {
                    this.Visit(innerStmt);
                }

                VarAllocaTable.Clear();
                VariableDeclarations.Clear();

                if (retType == LLVM.VoidType())
                {
                    LLVM.BuildRetVoid(_builder);
                }

                LLVM.VerifyFunction(directFunc, LLVMVerifierFailureAction.LLVMPrintMessageAction);
                LLVM.RunFunctionPassManager(FPM, directFunc);

                return directFunc;
            }

            throw new Exception("Could not create function from declaration statement");
        }

        #endregion

        #region Expression Visitation

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

        public unsafe object? Visit(ReferenceExpr expr)
        {
            if(expr.Value is VariableExpr)
            {
                var varAlloca = VarAllocaTable[(expr.Value as VariableExpr).Name];
                ValueStack.Push(varAlloca);
            }
            else if(expr.Value is StructMemberAccessExpr)
            {
                ValueStack.Push(GetStructMemberGEP((StructMemberAccessExpr)expr.Value));
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
                var strDecl = StructTypeDeclarations[strType];

                var varName = ((VariableExpr)expr.ObjectName).Name;

                var memberGEP = LLVM.BuildStructGEP2(_builder, strType, StructVAllocaTable[varName], (uint)strDecl.MemberIndexTable[expr.MemberName], StrToSByte("struct_memberGEP"));
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
        public unsafe LLVMOpaqueValue* GetStructMemberGEP(StructMemberAccessExpr expr, LLVMOpaqueType* strType, StructDeclarationStatement strDecl)
        {
            Visit(expr.ObjectName);
            var evalExpr = ValueStack.Pop();

            if (expr.ObjectName is VariableExpr) // this means that evalExpr = LLVM.BuildLoad(.., struct alloca value, ...)
            {
                var varName = ((VariableExpr)expr.ObjectName).Name;

                var memberGEP = LLVM.BuildStructGEP2(_builder, strType, StructVAllocaTable[varName], (uint)strDecl.MemberIndexTable[expr.MemberName], StrToSByte("struct_memberGEP"));
                return memberGEP;
            }

            throw new Exception("Couldn't get the GEP of the struct member.");
        }

        public unsafe object? Visit(StructMemberAccessExpr expr)
        {
            Visit(expr.ObjectName);
            var evalExpr = ValueStack.Pop();

            if(expr.ObjectName is VariableExpr) // this means that evalExpr = LLVM.BuildLoad(.., struct alloca value, ...)
            {
                var strType = LLVM.TypeOf(evalExpr);
                var strDecl = StructTypeDeclarations[strType];

                var varName = ((VariableExpr)expr.ObjectName).Name;
                ValueStack.Push(LLVM.BuildLoad2(_builder, GetLLVMType(strDecl.Members[strDecl.MemberIndexTable[expr.MemberName]].Type), GetStructMemberGEP(expr, strType, strDecl), StrToSByte("struct_member")));
            }

            return expr;
        }

        public unsafe object? Visit(StructObjCreationExpr expr)
        {
            var strType = StructTable[expr.StructName];
            var stmt = StructDeclarations[expr.StructName];
            var strAlloca = LLVM.BuildAlloca(_builder, strType, StrToSByte("struct_"+expr.StructName+"Instance"));

            LLVMOpaqueType*[] structTypes = new LLVMOpaqueType*[stmt.Members.Count];

            for (int i = 0; i < expr.Args.Count; i++)
            {
                var memberPtr = LLVM.BuildStructGEP2(_builder, strType, strAlloca, (uint)i, StrToSByte("struct_memberGEP"));
                Visit(expr.Args[i]);
                LLVM.BuildStore(_builder, ValueStack.Pop(), memberPtr);
            }

            ValueStack.Push(strAlloca);
            return expr;
        }

        public unsafe object? Visit(BinaryExpression expr)
        {
            Visit(expr.primaryB);
            Visit(expr.primaryA);

            var lhVal = ValueStack.Pop();
            var rhVal = ValueStack.Pop();

            LLVMOpaqueValue* result = null;

            switch (expr._operator)
            {
                case "+":
                    result = LLVM.BuildAdd(_builder, lhVal, rhVal, StrToSByte("arithmetic"));
                    break;
                case "-":
                    result = LLVM.BuildSub(_builder, lhVal, rhVal, StrToSByte("arithmetic"));
                    break;
                case "*":
                    result = LLVM.BuildMul(_builder, lhVal, rhVal, StrToSByte("arithmetic"));
                    break;
                case "/":
                    result = LLVM.BuildSDiv(_builder, lhVal, rhVal, StrToSByte("arithmetic"));
                    break;
            }

            ValueStack.Push(result);

            return expr;
        }

        public unsafe object? Visit(IntLiteralExpr expr)
        {
            ValueStack.Push(LLVM.ConstInt(LLVM.Int32Type(), (ulong)expr.Value, 0));
            return expr;
        }

        public unsafe object? Visit(StringLiteralExpr expr)
        {
            ValueStack.Push(LLVM.BuildGlobalStringPtr(_builder, StrToSByte(expr.Value), StrToSByte("str_tmp")));
            return expr;
        }

        public unsafe object? Visit(VariableExpr expr)
        {
            var varDecl = VariableDeclarations[expr.Name];
            ValueStack.Push(LLVM.BuildLoad2(_builder, GetLLVMType(varDecl.Type), VarAllocaTable[expr.Name], StrToSByte("value_of" + expr.Name)));
            return expr;
        }

        public unsafe object? Visit(InvokeFunctionExpr expr)
        {
            if (FunctionTable.ContainsKey(expr.FunctionName) || expr.FunctionName == "print")
            {
                Pointer<LLVMOpaqueValue> funcValue = LLVM.GetNamedFunction(_module, StrToSByte(expr.FunctionName));
                var arguments = new LLVMOpaqueValue*[expr.Arguments.Count];

                for (int i = 0; i < expr.Arguments.Count; i++)
                {
                    this.Visit(expr.Arguments[i]);
                    arguments[i] = ValueStack.Pop();
                }

                fixed(LLVMOpaqueValue** pArgs = arguments)
                {
                    LLVMOpaqueValue* result = null;
                    if (expr.FunctionName != "print")
                    {
                        Pointer<LLVMOpaqueType> funcType = FunctionTable[expr.FunctionName];
                        FunctionDeclarationStatement fnDecl = FunctionDeclarations[expr.FunctionName];

                        if(GetLLVMType(fnDecl.ReturnType) != LLVM.VoidType())
                            result = LLVM.BuildCall2(_builder, funcType, funcValue, pArgs, (uint)arguments.Length, StrToSByte("invoke_" + expr.FunctionName));
                        else
                            result = LLVM.BuildCall2(_builder, funcType, funcValue, pArgs, (uint)arguments.Length, StrToSByte(""));
                    }
                    else
                    {
                        try
                        {
                            result = LLVM.BuildCall2(_builder, _printfType, _printf, pArgs, (uint)arguments.Length, StrToSByte("printf"));
                        }
                        catch 
                        {
                            Console.WriteLine(SByteToStr(LLVM.PrintModuleToString(_module)));
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

        #endregion

        public unsafe LLVMOpaqueModule* EmitModule(List<Statement> statements)
        {
            if (Initialized)
            {
                try
                {
                    // First, extern printf for any printing purposes. (TO-DO: Replace this with a less C-stdlib dependent way to print)
                    fixed (LLVMOpaqueType** printfParams = new LLVMOpaqueType*[] { LLVM.PointerType(LLVM.Int8Type(), 0) })
                    {
                        _printfType = LLVM.FunctionType(LLVM.Int32Type(), printfParams, 1, 1);
                        _printf = LLVM.AddFunction(_module, StrToSByte("printf"), _printfType);
                    }

                    foreach (Statement statement in statements)
                    {
                        Visit(statement);
                    }
                }
                catch(AccessViolationException e)
                {
                    Console.WriteLine("EXCEPTION OCCURED");
                    Console.WriteLine(SByteToStr(LLVM.PrintModuleToString(_module)));
                }

                LLVM.RunPasses(_module, StrToSByte("tailcallelim"), TargetMachine, pbo);
                LLVM.RunPasses(_module, StrToSByte("mem2reg"), TargetMachine, pbo);
                LLVM.RunPasses(_module, StrToSByte("gvn"), TargetMachine, pbo);
                LLVM.RunPasses(_module, StrToSByte("instcombine"), TargetMachine, pbo);
                LLVM.RunPasses(_module, StrToSByte("early-cse"), TargetMachine, pbo);

                return _module;
            }
            throw new Exception("LLVM emitter must be initialized with your desired backend to use.");
        }

        public unsafe void Dispose()
        {
            LLVM.DisposeBuilder(_builder);
            LLVM.DisposeModule(_module);
            LLVM.DisposePassManager(FPM);
        }
    }
}

using LLVMSharp;
using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Parsing.Expressions;
using Overcast.CodeAnalysis.Parsing.Statements;
using Overcast.CodeAnalysis.Semantic.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Semantic
{
    public class Binder
    {
        public Stack<Scope> Scopes = new Stack<Scope>();
        public Dictionary<string, FunctionSymbol> Functions = new Dictionary<string, FunctionSymbol>();
        public Dictionary<string, StructSymbol> Structs = new Dictionary<string, StructSymbol>();
        public FunctionSymbol CurrentFn;

        #region Statement Visitation

        public object? Visit(Statement stmt) => stmt switch
        {
            FunctionDeclarationStatement fnDeclStmt => Visit(fnDeclStmt),
            VariableDeclarationStatement variableDeclStmt => Visit(variableDeclStmt),
            StructDeclarationStatement structDeclStmt => Visit(structDeclStmt),
            VariableSetStatement varSetStmt => Visit(varSetStmt),
            ExpressionStatement exprStmt => Visit(exprStmt.Expr),
            ReturnStatement retStmt => Visit(retStmt),
            StructMemberSetStatement structMemberSetStatement => Visit(structMemberSetStatement),
            _ => null
        };

        public object? Visit(StructMemberSetStatement stmt)
        {
            Visit(stmt.Accessee);

            if(!GetTypeOfExpr(stmt.Accessee).Equals(GetTypeOfExpr(stmt.Value)))
            {
                throw new BinderException("Cannot set struct member of type " + GetTypeOfExpr(stmt.Accessee) + " using value of type " + GetTypeOfExpr(stmt.Value));
            }

            return stmt;
        }

        public object? Visit(StructDeclarationStatement stmt)
        {
            if (Structs.ContainsKey(stmt.Name))
                throw new BinderException("Attempt to redeclare existing struct " + stmt.Name);

            Structs.Add(stmt.Name, new StructSymbol(stmt.Name, stmt.Members));
            return stmt;
        }

        public object? Visit(ReturnStatement stmt)
        {
            if (CurrentFn != null)
            {
                if(!GetTypeOfExpr(stmt.Value).Equals(CurrentFn.ReturnType))
                {
                    throw new BinderException("Return/Function type mismatch.");
                }
            }
            else
                throw new BinderException("Top-level returns are not supported.");

            return stmt;
        }

        public object? Visit(VariableSetStatement stmt)
        {
            if(Scopes.Peek().LocalExists(stmt.Name))
            {
                var currentScope = Scopes.Peek();
                var varSym = currentScope.Locals[stmt.Name];
                if (!GetTypeOfExpr(stmt.Value).Equals(varSym.Type))
                {
                    throw new BinderException("Type mismatch while setting variable " + stmt.Name + ".");
                }
                return stmt;
            }

            throw new BinderException("Attempt to set value for invalid variable.");   
        }

        public object? Visit(FunctionDeclarationStatement stmt)
        {
            List<VariableSymbol> variables = new List<VariableSymbol>();

            foreach (Parameter param in stmt.Parameters)
            {
                variables.Add(new VariableSymbol(param.Name, param.Type));
            }

            Functions.Add(stmt.Name, new FunctionSymbol(variables, stmt.Name, stmt.ReturnType));

            Scopes.Push(new Scope());
            CurrentFn = Functions[stmt.Name];
            foreach (VariableSymbol variable in variables) // support for parameters
                Scopes.Peek().AddLocal(variable.Name, variable);

            foreach(var fnPartStmt in stmt.Block.statements)
            {
                Visit(fnPartStmt);
            }

            Scopes.Pop();
            return stmt;
        }

        public object? Visit(VariableDeclarationStatement stmt)
        {
            var currentScope = Scopes.Peek();
            if(!currentScope.LocalExists(stmt.Name))
            {
                Visit(stmt.PrimaryValue);
                if (stmt.Type.Equals(GetTypeOfExpr(stmt.PrimaryValue)))
                {
                    currentScope.AddLocal(stmt.Name, new VariableSymbol(stmt.Name, stmt.Type)); // insert the variable into the scope.
                }
                else
                {
                    throw new BinderException("Variable type and value type mismatch");
                }    
                return stmt;
            }
            else
            {
                throw new BinderException($"Attempt to redeclare an already declared variable {stmt.Name}.");
            }
        }

        #endregion

        #region Expression Visitation

        public object? Visit(Expression expr) => expr switch
        {
            InvokeFunctionExpr invFuncExpr => Visit(invFuncExpr),
            VariableExpr varExpr => Visit(varExpr),
            BinaryExpression binExpr => Visit(binExpr),
            StructMemberAccessExpr structMemberAccessExpr => Visit(structMemberAccessExpr),
            ReferenceExpr refExpr => Visit(refExpr),
            _ => null
        };

        public object? Visit(ReferenceExpr expr)
        {
            if(expr.Value is VariableExpr || expr.Value is StructMemberAccessExpr)
            {
                return expr;
            }

            throw new BinderException("Reference can only be taken of valid lvalues");
        }

        public object? Visit(VariableExpr expr)
        {
            if (!Scopes.Peek().LocalExists(expr.Name))
                throw new BinderException("Attempt to access undefined variable " + expr.Name); 
            return expr;
        }

        public object? Visit(StructMemberAccessExpr expr)
        {
            Visit(expr.ObjectName);

            var _structType = GetTypeOfExpr(expr.ObjectName);
            var _structName = _structType.GetBaseType().Name;
            var _struct = Structs[_structName];

            foreach (var x in _struct.members)
                if (x.Name == expr.MemberName)
                    return expr;

            throw new BinderException("Attempt to access invalid member of struct "+_structName);
        }

        public object? Visit(BinaryExpression expr)
        {
            Visit(expr.primaryA);
            Visit(expr.primaryB);

            if (!GetTypeOfExpr(expr.primaryA).Equals(GetTypeOfExpr(expr.primaryB)))
            {
                throw new BinderException("Attempted to do arithmetics on incompatible types.");
            }

            return expr;
        }

        public OCType GetTypeOfExpr(Expression expr) => expr switch
        {
            StringLiteralExpr => IdentifierType.String,
            IntLiteralExpr => IdentifierType.Integer,
            InvokeFunctionExpr invFuncExpr => GetTypeOfExpr(invFuncExpr),
            VariableExpr varEx => GetTypeOfExpr(varEx),
            BinaryExpression binEx => GetTypeOfExpr(binEx),
            StructObjCreationExpr structObjCreationExpr => GetTypeOfExpr(structObjCreationExpr),
            StructMemberAccessExpr structMemberAccessExpr => GetTypeOfExpr(structMemberAccessExpr),
            ReferenceExpr refExpr => GetTypeOfExpr(refExpr),
            _ => IdentifierType.Any
        };

        public OCType GetTypeOfExpr(ReferenceExpr expr)
        {
            return new Overcast.CodeAnalysis.Parsing.PointerType(GetTypeOfExpr(expr.Value));
        }

        public OCType GetTypeOfExpr(StructMemberAccessExpr expr)
        {
            Visit(expr.ObjectName);

            var _structType = GetTypeOfExpr(expr.ObjectName);
            var _structName = _structType.GetBaseType().Name;
            var _struct = Structs[_structName];

            foreach(var x in _struct.members)
            {
                if(x.Name == expr.MemberName)
                {
                    return x.Type;
                }
            }

            throw new BinderException("Attempt to access invalid member of struct " + _structName);
        }

        public OCType GetTypeOfExpr(StructObjCreationExpr expr)
        {
            if(Structs.ContainsKey(expr.StructName))
            {
                return new IdentifierType(expr.StructName);
            }
            throw new BinderException($"No such struct {expr.StructName} exists.");
        }

        public OCType GetTypeOfExpr(BinaryExpression binEx)
        {
            return GetTypeOfExpr(binEx.primaryA);
        }

        public OCType GetTypeOfExpr(InvokeFunctionExpr expr)
        {
            if (Functions.ContainsKey(expr.FunctionName))
            {
                var funcSymbol = Functions[expr.FunctionName];
                return funcSymbol.ReturnType;
            }
            else
            {
                throw new BinderException($"Attempt to invoke/call invalid function {expr.FunctionName}.");
            }
        }

        public OCType GetTypeOfExpr(VariableExpr expr)
        {
            var currentScope = Scopes.Peek();
            if (currentScope.LocalExists(expr.Name))
            {
                var variable = currentScope.Locals[expr.Name];
                return variable.Type;
            }
            else
            {
                throw new BinderException($"Invalid variable {expr.Name} passed.");
            }
        }

        public object? Visit(InvokeFunctionExpr expr)
        {
            if(Functions.ContainsKey(expr.FunctionName))
            {
                var fn = Functions[expr.FunctionName];

                if ((expr.Arguments.Count > fn.Parameters.Count || expr.Arguments.Count < fn.Parameters.Count) && fn.VArgs == false)
                    throw new BinderException("Argument/parameter count mismatch.");

                if(fn.VArgs == false)
                    for (int i = 0; i < expr.Arguments.Count; i++)
                    {
                        var param = expr.Arguments[i];
                        var fnParam = fn.Parameters[i];

                        if(!GetTypeOfExpr(param).Equals(fnParam.Type))
                        {
                            throw new BinderException($"Incorrect argument passed to function {fn.Name}. Expected argument of type {fnParam.Type.ToString()}, instead got {GetTypeOfExpr(param).ToString()}.");
                        }
                    }
                else
                {
                    for (int i = 0; i < fn.Parameters.Count; i++)
                    {
                        var param = expr.Arguments[i];
                        var fnParam = fn.Parameters[i];

                        if (!GetTypeOfExpr(param).Equals(fnParam.Type))
                        {
                            throw new BinderException($"Incorrect argument passed to function {fn.Name}. Expected argument of type {fnParam.Type.ToString()}, instead got {GetTypeOfExpr(param).ToString()}.");
                        }
                    }
                }

                return expr;
            }

            throw new BinderException($"Attempt to invoke/call invalid function {expr.FunctionName}");
        }

        #endregion

        public void RunAnalysis(List<Statement> AST)
        {
            Functions.Add("print", new FunctionSymbol(new List<VariableSymbol> { new VariableSymbol("format", IdentifierType.String) }, "print", IdentifierType.Integer, true));
            foreach(Statement s in AST)
            {
                Visit(s);
            }    
        }
    }

    public class BinderException : Exception
    {
        public BinderException()
        {
        }

        public BinderException(string? message) : base(message)
        {
        }
    }
}

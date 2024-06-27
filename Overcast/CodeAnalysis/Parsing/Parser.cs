using Overcast.CodeAnalysis.Parsing.Expressions;
using Overcast.CodeAnalysis.Parsing.Statements;
using Overcast.CodeAnalysis.Tokenization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class Scope
{
    public List<string> Variables = new List<string>();
}

namespace Overcast.CodeAnalysis.Parsing
{
    public class Parser
    {
        private Token CurrentToken; // The current token, equal to Feed[IndexAt]
        private List<Token> Feed; // The feed of tokens, being processed.
        public int IndexAt = 0; // The current index in the token feed.
        public List<string> Functions = new List<string>();
        public List<string> Structs = new List<string>();
        public Queue<Scope> Scopes = new Queue<Scope>();

        #region Parser Functions

        public StringLiteralExpr ParseStrLitExpr()
        {
            return new StringLiteralExpr(((string)Consume(TokenType.STRING).Value).Replace("\"", "").Replace("\\n", "\n"));
        }

        public IntLiteralExpr ParseIntLitExpr()
        {
            return new IntLiteralExpr((int)Consume(TokenType.INTEGER).Value);
        }

        public VariableExpr ParseVariableExpr()
        {
            return new VariableExpr((string)Consume(TokenType.IDENTIFIER).Value);
        }

        public InvokeFunctionExpr ParseInvFuncExpr()
        {
            var name = Consume(TokenType.IDENTIFIER);
            var exprs = new List<Expression>();
            Consume(TokenType.SYMBOL, "(");
            while (!CurrentToken.Value.Equals(")"))
            {
                exprs.Add(ParseExpression());
                if (CurrentToken.Value.Equals(","))
                {
                    Consume(TokenType.SYMBOL, ",");
                }
            }
            Consume(TokenType.SYMBOL, ")");
            return new InvokeFunctionExpr((string)name.Value, exprs);
        }

        private OperatorInfo GetOperatorInfo(string op)
        {
            return Operators.OperatorPrecedence.TryGetValue(op, out var info) ? info : null;
        }

        public StructMemberAccessExpr ParseStructMemberAcc(Expression expr)
        {
            var objName = expr;
            Consume(TokenType.ARROW);
            var memberName = (string)Consume(TokenType.IDENTIFIER).Value;

            return new StructMemberAccessExpr(objName, memberName);
        }

        public StructObjCreationExpr ParseStrObjCrExpr()
        {
            Consume(TokenType.SYMBOL, "[");
            var structName = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.SYMBOL, "]");
            Consume(TokenType.SYMBOL, "{");
            var values = new List<Expression>();
            while (!CurrentToken.Value.Equals("}"))
            {
                values.Add(ParseExpression());
                if (CurrentToken.Value.Equals(","))
                    Consume(TokenType.SYMBOL, ",");
            }
            Consume(TokenType.SYMBOL, "}");

            if (values.Count < 1)
                throw new ParserException("Not enough initializer values (must be above 0)");

            return new StructObjCreationExpr(structName, values);
        }

        public Expression ParseBinaryExpr(int parentPrecedence = 0)
        {
            var leftHand = ParseExpr();

            while (true)
            {
                var opInfo = GetOperatorInfo(CurrentToken.Value as string);

                if (opInfo == null || opInfo.Precedence < parentPrecedence)
                    break;

                var op = Consume(TokenType.OPERATOR);

                var nextPrecedence = opInfo.Associativity == Associativity.Left ? opInfo.Precedence + 1 : opInfo.Precedence;
                var right = ParseBinaryExpr(nextPrecedence);

                leftHand = new BinaryExpression(leftHand, right, op.Value as string);
            }

            return leftHand;
        }

        public ConditionOperator GetCondOp(string value)
        {
            switch (value)
            {
                case ">":
                    return ConditionOperator.GT;
                case "<":
                    return ConditionOperator.LT;
                case ">=":
                    return ConditionOperator.GE;
                case "<=":
                    return ConditionOperator.LE;
                case "==":
                    return ConditionOperator.EQEQ;
                case "!=":
                    return ConditionOperator.NEQ;
                default:
                    throw new TokenMismatchException("Expected conditional operator for if statement, instead got " + value);
            }
        }

        public StructMemberSetStatement ParseStructMemberSet()
        {
            var accessee = ParseExpression();

            if(accessee is not StructMemberAccessExpr)
            {
                throw new ParserException("Expected struct member access, when setting a struct member to a value. Got "+accessee.GetType().Name);
            }

            Consume(TokenType.ARROW);
            var value = ParseExpression();

            return new StructMemberSetStatement((StructMemberAccessExpr)accessee, value);
        }

        public StructDeclarationStatement ParseStructDecl()
        {
            var name = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.ARROW);
            Consume(TokenType.STRUCT); // IDENTIFIER -> struct

            Consume(TokenType.SYMBOL, "{");

            List<Parameter> members = new List<Parameter>();

            while (!CurrentToken.Value.Equals("}"))
            {
                members.Add(ParseParameter());
            }

            if (members.Count < 1)
                throw new ParserException("A struct must have at least 1 member");

            Consume(TokenType.SYMBOL, "}");

            Structs.Add(name);

            return new StructDeclarationStatement(name, members);
        }

        public IfStatement ParseIfStmt()
        {
            Consume(TokenType.IF);
            Consume(TokenType.SYMBOL, "(");

            // TO-DO: Make this use binary expressions instead.
            Expression condA = ParseExpression();
            var condOp = GetCondOp((string)Consume(TokenType.OPERATOR).Value);
            Expression condB = ParseExpression();

            Consume(TokenType.SYMBOL, ")");

            var trueBlock = ParseBlockStmt();
            BlockStatement elseBlock = null;

            if (CurrentToken.Type == TokenType.ELSE)
            {
                Consume(TokenType.ELSE);
                elseBlock = ParseBlockStmt();
            }

            return new IfStatement(condA, condB, condOp, trueBlock, elseBlock);
        }

        public FunctionDeclarationStatement ParseFnDeclStmt()
        {
            FunctionDeclarationStatement fnDeclStmt = new FunctionDeclarationStatement();
            Scopes.Enqueue(new Scope());
            Consume(TokenType.FUNC);
            fnDeclStmt.Name = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.SYMBOL, "(");

            while ((string)CurrentToken.Value != ")")
            {
                var p = ParseParameter();
                fnDeclStmt.Parameters.Add(p);
                Scopes.Peek().Variables.Add(p.Name);
                if ((string)CurrentToken.Value == ",")
                {
                    Consume(TokenType.SYMBOL, ",");
                }
            }

            Consume(TokenType.SYMBOL, ")");

            Consume(TokenType.ARROW);

            fnDeclStmt.ReturnType = ParseOCType();

            Functions.Add(fnDeclStmt.Name);

            fnDeclStmt.Block = ParseBlockStmt();
            Scopes.Dequeue();
            return fnDeclStmt;

        }

        public Parameter ParseParameter()
        {
            var name = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.SYMBOL, ":");
            var type = ParseOCType();

            return new Parameter(name, type);
        }

        public OCType ParseOCType()
        {
            var typeName = (string)Consume(TokenType.IDENTIFIER).Value;

            if (CurrentToken.Type == TokenType.SYMBOL && (string)CurrentToken.Value == "*")
            {
                return ParsePointerType(new IdentifierType(typeName));
            }

            return new IdentifierType(typeName);
        }

        public OCType ParsePointerType(OCType of)
        {
            Advance();
            if (CurrentToken.Value.Equals("*"))
                return new PointerType(ParsePointerType(of));
            else
                return new PointerType(of);
        }

        public VariableDeclarationStatement ParseVarDeclStmt()
        {
            Consume(TokenType.LET);
            var name = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.SYMBOL, ":");
            var type = ParseOCType();
            Consume(TokenType.ARROW);
            var primaryVal = ParseExpression();

            Scopes.Peek().Variables.Add(name);

            return new VariableDeclarationStatement(name, primaryVal, type);
        }

        public VariableSetStatement ParseVarSetStmt()
        {
            var varName = (string)Consume(TokenType.IDENTIFIER).Value;
            Consume(TokenType.ARROW);
            var varVal = ParseExpression();
            return new VariableSetStatement(varName, varVal);
        }

        public ReturnStatement ParseReturnStmt()
        {
            Consume(TokenType.RETURN);
            return new ReturnStatement(ParseExpression());
        }

        public Statement ParseStatement()
        {
            switch (CurrentToken.Type)
            {
                case TokenType.FUNC:
                    return ParseFnDeclStmt();
                case TokenType.IDENTIFIER:
                    if (Peek().Type == TokenType.ARROW && Peek().Value.Equals("->") && Peek(2).Type == TokenType.IDENTIFIER)
                        return ParseStructMemberSet();
                    if (Scopes.Peek().Variables.Contains(CurrentToken.Value))
                        return ParseVarSetStmt();
                    if (Peek().Type == TokenType.ARROW)
                        return ParseStructDecl();
                    return ParseExprStmt();
                case TokenType.LET:
                    return ParseVarDeclStmt();
                case TokenType.IF:
                    return ParseIfStmt();
                case TokenType.RETURN:
                    return ParseReturnStmt();
            }
            throw new ParserException("Could not parse statement");
        }

        public BlockStatement ParseBlockStmt()
        {
            List<Statement> statements = new List<Statement>();
            Consume(TokenType.SYMBOL, "{");
            while ((string)CurrentToken.Value != "}")
            {
                statements.Add(ParseStatement());
            }
            Consume(TokenType.SYMBOL, "}");
            return new BlockStatement(statements);
        }

        public List<Statement> ParseTokens(List<Token> tokens)
        {
            List<Statement> result = new List<Statement>();
            Scopes.Enqueue(new Scope());
            Functions.Add("print"); // add the print function, since it's built-in, and otherwise would cause issues.
            Feed = tokens;

            CurrentToken = Feed[IndexAt];

            while (CurrentToken.Type != TokenType.EOF)
            {
                CurrentToken = Feed[IndexAt];
                result.Add(ParseStatement());

                if (IndexAt + 2 > Feed.Count)
                    break;
            }

            return result;
        }

        public ExpressionStatement ParseExprStmt()
        {
            var expr = ParseExpression();
            return new ExpressionStatement(expr);
        }

        public ReferenceExpr ParseRefExpr()
        {
            Consume(TokenType.OPERATOR, "&");
            return new ReferenceExpr(ParseExpression());
        }

        public Expression ParseExpr()
        {
            switch (CurrentToken.Type)
            {
                case TokenType.OPERATOR:
                    if(CurrentToken.Value.Equals("&"))
                    {
                        return ParseRefExpr();
                    }
                    break;
                case TokenType.STRING:
                    return ParseStrLitExpr();
                case TokenType.INTEGER:
                    return ParseIntLitExpr();
                case TokenType.IDENTIFIER:
                    if (Functions.Contains((string)CurrentToken.Value))
                        return ParseInvFuncExpr();
                    else if (Scopes.Peek().Variables.Contains(CurrentToken.Value))
                    {
                        if(Peek().Type == TokenType.ARROW && Peek().Value.Equals("->"))
                        {
                            return ParseStructMemberAcc(ParseVariableExpr());
                        }
                        if(!Peek().Value.Equals("->"))
                            return ParseVariableExpr();
                    }
                    break;
                        
                case TokenType.SYMBOL:
                    if (CurrentToken.Value.Equals("["))
                    {
                        return ParseStrObjCrExpr();
                    }
                    break;
            }
            throw new ParserException($"Expression expected, but not found. At around line {CurrentToken.lineAt}, col {CurrentToken.colAt}. (Current Token: {CurrentToken.Value})");
        }

        public Expression ParseExpression()
        {
            return ParseBinaryExpr();
        }

        #endregion

        #region Feed Management Methods

        private void ValidateIndex(int index)
        {
            if (index >= Feed.Count || index < 0)
                throw new ParserException($"[PARSER]: Index({index}) is out of bounds of the token feed.");
        }

        public Token Peek()
        {
            ValidateIndex(IndexAt + 1);
            return Feed[IndexAt + 1];
        }

        public Token Peek(int n)
        {
            ValidateIndex(IndexAt + n);
            return Feed[IndexAt + n];
        }

        public Token Before()
        {
            ValidateIndex(IndexAt - 1);
            return Feed[IndexAt - 1];
        }

        public Token Advance()
        {
            ValidateIndex(IndexAt + 1);
            CurrentToken = Feed[IndexAt + 1];
            IndexAt++;
            return CurrentToken;
        }

        public Token Consume(TokenType type)
        {
            var conToken = CurrentToken;
            if (conToken != null)
            {
                if (conToken.Type == type)
                {
                    Advance();
                    return conToken;
                }
                else
                {
                    throw new TokenMismatchException($"Expected {type}, got {conToken.Type} at line {conToken.lineAt}, col {conToken.colAt}.");
                }
            }
            else
            {
                throw new ParserException("[PARSER]: General fault, Peek gave null value");
            }
        }

        public void Consume(TokenType type, object value)
        {
            var conToken = CurrentToken;
            if (conToken != null)
            {
                if (conToken.Type == type && conToken.Value.Equals(value))
                {
                    Advance();
                }
                else
                {
                    throw new TokenMismatchException($"Expected \"{value}\", got {conToken.Type} \"{conToken.Value}\" at line {conToken.lineAt}, col {conToken.colAt}.");
                }
            }
            else
            {
                throw new ParserException("[PARSER]: General fault, Peek gave null value");
            }
        }
        #endregion
    }

    public class ParserException : Exception
    {

        public ParserException() { }

        public ParserException(string? message) : base(message)
        {
        }
    }

    public class TokenMismatchException : Exception
    {

        public TokenMismatchException() { }

        public TokenMismatchException(string? message) : base(message)
        {
        }
    }
}

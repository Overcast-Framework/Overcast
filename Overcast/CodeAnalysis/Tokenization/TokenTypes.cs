using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Tokenization
{
    public enum TokenType : byte
    {
        UNKNOWN = 0,
        EOF = 1,
        [TokenMatch("\".*?\"")] STRING,
        [TokenMatch("[-+]?[0-9]+")] INTEGER,
        [TokenMatch(@"[-+]?([0-9]*\.[0-9]+|[0-9]+\.[0-9]*)([eE][-+]?[0-9]+)?")] FLOAT,
        [TokenMatch(@"\s+")] WHITESPACE,
        [TokenMatch("include")] INCLUDE,
        [TokenMatch("func")] FUNC,
        [TokenMatch("let")] LET,
        [TokenMatch("if")] IF,
        [TokenMatch("else")] ELSE,
        [TokenMatch("struct")] STRUCT,
        [TokenMatch("return")] RETURN,
        [TokenMatch(@"(->|<-)")] ARROW,
        [TokenMatch(@"(!=|==|>=|<=|>|<|&&|\|\||\||\^|&|>>|<<|!|\+|-|\+\+|--|\+=|-=|/)")] OPERATOR,
        [TokenMatch(@"[A-Za-z_][A-Za-z0-9_]*")] IDENTIFIER,
        [TokenMatch(@"[@#$%*()\[\]{}\=|\\:'\"",./?]")] SYMBOL,
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public enum Associativity
    {
        Left,
        Right
    }

    public class OperatorInfo
    {
        public int Precedence { get; }
        public Associativity Associativity { get; }

        public OperatorInfo(int precedence, Associativity associativity)
        {
            Precedence = precedence;
            Associativity = associativity;
        }
    }

    public static class Operators
    {
        public static readonly Dictionary<string, OperatorInfo> OperatorPrecedence = new()
    {
        { "+", new OperatorInfo(1, Associativity.Left) },
        { "-", new OperatorInfo(1, Associativity.Left) },
        { "*", new OperatorInfo(2, Associativity.Left) },
        { "/", new OperatorInfo(2, Associativity.Left) },
    };
    }
}

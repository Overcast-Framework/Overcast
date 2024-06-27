using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class IntLiteralExpr : Expression
    {
        public int Value { get; set; }

        public IntLiteralExpr(int value)
        {
            Value = value;
        }
    }
}

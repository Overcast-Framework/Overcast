using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class StringLiteralExpr : Expression
    {
        public string Value;

        public StringLiteralExpr(string value)
        {
            Value = value;
        }

        public StringLiteralExpr()
        {
        }
    }
}

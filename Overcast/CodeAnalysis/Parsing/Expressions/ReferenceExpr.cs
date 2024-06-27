using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class ReferenceExpr : Expression
    {
        public Expression Value { get; set; }

        public ReferenceExpr(Expression value)
        {
            Value = value;
        }
    }
}

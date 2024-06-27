using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class ReturnStatement : Statement
    {
        public Expression Value { get; set; }

        public ReturnStatement(Expression value)
        {
            Value = value;
        }
    }
}

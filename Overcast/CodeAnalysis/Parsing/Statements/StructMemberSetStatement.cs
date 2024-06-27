using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class StructMemberSetStatement : Statement
    {
        public StructMemberAccessExpr Accessee { get; set; }
        public Expression Value { get; set; }

        public StructMemberSetStatement(StructMemberAccessExpr accessee, Expression value)
        {
            Accessee = accessee;
            Value = value;
        }
    }
}

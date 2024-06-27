using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class ExpressionStatement : Statement
    {
        public Expression Expr;

        public ExpressionStatement(Expression expr)
        {
            Expr = expr;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{

    public class BinaryExpression : Expression
    {
        public Expression primaryA;
        public Expression primaryB;
        public string _operator;

        public BinaryExpression(Expression primaryA, Expression primaryB, string @operator)
        {
            this.primaryA = primaryA;
            this.primaryB = primaryB;
            _operator = @operator;
        }
    }
}

using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public enum ConditionOperator
    {
        GT, LT, GE, LE, EQEQ, NEQ
    }

    public class IfStatement : Statement
    {
        public Expression conditandA;
        public Expression conditandB;
        public ConditionOperator _operator;

        public BlockStatement trueBlock;
        public BlockStatement elseBlock;

        public IfStatement(Expression conditandA, Expression conditandB, ConditionOperator @operator, BlockStatement trueBlock, BlockStatement elseBlock)
        {
            this.conditandA = conditandA;
            this.conditandB = conditandB;
            _operator = @operator;
            this.trueBlock = trueBlock;
            this.elseBlock = elseBlock;
        }
    }
}

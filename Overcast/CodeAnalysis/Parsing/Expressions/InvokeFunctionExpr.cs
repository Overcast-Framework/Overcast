using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class InvokeFunctionExpr : Expression
    {
        public string FunctionName { get; set; }
        public List<Expression> Arguments { get; set; }

        public InvokeFunctionExpr(string functionName, List<Expression> arguments)
        {
            FunctionName = functionName;
            Arguments = arguments;
        }
    }
}

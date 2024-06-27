using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class VariableExpr : Expression
    {
        public string Name;

        public VariableExpr(string name)
        {
            Name = name;
        }
    }
}

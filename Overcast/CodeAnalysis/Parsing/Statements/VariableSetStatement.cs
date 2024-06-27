using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class VariableSetStatement : Statement
    {
        public string Name { get; set; }
        public Expression Value { get; set; }

        public VariableSetStatement(string name, Expression value)
        {
            Name = name;
            Value = value;
        }
    }
}

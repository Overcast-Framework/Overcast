using Overcast.CodeAnalysis.Parsing.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class VariableDeclarationStatement : Statement
    {
        public string Name { get; set; }
        public Expression PrimaryValue { get; set; }
        public OCType Type { get; set; }

        public VariableDeclarationStatement(string name, Expression primaryValue, OCType type)
        {
            Name = name;
            PrimaryValue = primaryValue;
            Type = type;
        }
    }
}

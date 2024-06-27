using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class Parameter
    {
        public string Name { get; set; }
        public OCType Type { get; set; }

        public Parameter(string name, OCType type)
        {
            Name = name;
            Type = type;
        }
    }

    public class FunctionDeclarationStatement : Statement
    {
        public List<Parameter> Parameters { get; set; } = new List<Parameter>();
        public OCType ReturnType { get; set; }

        public string Name { get; set; }

        public bool IsPrototype { get; set; }

        public BlockStatement Block { get; set; }

        public FunctionDeclarationStatement(string name, List<Parameter> parameters, OCType returnType)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
        }

        public FunctionDeclarationStatement()
        {
        }
    }
}

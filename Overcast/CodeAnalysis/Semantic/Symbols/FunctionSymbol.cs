using Overcast.CodeAnalysis.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Semantic.Symbols
{
    public class FunctionSymbol : Symbol
    {
        public List<VariableSymbol> Parameters { get; set; }
        public string Name { get; set; }
        public OCType ReturnType { get; set; }
        public bool VArgs { get; set; } = false;

        public FunctionSymbol(List<VariableSymbol> parameters, string name, OCType returnType, bool vArgs) : this(parameters, name, returnType)
        {
            VArgs = vArgs;
        }

        public FunctionSymbol(List<VariableSymbol> parameters, string name, OCType returnType)
        {
            Parameters = parameters;
            Name = name;
            ReturnType = returnType;
        }
    }
}

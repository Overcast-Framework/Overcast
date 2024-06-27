using Overcast.CodeAnalysis.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Semantic.Symbols
{
    public class VariableSymbol : Symbol
    {
        public string Name { get; set; }
        public OCType Type { get; set; }

        public VariableSymbol(string name, OCType type)
        {
            Name = name;
            Type = type;
        }
    }
}

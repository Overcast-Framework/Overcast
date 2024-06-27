using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Parsing.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Semantic.Symbols
{
    public class StructSymbol : Symbol
    {
        public string name;
        public List<Parameter> members;

        public StructSymbol(string name, List<Parameter> members)
        {
            this.name = name;
            this.members = members;
        }
    }
}

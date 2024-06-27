using Overcast.CodeAnalysis.Semantic.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Semantic
{
    public class Scope
    {
        public Dictionary<string, VariableSymbol> Locals = new Dictionary<string, VariableSymbol>();

        public void AddLocal(string name, VariableSymbol variable)
        {
            if (!LocalExists(name))
                Locals.Add(name, variable);
            else
                throw new BinderException("Attempted to redeclare existing symbol");
        }

        public void RemoveLocal(string name) 
        {
            Locals.Remove(name); 
        }

        public bool LocalExists(string name) 
        {
            return Locals.ContainsKey(name); 
        }
    }
}

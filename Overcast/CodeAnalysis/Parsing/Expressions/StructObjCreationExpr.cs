using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class StructObjCreationExpr : Expression
    {
        public string StructName { get; set; }
        public List<Expression> Args { get; set; }

        public StructObjCreationExpr(string structName, List<Expression> args)
        {
            StructName = structName;
            Args = args;
        }
    }
}

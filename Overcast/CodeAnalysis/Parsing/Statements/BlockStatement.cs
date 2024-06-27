using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class BlockStatement : Statement
    {
        public List<Statement> statements;

        public BlockStatement(List<Statement> statements)
        {
            this.statements = statements;
        }
    }
}

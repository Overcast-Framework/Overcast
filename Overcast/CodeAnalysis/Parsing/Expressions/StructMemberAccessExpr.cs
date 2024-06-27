using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Expressions
{
    public class StructMemberAccessExpr : Expression
    {
        public Expression ObjectName { get; set; }
        public string MemberName { get; set; }

        public StructMemberAccessExpr(Expression objectName, string memberName)
        {
            ObjectName = objectName;
            MemberName = memberName;
        }
    }
}

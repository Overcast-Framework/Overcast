using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing.Statements
{
    public class StructDeclarationStatement : Statement
    {
        public string Name { get; set; }
        public List<Parameter> Members { get; set; }
        public Dictionary<string, int> MemberIndexTable { get; set; } = new Dictionary<string, int>();

        public StructDeclarationStatement(string name, List<Parameter> members)
        {
            Name = name;
            Members = members;

            for(int i = 0; i < Members.Count; i++)
                MemberIndexTable.Add(Members[i].Name, i);
        }
    }
}

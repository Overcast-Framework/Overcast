using LLVMSharp.Interop;
using Overcast.CodeAnalysis.Parsing.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.LLVMC.Data
{
    public struct StructData
    {
        public string Name { get; set; }
        public List<Parameter> Properties { get; set; }
        public Dictionary<string, int> Indices { get; set; } = new Dictionary<string, int>();
        public Pointer<LLVMOpaqueType> StructLLVMType { get; set; }

        public StructData(string name, List<Parameter> properties, Pointer<LLVMOpaqueType> structLLVMType)
        {
            Name = name;
            Properties = properties;
            StructLLVMType = structLLVMType;

            int i = 0;
            foreach(var prop in Properties)
            {
                Indices[prop.Name] = i;
                i++;
            }
        }
    }
}

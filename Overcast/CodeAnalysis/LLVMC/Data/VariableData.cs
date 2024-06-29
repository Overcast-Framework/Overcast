using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.LLVMC.Data
{
    public struct VariableData
    {
        public string Name {  get; set; }
        public Pointer<LLVMOpaqueType> Type { get; set; }
        public Pointer<LLVMOpaqueValue> Alloca { get; set; }

        public VariableData(string name, Pointer<LLVMOpaqueType> type, Pointer<LLVMOpaqueValue> alloca)
        {
            Name = name;
            Type = type;
            Alloca = alloca;
        }
    }
}

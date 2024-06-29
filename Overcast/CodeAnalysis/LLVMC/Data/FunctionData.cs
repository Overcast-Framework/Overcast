using LLVMSharp.Interop;
using Overcast.CodeAnalysis.Parsing;
using Overcast.CodeAnalysis.Parsing.Statements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.LLVMC.Data
{
    public struct FunctionData
    {
        public string Name;
        public List<Parameter> Parameters;
        public OCType ReturnTypeOC;
        public Pointer<LLVMOpaqueType> ReturnType;
        public Pointer<LLVMOpaqueType> FunctionType;
        public Pointer<LLVMOpaqueValue> FunctionValue;

        public FunctionData(string name, List<Parameter> parameters, Pointer<LLVMOpaqueType> returnType, Pointer<LLVMOpaqueType> functionType, Pointer<LLVMOpaqueValue> functionValue)
        {
            Name = name;
            Parameters = parameters;
            ReturnType = returnType;
            FunctionType = functionType;
            FunctionValue = functionValue;
        }
    }
}

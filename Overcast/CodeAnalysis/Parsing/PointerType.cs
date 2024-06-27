using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing
{
    public class PointerType : OCType
    {
        public OCType OfType;

        public unsafe override LLVMOpaqueType* LLVMType()
        {
            if (OfType == null)
            {
                throw new Exception("OfType is null in PointerType.");
            }
            return LLVM.PointerType(OfType.LLVMType(), 0);
        }

        public override unsafe IdentifierType GetBaseType()
        {
            return OfType.GetBaseType();
        }

        public override string? ToString()
        {
            return OfType.ToString() + "*";
        }

        public override bool Equals(object? obj)
        {
            if(obj is PointerType)
            {
                var castedObj = (PointerType)obj;
                if (castedObj.ToString() == ToString())
                {
                    return true;
                }
            }
            return false;
        }

        public PointerType(OCType ofType)
        {
            OfType = ofType;
        }
    }
}

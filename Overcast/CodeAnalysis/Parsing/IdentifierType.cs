using LLVMSharp.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Parsing
{
    public class OCType 
    {
        public unsafe virtual LLVMOpaqueType* LLVMType()
        {
            throw new NotImplementedException("Cannot use LLVMType on base class OCType");
        }
        public virtual IdentifierType GetBaseType()
        {
            throw new NotImplementedException("Cannot use GetBaseType on base class OCType");
        }
    }

    public class IdentifierType : OCType
    {
        public string Name { get; set; }

        /// <summary>
        /// Gets the LLVM type, if the type is a built-in type, and not a custom-declared one.
        /// </summary>

        public static IdentifierType String = new IdentifierType("string");
        public static IdentifierType Void = new IdentifierType("void");
        public static IdentifierType Integer = new IdentifierType("int");
        public static IdentifierType Float = new IdentifierType("float");
        public static IdentifierType Any = new IdentifierType("any");

        public override IdentifierType GetBaseType()
        {
            return this;
        }

        public unsafe override LLVMOpaqueType* LLVMType()
        { 
            switch (Name)
            {
                case "void":
                    return LLVM.VoidType();
                case "int":
                    return LLVM.Int32Type();
                case "int64":
                    return LLVM.Int64Type();
                case "string":
                    return LLVM.PointerType(LLVM.Int8Type(), 0);
                case "float":
                    return LLVM.FloatType();
                case "bool":
                    return LLVM.Int1Type();
                default:
                    throw new ParserException("Type is not a built-in type or is incorrectly spelled.");
            }
        }

        public override bool Equals(object? obj)
        {
            if(obj is IdentifierType)
            {
                IdentifierType other = (IdentifierType)obj;

                if(Name == "any")
                {
                    return true;
                }

                return other.Name == Name;
            }
            return false; 
        }

        public override string? ToString()
        {
            return Name;
        }

        public IdentifierType(string name)
        {
            Name = name;
        }
    }
}

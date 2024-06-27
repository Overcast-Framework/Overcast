using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Overcast
{
    public static class OCHelper
    {
        public static unsafe sbyte* StrToSByte(string str)
        {
            IntPtr ptr = Marshal.StringToHGlobalAnsi(str);
            sbyte* sby = (sbyte*)ptr;
            return sby;
        }

        public static unsafe string SByteToStr(sbyte* str)
        {
            string ptr = Marshal.PtrToStringUTF8((IntPtr)str);

            return ptr;
        }

        public static unsafe string SByteToStrA(sbyte* str)
        {
            string ptr = Marshal.PtrToStringAnsi((IntPtr)str);

            return ptr;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Tokenization
{
    [System.AttributeUsage(System.AttributeTargets.All)
    ]
    internal class TokenMatchAttribute : Attribute
    {
        public string regex { get; }

        public TokenMatchAttribute(string regex)
        {
            this.regex = regex; 
        }
    }
}

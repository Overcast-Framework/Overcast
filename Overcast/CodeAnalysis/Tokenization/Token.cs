using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Tokenization
{
    public class Token
    {
        public TokenType Type;
        public object Value;

        public int lineAt, colAt;

        public Token(TokenType type, object value)
        {
            Type = type;
            Value = value;
        }

        public Token(TokenType type, object value, int lineAt, int colAt) : this(type, value)
        {
            this.lineAt = lineAt;
            this.colAt = colAt;
        }
    }
}

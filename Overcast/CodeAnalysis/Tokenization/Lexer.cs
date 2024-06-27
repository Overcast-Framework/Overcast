using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Overcast.CodeAnalysis.Tokenization
{
    public class Lexer
    {
        int position = 0;
        int line = 0, col = 0;

        public List<Token> MatchTokens(string str)
        {
            List<Token> tokens = new List<Token>();

            position = 0;
            line = 1; 
            col = 0;

            while (position < str.Length)
            {
                TokenType tokType = TokenType.UNKNOWN;
                object value = null;

                // Skip whitespace
                Match whitespaceMatch = Regex.Match(str.Substring(position), @"\s+");
                if (whitespaceMatch.Success && whitespaceMatch.Index == 0)
                {
                    foreach (var ch in whitespaceMatch.Value)
                    {
                        if(ch == '\n')
                        {
                            line++; col = 0;
                        }
                    }

                    col += whitespaceMatch.Length;
                    position += whitespaceMatch.Length;
                    continue;
                }

                Match commentMatch = Regex.Match(str.Substring(position), @"^(//.*|/\*[\s\S]*?\*/)");
                if (commentMatch.Success)
                {
                    if (commentMatch.Value.StartsWith("//"))
                    {
                        // Single-line comment, skip to end of line
                        int endIndex = str.IndexOf('\n', position + 2);
                        if (endIndex == -1)
                        {
                            position = str.Length; // End of string
                        }
                        else
                        {
                            position = endIndex + 1;
                            line++;
                            col = 0;
                        }
                    }
                    else if (commentMatch.Value.StartsWith("/*"))
                    {
                        // Multi-line comment, skip to end of comment
                        int endIndex = str.IndexOf("*/", position + 2);
                        if (endIndex == -1)
                        {
                            throw new Exception("Unterminated multi-line comment.");
                        }
                        else
                        {
                            int newLines = CountNewLines(commentMatch.Value);
                            line += newLines;
                            col = newLines > 0 ? commentMatch.Value.Length - commentMatch.Value.LastIndexOf('\n') : col + commentMatch.Length;
                            position = endIndex + 2;
                        }
                    }
                    continue;
                }

                foreach (var type in Enum.GetValues(typeof(TokenType)))
                {
                    var eField = typeof(TokenType).GetField(type.ToString());
                    var attrib = eField.GetCustomAttribute<TokenMatchAttribute>();

                    if (attrib != null)
                    {
                        var pattern = attrib.regex;

                        if (!string.IsNullOrEmpty(pattern))
                        {
                            Match match = Regex.Match(str.Substring(position), pattern);
                            if (match.Success && match.Index == 0)
                            {
                                tokType = (TokenType)type;
                                value = match.Value;
                                if(tokType == TokenType.INTEGER)
                                    value = int.Parse(match.Value);
                                position += match.Length;
                                col += match.Length;
                                break;
                            }
                        }
                    }
                }

                if (tokType == TokenType.UNKNOWN)
                    throw new Exception($"Unknown character {str[position]}({(int)str[position]}) at line {line} col {col}");

                tokens.Add(new Token(tokType, value, line, col));
            }

            tokens.Add(new Token(TokenType.EOF, "<EOF>"));

            return tokens;
        }
        private int CountNewLines(string str)
        {
            return str.Split('\n').Length - 1;
        }
    }
}

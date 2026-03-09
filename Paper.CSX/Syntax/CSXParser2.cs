using System.Text;

namespace Paper.CSX.Syntax
{
    /// <summary>
    /// Minimal JSX-like parser for Paper CSX.
    /// - Finds the first element in the file and parses a nested element tree.
    /// - Attribute values support quoted strings or balanced-brace expressions: { ... }.
    /// - Children support text, nested elements, and balanced-brace expressions.
    /// This parser intentionally treats the content inside { ... } as an opaque code string.
    /// </summary>
    public sealed class CSXParser2
    {
        private readonly string _src;
        private int _i;

        public CSXParser2(string source)
        {
            _src = source ?? "";
            _i = 0;
        }

        /// <summary>Number of source characters consumed after the last Parse call.</summary>
        public int Position => _i;

        public CSXElement ParseFirstElement()
        {
            // Skip until first '<'
            while (_i < _src.Length && _src[_i] != '<') _i++;
            if (_i >= _src.Length) throw new Exception("No CSX element found.");
            return ParseElement();
        }

        private CSXElement ParseElement()
        {
            int start = _i;
            Expect('<');
            if (Peek('/') )
                throw new Exception("Unexpected closing tag.");

            SkipWs();
            string name = ReadIdentifier();
            if (name.Length == 0) throw new Exception("Expected tag name.");

            var attrs = new List<CSXAttribute>();
            while (_i < _src.Length)
            {
                SkipWs();
                if (Peek('/') || Peek('>')) break;

                int attrStart = _i;
                string attrName = ReadIdentifier();
                if (attrName.Length == 0) throw new Exception("Expected attribute name.");

                SkipWs();
                CSXAttributeValue attrVal = new CSXBareValue("true", new TextSpan(_i, 0));
                if (Peek('='))
                {
                    _i++; // '='
                    SkipWs();
                    attrVal = ReadAttributeValue();
                }

                attrs.Add(new CSXAttribute(attrName, attrVal, new TextSpan(attrStart, _i - attrStart)));
            }

            bool selfClosing = false;
            SkipWs();
            if (Peek('/'))
            {
                selfClosing = true;
                _i++;
            }
            Expect('>');

            if (selfClosing)
            {
                return new CSXElement(name, attrs, Array.Empty<CSXChild>(), new TextSpan(start, _i - start));
            }

            var children = new List<CSXChild>();
            while (_i < _src.Length)
            {
                if (Peek('<') && Peek("</"))
                    break;

                if (Peek('<'))
                {
                    // Nested element
                    var el = ParseElement();
                    children.Add(new CSXChildElement(el));
                    continue;
                }

                if (Peek('{'))
                {
                    int exprStart = _i;
                    string expr = ReadBalancedBraces();
                    children.Add(new CSXExpression(expr, new TextSpan(exprStart, _i - exprStart)));
                    continue;
                }

                // Text node
                int textStart = _i;
                var sb = new StringBuilder();
                while (_i < _src.Length && !Peek('<') && !Peek('{'))
                {
                    sb.Append(_src[_i]);
                    _i++;
                }
                var text = sb.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    children.Add(new CSXText(text, new TextSpan(textStart, _i - textStart)));
            }

            // Closing tag
            Expect('<');
            Expect('/');
            SkipWs();
            string endName = ReadIdentifier();
            if (!string.Equals(endName, name, StringComparison.Ordinal))
                throw new Exception($"Mismatched closing tag. Expected </{name}> but found </{endName}>.");
            SkipWs();
            Expect('>');

            return new CSXElement(name, attrs, children, new TextSpan(start, _i - start));
        }

        private CSXAttributeValue ReadAttributeValue()
        {
            int start = _i;

            if (Peek('{'))
            {
                string code = ReadBalancedBraces();
                return new CSXExpressionValue(code, new TextSpan(start, _i - start));
            }

            if (Peek('"') || Peek('\''))
            {
                char quote = _src[_i++];
                int contentStart = _i;
                var sb = new StringBuilder();
                while (_i < _src.Length && _src[_i] != quote)
                {
                    if (_src[_i] == '\\' && _i + 1 < _src.Length)
                    {
                        sb.Append(_src[_i + 1]);
                        _i += 2;
                        continue;
                    }
                    sb.Append(_src[_i]);
                    _i++;
                }
                if (_i >= _src.Length) throw new Exception("Unterminated string literal.");
                _i++; // closing quote
                return new CSXStringValue(sb.ToString(), new TextSpan(start, _i - start));
            }

            // Bare token until whitespace or tag end.
            string bare = ReadBareToken();
            return new CSXBareValue(bare, new TextSpan(start, _i - start));
        }

        private string ReadBareToken()
        {
            var sb = new StringBuilder();
            while (_i < _src.Length && !char.IsWhiteSpace(_src[_i]) && _src[_i] != '>' && _src[_i] != '/')
            {
                sb.Append(_src[_i]);
                _i++;
            }
            return sb.ToString();
        }

        private string ReadBalancedBraces()
        {
            // Reads {...} and returns inner content string (without the outer braces)
            Expect('{');
            int depth = 1;
            int startInner = _i;

            bool inString = false;
            char stringQuote = '\0';

            while (_i < _src.Length && depth > 0)
            {
                char c = _src[_i];

                if (inString)
                {
                    if (c == '\\' && _i + 1 < _src.Length)
                    {
                        _i += 2;
                        continue;
                    }
                    if (c == stringQuote)
                    {
                        inString = false;
                        stringQuote = '\0';
                    }
                    _i++;
                    continue;
                }

                if (c == '\'' || c == '"')
                {
                    inString = true;
                    stringQuote = c;
                    _i++;
                    continue;
                }

                if (c == '{') { depth++; _i++; continue; }
                if (c == '}') { depth--; _i++; continue; }
                _i++;
            }

            if (depth != 0) throw new Exception("Unterminated { ... } expression.");

            int endInner = _i - 1; // position before the final '}'
            return _src.Substring(startInner, Math.Max(0, endInner - startInner)).Trim();
        }

        private string ReadIdentifier()
        {
            int start = _i;
            while (_i < _src.Length)
            {
                char c = _src[_i];
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-')
                {
                    _i++;
                    continue;
                }
                break;
            }
            return _src.Substring(start, _i - start);
        }

        private void SkipWs()
        {
            while (_i < _src.Length && char.IsWhiteSpace(_src[_i])) _i++;
        }

        private bool Peek(char c) => _i < _src.Length && _src[_i] == c;

        private bool Peek(string s)
        {
            if (_i + s.Length > _src.Length) return false;
            for (int j = 0; j < s.Length; j++)
                if (_src[_i + j] != s[j]) return false;
            return true;
        }

        private void Expect(char c)
        {
            if (_i >= _src.Length || _src[_i] != c)
                throw new Exception($"Expected '{c}'.");
            _i++;
        }
    }
}


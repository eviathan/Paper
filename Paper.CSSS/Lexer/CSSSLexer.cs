namespace Paper.CSSS.Lexer
{
    /// <summary>
    /// Tokenises CSSS source text into a flat list of <see cref="Token"/>s.
    /// </summary>
    internal sealed class CSSSLexer
    {
        private readonly string _src;
        private int    _pos;
        private int    _line   = 1;
        private int    _column = 1;

        public CSSSLexer(string source) { _src = source; }

        public List<Token> Tokenise()
        {
            var tokens = new List<Token>();
            bool lastWasWs = true; // suppress leading whitespace
            while (!AtEnd())
            {
                var tok = NextToken();
                if (tok.Kind == TokenKind.Comment) continue;
                bool isWs = tok.Kind is TokenKind.Whitespace or TokenKind.Newline;
                if (isWs)
                {
                    // Collapse consecutive whitespace/newlines into a single space token
                    if (!lastWasWs)
                        tokens.Add(new Token(TokenKind.Whitespace, " ", tok.Line, tok.Column));
                    lastWasWs = true;
                }
                else
                {
                    tokens.Add(tok);
                    lastWasWs = false;
                }
            }
            tokens.Add(Tok(TokenKind.EOF, ""));
            return tokens;
        }

        // ── Core ──────────────────────────────────────────────────────────────

        private Token NextToken()
        {
            int startLine = _line, startCol = _column;
            char c = Peek();

            // Whitespace
            if (c == '\n') { Advance(); return Tok(TokenKind.Newline, "\n", startLine, startCol); }
            if (char.IsWhiteSpace(c))
            {
                while (!AtEnd() && char.IsWhiteSpace(Peek()) && Peek() != '\n') Advance();
                return Tok(TokenKind.Whitespace, " ", startLine, startCol);
            }

            // Comments
            if (c == '/' && PeekAt(1) == '/')
            {
                while (!AtEnd() && Peek() != '\n') Advance();
                return Tok(TokenKind.Comment, "", startLine, startCol);
            }
            if (c == '/' && PeekAt(1) == '*')
            {
                Advance(); Advance(); // consume /*
                while (!AtEnd() && !(Peek() == '*' && PeekAt(1) == '/'))
                    Advance();
                if (!AtEnd()) { Advance(); Advance(); } // consume */
                return Tok(TokenKind.Comment, "", startLine, startCol);
            }

            // Variables: $name
            if (c == '$')
            {
                Advance();
                string name = ReadIdent();
                return Tok(TokenKind.Variable, name, startLine, startCol);
            }

            // Hex colour: #rrggbb / #rgb / #rrggbbaa — include # in the token value so
            // CollectUntil produces the full "#rrggbb" string that ParseColour expects.
            if (c == '#' && IsHexStart(PeekAt(1)))
            {
                Advance(); // consume #
                string hex = "#";
                while (!AtEnd() && IsHexChar(Peek())) hex += Advance();
                return Tok(TokenKind.Hash, hex, startLine, startCol);
            }

            // Strings
            if (c == '"' || c == '\'')
            {
                char quote = Advance();
                string s = "";
                while (!AtEnd() && Peek() != quote)
                {
                    if (Peek() == '\\') { Advance(); s += Advance(); }
                    else s += Advance();
                }
                if (!AtEnd()) Advance(); // closing quote
                return Tok(TokenKind.String, s, startLine, startCol);
            }

            // Numbers and dimensions: 12, 3.14, 12px, 50%, 1.5em, 2fr
            if (char.IsAsciiDigit(c) || (c == '.' && char.IsAsciiDigit(PeekAt(1))) ||
                (c == '-' && (char.IsAsciiDigit(PeekAt(1)) || (PeekAt(1) == '.' && char.IsAsciiDigit(PeekAt(2))))))
            {
                string num = "";
                if (c == '-') num += Advance();
                while (!AtEnd() && (char.IsAsciiDigit(Peek()) || Peek() == '.')) num += Advance();

                // Dimension suffix?
                string suffix = "";
                if (!AtEnd() && (char.IsLetter(Peek()) || Peek() == '%'))
                {
                    while (!AtEnd() && (char.IsLetter(Peek()) || Peek() == '%')) suffix += Advance();
                    return Tok(TokenKind.Dimension, num + suffix, startLine, startCol);
                }
                return Tok(TokenKind.Number, num, startLine, startCol);
            }

            // Identifiers (including property names, keywords)
            if (IsIdentStart(c))
            {
                string ident = ReadIdent();
                return Tok(TokenKind.Ident, ident, startLine, startCol);
            }

            // Single-character tokens
            Advance();
            return c switch
            {
                '{'  => Tok(TokenKind.LeftBrace,    "{",  startLine, startCol),
                '}'  => Tok(TokenKind.RightBrace,   "}",  startLine, startCol),
                '('  => Tok(TokenKind.LeftParen,    "(",  startLine, startCol),
                ')'  => Tok(TokenKind.RightParen,   ")",  startLine, startCol),
                '['  => Tok(TokenKind.LeftBracket,  "[",  startLine, startCol),
                ']'  => Tok(TokenKind.RightBracket, "]",  startLine, startCol),
                ':'  => Tok(TokenKind.Colon,         ":",  startLine, startCol),
                ';'  => Tok(TokenKind.Semicolon,     ";",  startLine, startCol),
                ','  => Tok(TokenKind.Comma,          ",",  startLine, startCol),
                '.'  => Tok(TokenKind.Dot,            ".",  startLine, startCol),
                '#'  => Tok(TokenKind.Hash_,          "#",  startLine, startCol),
                '&'  => Tok(TokenKind.Ampersand,      "&",  startLine, startCol),
                '@'  => Tok(TokenKind.At,             "@",  startLine, startCol),
                '!'  => Tok(TokenKind.Bang,           "!",  startLine, startCol),
                '/'  => Tok(TokenKind.Slash,          "/",  startLine, startCol),
                '*'  => Tok(TokenKind.Star,           "*",  startLine, startCol),
                '+'  => Tok(TokenKind.Plus,           "+",  startLine, startCol),
                '-'  => Tok(TokenKind.Minus,          "-",  startLine, startCol),
                '='  => Tok(TokenKind.Equals,         "=",  startLine, startCol),
                '>'  => Tok(TokenKind.Greater,        ">",  startLine, startCol),
                '~'  => Tok(TokenKind.Tilde,          "~",  startLine, startCol),
                _    => Tok(TokenKind.Unknown,         c.ToString(), startLine, startCol),
            };
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private string ReadIdent()
        {
            string s = "";
            // Identifiers can start with a letter, underscore, or hyphen
            while (!AtEnd() && IsIdentChar(Peek()))
                s += Advance();
            return s;
        }

        private bool AtEnd()  => _pos >= _src.Length;
        private char Peek()   => AtEnd() ? '\0' : _src[_pos];
        private char PeekAt(int offset)
        {
            int idx = _pos + offset;
            return idx < _src.Length ? _src[idx] : '\0';
        }

        private char Advance()
        {
            char c = _src[_pos++];
            if (c == '\n') { _line++; _column = 1; }
            else _column++;
            return c;
        }

        private Token Tok(TokenKind kind, string value) =>
            new Token(kind, value, _line, _column);
        private Token Tok(TokenKind kind, string value, int line, int col) =>
            new Token(kind, value, line, col);

        private static bool IsIdentStart(char c) =>
            char.IsLetter(c) || c == '_' || c == '-';

        private static bool IsIdentChar(char c) =>
            char.IsLetterOrDigit(c) || c == '_' || c == '-';

        private static bool IsHexStart(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static bool IsHexChar(char c) =>
            (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
    }
}

namespace Paper.CSSS.Lexer
{
    internal enum TokenKind
    {
        // Literals
        Ident,          // foo, background-color
        String,         // "hello" or 'hello'
        Number,         // 42, 3.14
        Dimension,      // 12px, 50%, 1.5em, 2fr
        Hash,           // #ff0000
        Variable,       // $primary-color

        // Punctuation
        Colon,          // :
        Semicolon,      // ;
        Comma,          // ,
        Dot,            // .
        Hash_,          // # (before ident in selectors — distinct from Hash colour)
        Ampersand,      // &
        At,             // @
        Bang,           // !
        Slash,          // /
        Star,           // *
        Plus,           // +
        Minus,          // -
        Equals,         // =
        Greater,        // >
        Tilde,          // ~

        LeftBrace,      // {
        RightBrace,     // }
        LeftParen,      // (
        RightParen,     // )
        LeftBracket,    // [
        RightBracket,   // ]

        // Whitespace / layout
        Whitespace,
        Newline,

        // Special
        Comment,        // /* ... */ or // ...
        EOF,
        Unknown,
    }

    internal readonly struct Token
    {
        public TokenKind Kind { get; }
        public string Value { get; }
        public int Line { get; }
        public int Column { get; }

        public Token(TokenKind kind, string value, int line, int column)
        {
            Kind = kind;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"{Kind}({Value}) @{Line}:{Column}";
    }
}

using Paper.CSSS.Lexer;

namespace Paper.CSSS.Parser
{
    /// <summary>
    /// Exception thrown during CSSS parsing.
    /// </summary>
    public class CSSSParseException : Exception
    {
        public int Line { get; }
        public int Column { get; }

        public CSSSParseException(string message, int line = 1, int column = 1)
            : base(message)
        {
            Line = line;
            Column = column;
        }
    }

    /// <summary>
    /// Parses a flat list of <see cref="Token"/>s into a <see cref="CSSSStylesheet"/> AST.
    /// Handles: selectors, declarations, nested rules, variables, @mixin, @include, @media.
    /// </summary>
    internal sealed class CSSSParser
    {
        private readonly List<Token> _tokens;
        private int _pos;

        public CSSSParser(List<Token> tokens) { _tokens = tokens; }

        public CSSSStylesheet Parse()
        {
            var sheet = new CSSSStylesheet();
            while (true)
            {
                SkipWs();
                if (AtEnd()) break;
                sheet.Statements.Add(ParseStatement());
            }
            return sheet;
        }

        // ── Statement dispatch ────────────────────────────────────────────────

        private CSSSStatement ParseStatement()
        {
            SkipWs();
            var tok = PeekRaw();

            if (tok.Kind == TokenKind.Variable)
                return ParseVariableDecl();

            if (tok.Kind == TokenKind.At)
                return ParseAtRule();

            if (IsDeclarationStart())
                return ParseDeclaration();

            return ParseRule();
        }

        private bool IsDeclarationStart()
        {
            // A declaration looks like:   ident COLON value SEMICOLON
            // A rule selector looks like: ident { ...   or  .class { ...  or  &:hover { ...
            int i = _pos;
            while (i < _tokens.Count && _tokens[i].Kind is TokenKind.Whitespace or TokenKind.Newline) i++;
            if (i >= _tokens.Count) return false;
            var first = _tokens[i].Kind;
            if (first != TokenKind.Ident && first != TokenKind.Minus) return false;

            while (i < _tokens.Count)
            {
                var tokenKind = _tokens[i].Kind;
                if (tokenKind == TokenKind.Colon)     return true;
                if (tokenKind == TokenKind.LeftBrace) return false;
                if (tokenKind == TokenKind.Semicolon) return true;
                if (tokenKind == TokenKind.EOF)       return false;
                i++;
            }
            return false;
        }

        // ── Variable ─────────────────────────────────────────────────────────

        private CSSSVariableDecl ParseVariableDecl()
        {
            string name = Consume(TokenKind.Variable).Value;
            Consume(TokenKind.Colon);
            string value = CollectUntil(TokenKind.Semicolon);
            TryConsume(TokenKind.Semicolon);
            return new CSSSVariableDecl { Name = name, Value = value.Trim() };
        }

        // ── Declaration ───────────────────────────────────────────────────────

        private CSSSDeclaration ParseDeclaration()
        {
            string prop = Consume(TokenKind.Ident).Value;
            Consume(TokenKind.Colon);
            string value = CollectUntil(TokenKind.Semicolon, TokenKind.RightBrace);
            TryConsume(TokenKind.Semicolon);
            return new CSSSDeclaration { Property = prop, Value = value.Trim() };
        }

        // ── Rule ─────────────────────────────────────────────────────────────

        private CSSSRule ParseRule()
        {
            var rule = new CSSSRule();

            // Parse comma-separated selectors — CollectUntil preserves spaces in selector text
            string selector = CollectUntil(TokenKind.LeftBrace, TokenKind.Comma).Trim();
            rule.Selectors.Add(selector);

            while (TryConsume(TokenKind.Comma))
            {
                selector = CollectUntil(TokenKind.LeftBrace, TokenKind.Comma).Trim();
                rule.Selectors.Add(selector);
            }

            Consume(TokenKind.LeftBrace);

            while (true)
            {
                SkipWs();
                if (AtEnd() || PeekRaw().Kind == TokenKind.RightBrace) break;
                rule.Body.Add(ParseStatement());
            }

            TryConsume(TokenKind.RightBrace);
            return rule;
        }

        // ── @-rules ───────────────────────────────────────────────────────────

        private CSSSStatement ParseAtRule()
        {
            Consume(TokenKind.At);
            string name = Consume(TokenKind.Ident).Value;

            switch (name.ToLowerInvariant())
            {
                case "mixin":   return ParseMixin();
                case "include": return ParseInclude();
                case "import":  return ParseImport();
                case "extend":  return ParseExtend();
                default:        return ParseGenericAtRule(name);
            }
        }

        private CSSSImport ParseImport()
        {
            string path = CollectUntil(TokenKind.Semicolon).Trim();
            TryConsume(TokenKind.Semicolon);
            return new CSSSImport { Path = path };
        }

        private CSSSExtend ParseExtend()
        {
            var selectors = new List<string>();
            while (true)
            {
                SkipWs();
                string selector = CollectUntil(TokenKind.Semicolon, TokenKind.Comma).Trim();
                if (!string.IsNullOrEmpty(selector))
                    selectors.Add(selector);
                if (!TryConsume(TokenKind.Comma))
                    break;
            }
            TryConsume(TokenKind.Semicolon);
            return new CSSSExtend { Selectors = selectors };
        }

        private CSSSMixin ParseMixin()
        {
            string mixinName = Consume(TokenKind.Ident).Value;
            var mixin = new CSSSMixin { Name = mixinName };

            if (TryConsume(TokenKind.LeftParen))
            {
                while (true)
                {
                    SkipWs();
                    if (AtEnd() || PeekRaw().Kind == TokenKind.RightParen) break;
                    if (PeekRaw().Kind == TokenKind.Variable)
                    {
                        var param = new CSSSMixinParameter { Name = Advance().Value.TrimStart('$') };
                        // Check for default value (colon after variable name)
                        SkipWs();
                        if (TryConsume(TokenKind.Colon))
                        {
                            param.DefaultValue = CollectUntil(TokenKind.Comma, TokenKind.RightParen).Trim();
                        }
                        mixin.Parameters.Add(param);
                    }
                    else
                    {
                        Advance();
                    }
                    TryConsume(TokenKind.Comma);
                }
                TryConsume(TokenKind.RightParen);
            }

            Consume(TokenKind.LeftBrace);
            while (true)
            {
                SkipWs();
                if (AtEnd() || PeekRaw().Kind == TokenKind.RightBrace) break;
                mixin.Body.Add(ParseStatement());
            }
            TryConsume(TokenKind.RightBrace);
            return mixin;
        }

        private CSSSInclude ParseInclude()
        {
            string mixinName = Consume(TokenKind.Ident).Value;
            var include = new CSSSInclude { Name = mixinName };

            if (TryConsume(TokenKind.LeftParen))
            {
                while (true)
                {
                    SkipWs();
                    if (AtEnd() || PeekRaw().Kind == TokenKind.RightParen) break;
                    string arg = CollectUntil(TokenKind.Comma, TokenKind.RightParen).Trim();
                    include.Arguments.Add(arg);
                    TryConsume(TokenKind.Comma);
                }
                TryConsume(TokenKind.RightParen);
            }

            TryConsume(TokenKind.Semicolon);
            return include;
        }

        private CSSSAtRule ParseGenericAtRule(string name)
        {
            string prelude = CollectUntil(TokenKind.LeftBrace, TokenKind.Semicolon).Trim();
            var atRule = new CSSSAtRule { Name = name, Prelude = prelude };

            if (TryConsume(TokenKind.LeftBrace))
            {
                atRule.Block = new List<CSSSStatement>();
                while (true)
                {
                    SkipWs();
                    if (AtEnd() || PeekRaw().Kind == TokenKind.RightBrace) break;
                    atRule.Block.Add(ParseStatement());
                }
                TryConsume(TokenKind.RightBrace);
            }
            else
            {
                TryConsume(TokenKind.Semicolon);
            }

            return atRule;
        }

        // ── Token helpers ─────────────────────────────────────────────────────

        private bool AtEnd() => _pos >= _tokens.Count || _tokens[_pos].Kind == TokenKind.EOF;

        /// <summary>Returns the raw token at the current position (may be whitespace).</summary>
        private Token PeekRaw() =>
            _pos < _tokens.Count ? _tokens[_pos] : new Token(TokenKind.EOF, "", 0, 0);

        /// <summary>Skips any whitespace/newline tokens at the current position.</summary>
        private void SkipWs()
        {
            while (_pos < _tokens.Count && _tokens[_pos].Kind is TokenKind.Whitespace or TokenKind.Newline)
                _pos++;
        }

        private Token Advance()
        {
            var token = PeekRaw();
            _pos++;
            return token;
        }

        private Token Consume(TokenKind kind)
        {
            SkipWs();
            var token = PeekRaw();
            if (token.Kind != kind)
                throw new CSSSParseException($"Expected {kind} but got {token.Kind}('{token.Value}') at {token.Line}:{token.Column}");
            _pos++;
            return token;
        }

        private bool TryConsume(TokenKind kind)
        {
            SkipWs();
            if (PeekRaw().Kind == kind) { _pos++; return true; }
            return false;
        }

        /// <summary>
        /// Collects tokens as a string until a stop token is reached at depth 0.
        /// Whitespace tokens are emitted as single spaces — this preserves descendant selector
        /// structure (e.g. <c>Box .label</c>) and multi-word values (e.g. <c>1px solid red</c>).
        /// </summary>
        private string CollectUntil(params TokenKind[] stopKinds)
        {
            var sb = new System.Text.StringBuilder();
            int depth = 0;
            while (!AtEnd())
            {
                var token = PeekRaw();

                if (depth <= 0 && stopKinds.Contains(token.Kind)) break;

                if (token.Kind is TokenKind.Whitespace or TokenKind.Newline)
                {
                    sb.Append(' ');
                    _pos++;
                    continue;
                }

                if (token.Kind is TokenKind.LeftBrace or TokenKind.LeftParen)   depth++;
                if (token.Kind is TokenKind.RightBrace or TokenKind.RightParen) depth--;

                if (token.Kind == TokenKind.Variable) sb.Append('$');
                sb.Append(token.Value);
                _pos++;
            }
            return sb.ToString();
        }
    }
}

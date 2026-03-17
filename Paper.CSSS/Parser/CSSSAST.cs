namespace Paper.CSSS.Parser
{
    // ── Abstract nodes ────────────────────────────────────────────────────────

    internal abstract class CSSSNode { }

    internal abstract class CSSSStatement : CSSSNode { }

    // ── Top-level ─────────────────────────────────────────────────────────────

    /// <summary>Root of the CSSS file.</summary>
    internal sealed class CSSSStylesheet : CSSSNode
    {
        public List<CSSSStatement> Statements { get; } = new();
    }

    // ── Rules ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// A CSSS rule: one or more selectors + a block of declarations and nested rules.
    /// </summary>
    internal sealed class CSSSRule : CSSSStatement
    {
        public List<string> Selectors { get; } = new();
        public List<CSSSStatement> Body { get; } = new();
    }

    // ── Declarations ──────────────────────────────────────────────────────────

    /// <summary>A CSS property declaration: <c>color: red;</c></summary>
    internal sealed class CSSSDeclaration : CSSSStatement
    {
        public string Property { get; set; } = "";
        public string Value { get; set; } = "";
    }

    /// <summary>A CSSS variable assignment: <c>$primary: #fff;</c></summary>
    internal sealed class CSSSVariableDecl : CSSSStatement
    {
        public string Name { get; set; } = "";
        public string Value { get; set; } = "";
    }

    // ── At-rules ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Generic <c>@rule</c> — covers <c>@mixin</c>, <c>@include</c>, <c>@import</c>, <c>@media</c>, etc.
    /// </summary>
    internal sealed class CSSSAtRule : CSSSStatement
    {
        public string Name { get; set; } = "";
        public string Prelude { get; set; } = "";
        public List<CSSSStatement>? Block { get; set; }
    }

    // ── Mixin ─────────────────────────────────────────────────────────────────

    /// <summary>A mixin parameter with optional default value.</summary>
    internal sealed class CSSSMixinParameter
    {
        public string Name { get; set; } = "";
        public string? DefaultValue { get; set; }
    }

    internal sealed class CSSSMixin : CSSSStatement
    {
        public string Name { get; set; } = "";
        public List<CSSSMixinParameter> Parameters { get; } = new();
        public List<CSSSStatement> Body { get; } = new();
    }

    internal sealed class CSSSInclude : CSSSStatement
    {
        public string Name { get; set; } = "";
        public List<string> Arguments { get; } = new();
    }

    /// <summary>An @import statement: <c>@import "path";</c></summary>
    internal sealed class CSSSImport : CSSSStatement
    {
        public string Path { get; set; } = "";
    }

    /// <summary>An @extend statement: <c>@extend .selector;</c></summary>
    internal sealed class CSSSExtend : CSSSStatement
    {
        public List<string> Selectors { get; set; } = new();
    }
}

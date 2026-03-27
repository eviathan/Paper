namespace Paper.CSX.LanguageServer.Enums
{
    /// <summary>LSP CompletionItemKind values (https://microsoft.github.io/language-server-protocol/specifications/lsp/3.17/specification/#completionItemKind)</summary>
    internal enum CompletionItemKind
    {
        Method      = 2,
        Field       = 5,
        Variable    = 6,
        Class       = 7,
        Property    = 10,
        Value       = 12,
        Snippet     = 15,
        Keyword     = 14,
        File        = 17,
        Folder      = 19,
    }
}

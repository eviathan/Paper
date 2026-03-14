using Paper.CSX.LanguageServer;

// Force Paper assemblies to be loaded into the AppDomain so Roslyn can reference them
_ = typeof(Paper.Core.VirtualDom.UINode).Assembly;
_ = typeof(Paper.Core.Styles.StyleSheet).Assembly;
_ = typeof(Paper.Core.Hooks.Hooks).Assembly;
_ = typeof(Paper.CSX.CSXCompiler).Assembly;

var lsp = new SimpleLspServer();
await lsp.RunAsync();
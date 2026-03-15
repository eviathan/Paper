using Paper.CSX.LanguageServer;

// Force Paper assemblies to be loaded into the AppDomain so Roslyn can reference them
_ = typeof(Paper.Core.VirtualDom.UINode).Assembly;
_ = typeof(Paper.Core.Styles.StyleSheet).Assembly;
_ = typeof(Paper.Core.Hooks.Hooks).Assembly;
_ = typeof(Paper.CSX.CSXCompiler).Assembly;

// Pre-warm Roslyn metadata references on a background thread so the first
// hover/completion doesn't stall waiting for assembly scanning.
_ = Task.Run(() => { try { RoslynMembers.GetRefs(); } catch { } });

var lsp = new SimpleLspServer();
await lsp.RunAsync();
using System.Text;
using System.Text.Json;
using Paper.CSX;
using Paper.CSX.Syntax;

namespace Paper.CSX.LanguageServer
{
    internal sealed class SimpleLspServer
    {
        private readonly Stream _in = Console.OpenStandardInput();
        private readonly Stream _out = Console.OpenStandardOutput();
        private readonly Dictionary<string, string> _docs = new(StringComparer.Ordinal);
        private int _nextRequestId = 1;

        // Debounce diagnostics: cancel previous run when text changes
        private readonly Dictionary<string, CancellationTokenSource> _diagCts = new(StringComparer.Ordinal);

        private static void Log(string msg)
        {
            Console.Error.WriteLine($"[Paper] {msg}");
        }

        public async Task RunAsync()
        {
            Log("Language Server starting...");
            while (true)
            {
                var message = await ReadMessageAsync();

                if (message == null)
                {
                    Log("Connection closed");
                    break;
                }

                if (!message.RootElement.TryGetProperty("method", out var methodElement))
                    continue;

                var method = methodElement.GetString() ?? "";

                var id = message.RootElement.TryGetProperty("id", out var idElement)
                    ? idElement
                    : (JsonElement?)null;

                try
                {
                switch (method)
                {
                    case "initialize":
                        await ReplyAsync(id, new
                        {
                            capabilities = new
                            {
                                textDocumentSync = new { openClose = true, change = 1 },
                                completionProvider = new
                                {
                                    triggerCharacters = new[] { "<", " ", "{", ":", "\"", "'", ".", "/" },
                                    resolveProvider = false,
                                    completionItem = new { labelDetailsSupport = true },
                                },
                                hoverProvider = true,
                                signatureHelpProvider = new
                                {
                                    triggerCharacters = new[] { "(", "," },
                                    retriggerCharacters = new[] { ")" },
                                },
                                semanticTokensProvider = new
                                {
                                    legend = new
                                    {
                                        tokenTypes     = RoslynSemanticTokens.TokenTypes,
                                        tokenModifiers = Array.Empty<string>(),
                                    },
                                    full  = true,
                                    range = false,
                                },
                                definitionProvider     = true,
                                documentSymbolProvider = true,
                                foldingRangeProvider   = true,
                                inlayHintProvider      = true,
                                renameProvider         = true,
                                documentFormattingProvider = true,
                            }
                        });
                        break;

                    case "initialized":
                        break;

                    case "shutdown":
                        await ReplyAsync(id, (object?)null);
                        break;

                    case "exit":
                        return;

                    case "textDocument/didOpen":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var textDocument = parameters.GetProperty("textDocument");
                            var uri = textDocument.GetProperty("uri").GetString() ?? "";
                            var text = textDocument.GetProperty("text").GetString() ?? "";
                            _docs[uri] = text;
                            Log($"Document opened: {Path.GetFileName(uri)} ({text.Length} chars)");
                            // Pre-warm the Roslyn compilation immediately so the first hover/completion
                            // doesn't have to wait for a cold build (which can take 5-10 seconds).
                            _ = Task.Run(() => { try { RoslynHover.GetOrBuildCompilation(text); } catch { } });
                            ScheduleDiagnostics(uri, text);
                            break;
                        }

                    case "textDocument/didChange":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var textDocument = parameters.GetProperty("textDocument");
                            var uri = textDocument.GetProperty("uri").GetString() ?? "";
                            var changes = parameters.GetProperty("contentChanges");
                            var text = changes[changes.GetArrayLength() - 1].GetProperty("text").GetString() ?? "";
                            _docs[uri] = text;
                            Log($"Document changed: {Path.GetFileName(uri)} ({text.Length} chars)");
                            ScheduleDiagnostics(uri, text);
                            break;
                        }

                    case "textDocument/completion":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var textDocument = parameters.GetProperty("textDocument");
                            var uri = textDocument.GetProperty("uri").GetString() ?? "";
                            var position = parameters.GetProperty("position");
                            int line = position.GetProperty("line").GetInt32();
                            int character = position.GetProperty("character").GetInt32();

                            _docs.TryGetValue(uri, out var src);
                            var items = Completions.Compute(src ?? "", line, character, uri);
                            await ReplyAsync(id, new { isIncomplete = false, items });
                            break;
                        }

                    case "textDocument/hover":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var textDocument = parameters.GetProperty("textDocument");
                            var uri = textDocument.GetProperty("uri").GetString() ?? "";
                            var position = parameters.GetProperty("position");
                            int line = position.GetProperty("line").GetInt32();
                            int character = position.GetProperty("character").GetInt32();

                            _docs.TryGetValue(uri, out var src);
                            var hover = Hover.Compute(src ?? "", line, character);
                            await ReplyAsync(id, hover);
                            break;
                        }

                    case "textDocument/signatureHelp":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var textDocument = parameters.GetProperty("textDocument");
                            var uri = textDocument.GetProperty("uri").GetString() ?? "";
                            var position = parameters.GetProperty("position");
                            int line = position.GetProperty("line").GetInt32();
                            int character = position.GetProperty("character").GetInt32();

                            _docs.TryGetValue(uri, out var src);
                            var sigHelp = RoslynSignatureHelp.GetSignatureHelp(src ?? "", line, character);
                            await ReplyAsync(id, sigHelp);
                            break;
                        }

                    case "textDocument/semanticTokens/full":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                            _docs.TryGetValue(uri, out var src);
                            var data = RoslynSemanticTokens.GetEncodedTokens(src ?? "");
                            await ReplyAsync(id, new { data });
                            break;
                        }

                    case "textDocument/definition":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var uri        = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                            var position   = parameters.GetProperty("position");
                            int line       = position.GetProperty("line").GetInt32();
                            int character  = position.GetProperty("character").GetInt32();

                            _docs.TryGetValue(uri, out var src);
                            var location = RoslynDefinition.GetDefinition(src ?? "", uri, line, character);
                            // Return null (not array) when not found — array when found
                            await ReplyAsync(id, location != null ? new[] { location } : null);
                            break;
                        }

                    case "textDocument/documentSymbol":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var uri        = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";

                            _docs.TryGetValue(uri, out var src);
                            var symbols = RoslynDocumentSymbols.GetDocumentSymbols(src ?? "", uri);
                            await ReplyAsync(id, symbols);
                            break;
                        }

                    case "textDocument/foldingRange":
                        {
                            var uri = message.RootElement
                                .GetProperty("params")
                                .GetProperty("textDocument")
                                .GetProperty("uri").GetString() ?? "";
                            _docs.TryGetValue(uri, out var src);
                            var foldingRanges = FoldingRanges.GetFoldingRanges(src ?? "");
                            await ReplyAsync(id, foldingRanges);
                            break;
                        }

                    case "textDocument/inlayHint":
                        {
                            var uri = message.RootElement
                                .GetProperty("params")
                                .GetProperty("textDocument")
                                .GetProperty("uri").GetString() ?? "";
                            _docs.TryGetValue(uri, out var src);
                            var hints = RoslynInlayHints.GetInlayHints(src ?? "");
                            await ReplyAsync(id, hints);
                            break;
                        }

                    case "textDocument/rename":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var uri        = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                            var position   = parameters.GetProperty("position");
                            int line       = position.GetProperty("line").GetInt32();
                            int character  = position.GetProperty("character").GetInt32();
                            var newName    = parameters.GetProperty("newName").GetString() ?? "";

                            _docs.TryGetValue(uri, out var src);
                            var edit = RoslynRename.GetRename(src ?? "", uri, line, character, newName);
                            await ReplyAsync(id, edit);
                            break;
                        }

                    case "textDocument/formatting":
                        {
                            var parameters = message.RootElement.GetProperty("params");
                            var uri = parameters.GetProperty("textDocument").GetProperty("uri").GetString() ?? "";
                            var options = parameters.GetProperty("options");

                            _docs.TryGetValue(uri, out var src);
                            if (string.IsNullOrEmpty(src))
                            {
                                await ReplyAsync(id, Array.Empty<object>());
                                break;
                            }

                            var edits = RoslynFormatting.Format(src ?? "", uri, options);
                            await ReplyAsync(id, edits);
                            break;
                        }

                    case "textDocument/didClose":
                        {
                            var uri = message.RootElement
                                .GetProperty("params")
                                .GetProperty("textDocument")
                                .GetProperty("uri").GetString() ?? "";
                            _docs.Remove(uri);
                            if (_diagCts.TryGetValue(uri, out var cts))
                            {
                                cts.Cancel();
                                _diagCts.Remove(uri);
                            }
                            break;
                        }
                }
                }
                catch (Exception ex)
                {
                    // Reply with a null result so the client doesn't hang waiting for a response.
                    if (id != null)
                        await ReplyAsync(id, (object?)null);
                    // Log to stderr so it can be captured for debugging without polluting stdout.
                    await Console.Error.WriteLineAsync($"[LSP] Unhandled exception for '{method}': {ex.Message}");
                }
            }
        }

        private void ScheduleDiagnostics(string uri, string text)
        {
            if (_diagCts.TryGetValue(uri, out var old))
                old.Cancel();

            var cts = new CancellationTokenSource();
            _diagCts[uri] = cts;
            
            Log($"Scheduling diagnostics for: {Path.GetFileName(uri)}");
            _ = RunDiagnosticsAsync(uri, text, cts.Token);
        }

        private async Task RunDiagnosticsAsync(string uri, string text, CancellationToken token)
        {
            try { await Task.Delay(400, token); }
            catch (OperationCanceledException) { return; }

            Log($"Running diagnostics for: {Path.GetFileName(uri)}");
            var diagnostics = new List<object>();

            // 1. CSX parser errors — parse only the JSX portion, not the full file.
            // Passing the full file to CSXElementParser causes it to find the first '<' in
            // preamble code (e.g. List<string>) and mis-parse it as a JSX element.
            bool parseOk = true;
            try
            {
                var (_, jsxRaw, _, _) = CSXCompiler.ExtractPreambleAndJsx(text);
                if (!string.IsNullOrWhiteSpace(jsxRaw))
                    _ = new Paper.CSX.Syntax.CSXElementParser(jsxRaw).ParseFirstElement();
            }
            catch (Exception ex)
            {
                parseOk = false;
                diagnostics.Add(new
                {
                    range = new { start = new { line = 0, character = 0 }, end = new { line = 0, character = 1 } },
                    severity = 1,
                    source = "paper-csx",
                    message = ex.Message,
                });
            }

            // 2. Roslyn type-checking — always run to keep compilation cache warm,
            //    but suppress Roslyn errors when the CSX parser already reported a syntax error.
            var roslynDiags = RoslynDiagnostics.Compile(text);
            if (parseOk)
                diagnostics.AddRange(roslynDiags);

            if (token.IsCancellationRequested) return;
            Log($"Publishing {diagnostics.Count} diagnostics for {Path.GetFileName(uri)}");
            await NotifyAsync("textDocument/publishDiagnostics", new { uri, diagnostics });
        }

        private async Task ReplyAsync(JsonElement? id, object? result)
        {
            if (id == null) return;
            var reply = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id.Value.ValueKind switch
                {
                    JsonValueKind.Number => id.Value.GetInt32(),
                    JsonValueKind.String => id.Value.GetString(),
                    _ => _nextRequestId++,
                },
                ["result"] = result,
            };
            await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(reply));
        }

        private async Task NotifyAsync(string method, object? @params)
        {
            var msg = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["method"] = method,
                ["params"] = @params,
            };
            await WriteMessageAsync(JsonSerializer.SerializeToUtf8Bytes(msg));
        }

        private async Task<JsonDocument?> ReadMessageAsync()
        {
            int contentLength = 0;
            while (true)
            {
                var line = await ReadLineAsync(_in);
                if (line == null) return null;
                if (line.Length == 0) break;
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    int.TryParse(line["Content-Length:".Length..].Trim(), out contentLength);
            }
            if (contentLength <= 0) return null;
            var buf = new byte[contentLength];
            int read = 0;
            while (read < contentLength)
            {
                int n = await _in.ReadAsync(buf.AsMemory(read, contentLength - read));
                if (n <= 0) break;
                read += n;
            }
            if (read != contentLength) return null;
            return JsonDocument.Parse(buf);
        }

        private static async Task<string?> ReadLineAsync(Stream stream)
        {
            var stringBuilder = new StringBuilder();
            var b = new byte[1];
            while (true)
            {
                int n = await stream.ReadAsync(b.AsMemory(0, 1));
                if (n == 0) return null;
                char c = (char)b[0];
                if (c == '\r') continue;
                if (c == '\n') break;
                stringBuilder.Append(c);
            }
            return stringBuilder.ToString();
        }

        private async Task WriteMessageAsync(byte[] json)
        {
            var header = Encoding.ASCII.GetBytes($"Content-Length: {json.Length}\r\n\r\n");
            await _out.WriteAsync(header);
            await _out.WriteAsync(json);
            await _out.FlushAsync();
        }
    }
}
using System.Text;
using System.Text.Json;
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

        public async Task RunAsync()
        {
            while (true)
            {
                var message = await ReadMessageAsync();

                if (message == null)
                    break;

                if (!message.RootElement.TryGetProperty("method", out var methodElement))
                    continue;

                var method = methodElement.GetString() ?? "";

                var id = message.RootElement.TryGetProperty("id", out var idElement) 
                    ? idElement
                    : (JsonElement?)null;

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
                }
            }
        }

        private void ScheduleDiagnostics(string uri, string text)
        {
            if (_diagCts.TryGetValue(uri, out var old))
                old.Cancel();

            var cts = new CancellationTokenSource();
            _diagCts[uri] = cts;
            
            _ = RunDiagnosticsAsync(uri, text, cts.Token);
        }

        private async Task RunDiagnosticsAsync(string uri, string text, CancellationToken token)
        {
            try { await Task.Delay(400, token); }
            catch (OperationCanceledException) { return; }

            var diagnostics = new List<object>();

            // 1. CSX parser errors
            bool parseOk = false;
            try { _ = new CSXParser2(text).ParseFirstElement(); parseOk = true; }
            catch (Exception ex)
            {
                diagnostics.Add(new
                {
                    range = new { start = new { line = 0, character = 0 }, end = new { line = 0, character = 1 } },
                    severity = 1,
                    source = "paper-csx",
                    message = ex.Message,
                });
            }

            // 2. Roslyn type-checking (only when parser succeeded, to avoid noise)
            if (parseOk)
                diagnostics.AddRange(RoslynDiagnostics.Compile(text));

            if (token.IsCancellationRequested) return;
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
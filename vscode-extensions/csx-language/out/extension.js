"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = require("vscode");
const fs = require("fs");
const path = require("path");
const child_process_1 = require("child_process");
const node_1 = require("vscode-languageclient/node");
let client;
let clientStarting = false;
const CSS_PROPS = [
    { name: 'display', detail: 'How element is rendered', values: ['flex', 'block', 'inline', 'inline-flex', 'inline-block', 'grid', 'inline-grid', 'none'] },
    { name: 'flex-direction', detail: 'Main axis direction', values: ['row', 'column', 'row-reverse', 'column-reverse'] },
    { name: 'flex-wrap', detail: 'Allow wrapping', values: ['nowrap', 'wrap', 'wrap-reverse'] },
    { name: 'flex', detail: 'flex shorthand', values: [] },
    { name: 'flex-grow', detail: 'How much to grow', values: [] },
    { name: 'flex-shrink', detail: 'How much to shrink', values: [] },
    { name: 'justify-content', detail: 'Alignment along main axis', values: ['flex-start', 'flex-end', 'center', 'space-between', 'space-around', 'space-evenly'] },
    { name: 'align-items', detail: 'Alignment along cross axis', values: ['flex-start', 'flex-end', 'center', 'stretch', 'baseline'] },
    { name: 'align-self', detail: 'Self cross-axis alignment', values: ['auto', 'flex-start', 'flex-end', 'center', 'stretch', 'baseline'] },
    { name: 'align-content', detail: 'Multi-line cross alignment', values: ['flex-start', 'flex-end', 'center', 'stretch', 'space-between', 'space-around'] },
    { name: 'gap', detail: 'Row and column gap', values: [] },
    { name: 'row-gap', detail: 'Row gap', values: [] },
    { name: 'column-gap', detail: 'Column gap', values: [] },
    { name: 'grid-template-columns', detail: 'Grid column track sizes', values: [] },
    { name: 'grid-template-rows', detail: 'Grid row track sizes', values: [] },
    { name: 'grid-column', detail: 'Column span e.g. 1 / 3', values: [] },
    { name: 'grid-row', detail: 'Row span', values: [] },
    { name: 'justify-items', detail: 'Justify all items in grid', values: ['start', 'end', 'center', 'stretch'] },
    { name: 'width', detail: 'Width', values: ['auto'] },
    { name: 'height', detail: 'Height', values: ['auto'] },
    { name: 'min-width', detail: 'Minimum width', values: [] },
    { name: 'min-height', detail: 'Minimum height', values: [] },
    { name: 'max-width', detail: 'Maximum width', values: [] },
    { name: 'max-height', detail: 'Maximum height', values: [] },
    { name: 'padding', detail: 'All-sides padding', values: [] },
    { name: 'padding-top', detail: 'Top padding', values: [] },
    { name: 'padding-right', detail: 'Right padding', values: [] },
    { name: 'padding-bottom', detail: 'Bottom padding', values: [] },
    { name: 'padding-left', detail: 'Left padding', values: [] },
    { name: 'margin', detail: 'All-sides margin', values: [] },
    { name: 'margin-top', detail: 'Top margin', values: [] },
    { name: 'margin-right', detail: 'Right margin', values: [] },
    { name: 'margin-bottom', detail: 'Bottom margin', values: [] },
    { name: 'margin-left', detail: 'Left margin', values: [] },
    { name: 'background', detail: 'Background color', values: [] },
    { name: 'color', detail: 'Text color', values: [] },
    { name: 'opacity', detail: 'Opacity 0..1', values: [] },
    { name: 'visibility', detail: 'Element visibility', values: ['visible', 'hidden'] },
    { name: 'font-size', detail: 'Font size', values: [] },
    { name: 'font-weight', detail: 'Font weight', values: ['normal', 'bold'] },
    { name: 'font-family', detail: 'Font family', values: [] },
    { name: 'line-height', detail: 'Line height multiplier', values: [] },
    { name: 'letter-spacing', detail: 'Letter spacing', values: [] },
    { name: 'text-align', detail: 'Text alignment', values: ['left', 'center', 'right'] },
    { name: 'text-overflow', detail: 'Text overflow', values: ['ellipsis', 'clip'] },
    { name: 'text-decoration', detail: 'Text decoration', values: ['none', 'underline'] },
    { name: 'white-space', detail: 'White-space handling', values: ['normal', 'nowrap', 'pre', 'pre-wrap'] },
    { name: 'word-wrap', detail: 'Word wrap', values: ['normal', 'break-word'] },
    { name: 'border', detail: 'Border shorthand', values: [] },
    { name: 'border-top', detail: 'Top border', values: [] },
    { name: 'border-right', detail: 'Right border', values: [] },
    { name: 'border-bottom', detail: 'Bottom border', values: [] },
    { name: 'border-left', detail: 'Left border', values: [] },
    { name: 'border-radius', detail: 'Corner radius', values: [] },
    { name: 'border-width', detail: 'Border width', values: [] },
    { name: 'border-color', detail: 'Border color', values: [] },
    { name: 'border-style', detail: 'Border style', values: ['solid', 'dashed', 'dotted', 'none'] },
    { name: 'overflow', detail: 'Content overflow', values: ['hidden', 'visible', 'scroll', 'auto', 'clip'] },
    { name: 'overflow-x', detail: 'Horizontal overflow', values: ['hidden', 'visible', 'scroll', 'auto', 'clip'] },
    { name: 'overflow-y', detail: 'Vertical overflow', values: ['hidden', 'visible', 'scroll', 'auto', 'clip'] },
    { name: 'position', detail: 'Positioning scheme', values: ['relative', 'absolute', 'fixed', 'sticky'] },
    { name: 'top', detail: 'Top offset', values: [] },
    { name: 'left', detail: 'Left offset', values: [] },
    { name: 'right', detail: 'Right offset', values: [] },
    { name: 'bottom', detail: 'Bottom offset', values: [] },
    { name: 'z-index', detail: 'Z stack order', values: [] },
    { name: 'cursor', detail: 'Mouse cursor shape', values: ['default', 'pointer', 'text', 'crosshair', 'grab', 'grabbing', 'not-allowed'] },
    { name: 'pointer-events', detail: 'Receive pointer events', values: ['auto', 'none'] },
    { name: 'transform', detail: 'CSS transforms', values: [] },
    { name: 'transition', detail: "CSS transition e.g. 'all 0.2s'", values: [] },
    { name: 'box-sizing', detail: 'Box sizing model', values: ['border-box', 'content-box'] },
    { name: 'object-fit', detail: 'Image fit mode', values: ['contain', 'cover', 'fill'] },
    { name: 'box-shadow', detail: 'Box shadow', values: [] },
];
// Paper element names (for selector completions in CSSS)
const PAPER_ELEMENTS = [
    'Box', 'Text', 'Button', 'Input', 'Scroll', 'Image',
    'Textarea', 'Checkbox', 'Table', 'TableRow', 'TableCell',
    'RadioGroup', 'RadioOption', 'Fragment', 'Viewport',
];
// ── Activation ────────────────────────────────────────────────────────────────
function activate(context) {
    console.log('CSX/CSSS Language Support activated');
    // Start LSP lazily for CSX files
    context.subscriptions.push(vscode.workspace.onDidOpenTextDocument((doc) => {
        if (doc.languageId === 'csx' && !client && !clientStarting) {
            tryStartLanguageServer(context, doc.uri.fsPath);
        }
    }));
    const openCSX = vscode.workspace.textDocuments.find((d) => d.languageId === 'csx');
    if (openCSX && openCSX.uri.scheme === 'file' && !client && !clientStarting) {
        tryStartLanguageServer(context, openCSX.uri.fsPath);
    }
    // ── CSSS completions (in-process, no separate server needed) ─────────────
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ scheme: 'file', language: 'csss' }, new CSSSCompletionProvider(), '$', '.', '#', ':', ' ', '\n'));
    // ── CSX compile commands ──────────────────────────────────────────────────
    context.subscriptions.push(vscode.commands.registerCommand('csx.compile', async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor) {
            vscode.window.showErrorMessage('No file is open');
            return;
        }
        if (editor.document.languageId !== 'csx') {
            vscode.window.showErrorMessage('Current file is not a CSX file');
            return;
        }
        await compileFile(editor.document.uri.fsPath);
    }));
    context.subscriptions.push(vscode.commands.registerCommand('csx.compileAll', async () => {
        let rootPath;
        const folders = vscode.workspace.workspaceFolders;
        if (folders?.length) {
            rootPath = folders[0].uri.fsPath;
        }
        else {
            const ed = vscode.window.activeTextEditor;
            if (ed?.document.languageId === 'csx') {
                rootPath = path.dirname(ed.document.uri.fsPath);
            }
            else {
                const csxDoc = vscode.workspace.textDocuments.find((d) => d.languageId === 'csx' && d.uri.scheme === 'file');
                rootPath = csxDoc ? path.dirname(csxDoc.uri.fsPath) : undefined;
            }
        }
        if (!rootPath) {
            vscode.window.showErrorMessage('CSX: Open a workspace folder or a CSX file to run Compile All.');
            return;
        }
        await compileAllFiles(rootPath);
    }));
    // Auto-compile CSX on save
    context.subscriptions.push(vscode.workspace.onDidSaveTextDocument(async (doc) => {
        if (doc.languageId === 'csx' && doc.uri.scheme === 'file') {
            await compileFile(doc.uri.fsPath);
        }
    }));
}
// ── CSSS completion provider ──────────────────────────────────────────────────
class CSSSCompletionProvider {
    provideCompletionItems(document, position) {
        const lineText = document.lineAt(position).text;
        const before = lineText.slice(0, position.character);
        // After `$` — variable reference or declaration
        if (/\$[\w-]*$/.test(before)) {
            return this.variableCompletions(document);
        }
        // Inside a declaration value (after `:`) — CSS values for the property on this line
        const valueMatch = before.match(/^\s*([\w-]+)\s*:\s*([\w-]*)$/);
        if (valueMatch) {
            const prop = CSS_PROPS.find((p) => p.name === valueMatch[1]);
            if (prop && prop.values.length > 0) {
                return prop.values.map((v) => {
                    const item = new vscode.CompletionItem(v, vscode.CompletionItemKind.Value);
                    item.insertText = v;
                    return item;
                });
            }
        }
        // Inside a selector context (no `{` opened yet, or at start of block) — property names
        if (this.isInsideDeclarationBlock(document, position)) {
            return this.cssPropertyCompletions();
        }
        // Outside a block — selector completions
        return this.selectorCompletions();
    }
    isInsideDeclarationBlock(doc, pos) {
        // Walk back through the document counting { and } — if opens > closes, we're inside a block
        const text = doc.getText(new vscode.Range(new vscode.Position(0, 0), pos));
        let depth = 0;
        for (const ch of text) {
            if (ch === '{')
                depth++;
            else if (ch === '}')
                depth--;
        }
        return depth > 0;
    }
    selectorCompletions() {
        const items = [];
        // Paper element type selectors
        for (const el of PAPER_ELEMENTS) {
            const item = new vscode.CompletionItem(el, vscode.CompletionItemKind.Class);
            item.detail = 'Paper element selector';
            item.insertText = new vscode.SnippetString(`${el} {\n    \${0}\n}`);
            items.push(item);
        }
        // Pseudo-classes
        for (const pseudo of [':hover', ':active', ':focus', ':disabled']) {
            const item = new vscode.CompletionItem(pseudo, vscode.CompletionItemKind.Keyword);
            item.detail = 'Pseudo-class';
            items.push(item);
        }
        return items;
    }
    cssPropertyCompletions() {
        return CSS_PROPS.map((p) => {
            const item = new vscode.CompletionItem(p.name, vscode.CompletionItemKind.Property);
            item.detail = p.detail;
            if (p.values.length > 0) {
                item.insertText = new vscode.SnippetString(`${p.name}: \${1|${p.values.join(',')}|};`);
            }
            else {
                item.insertText = new vscode.SnippetString(`${p.name}: \${0};`);
            }
            item.documentation = p.detail;
            return item;
        });
    }
    variableCompletions(document) {
        const text = document.getText();
        const items = [];
        const seen = new Set();
        // Find all $variable declarations in the document
        const re = /\$([\w-]+)\s*:/g;
        let m;
        while ((m = re.exec(text)) !== null) {
            const name = '$' + m[1];
            if (!seen.has(name)) {
                seen.add(name);
                const item = new vscode.CompletionItem(name, vscode.CompletionItemKind.Variable);
                item.detail = 'CSSS variable';
                item.insertText = name;
                items.push(item);
            }
        }
        return items;
    }
}
// ── LSP startup ───────────────────────────────────────────────────────────────
function tryStartLanguageServer(context, fromFilePath) {
    const lsPath = getCSXLanguageServerPath(fromFilePath);
    if (!lsPath || !fs.existsSync(path.join(lsPath, 'Paper.CSX.LanguageServer.csproj')))
        return;
    clientStarting = true;
    // Prefer the pre-built DLL (instant startup) — fall back to dotnet run (builds first)
    const tfm = 'net10.0';
    const dllPath = path.join(lsPath, 'bin', 'Debug', tfm, 'Paper.CSX.LanguageServer.dll');
    const serverOptions = fs.existsSync(dllPath)
        ? { command: 'dotnet', args: [dllPath], transport: node_1.TransportKind.stdio }
        : { command: 'dotnet', args: ['run', '--project', lsPath, '--no-build', '--'], transport: node_1.TransportKind.stdio };
    const clientOptions = {
        documentSelector: [{ scheme: 'file', language: 'csx' }],
        synchronize: { fileEvents: vscode.workspace.createFileSystemWatcher('**/*.csx') },
    };
    client = new node_1.LanguageClient('paperCSXLanguageServer', 'Paper CSX Language Server', serverOptions, clientOptions);
    context.subscriptions.push(client);
    client.start().finally(() => { clientStarting = false; });
}
// ── Compile helpers ───────────────────────────────────────────────────────────
async function compileFile(filePath) {
    const cliPath = getCSXCliPath(filePath);
    if (!cliPath || !fs.existsSync(path.join(cliPath, 'Paper.CSX.Cli.csproj'))) {
        vscode.window.showErrorMessage('CSX: Could not find Paper.CSX.Cli. Open a CSX file under the Paper repo, or set csx.cliPath in settings.');
        return;
    }
    try {
        await executeCommand(`dotnet run --project "${cliPath}" -- parse "${filePath}"`);
        vscode.window.showInformationMessage(`Compiled ${path.basename(filePath)}`);
    }
    catch (error) {
        vscode.window.showErrorMessage(`Compilation failed: ${error}`);
    }
}
async function compileAllFiles(rootPath) {
    const cliPath = getCSXCliPath(rootPath);
    if (!cliPath || !fs.existsSync(path.join(cliPath, 'Paper.CSX.Cli.csproj'))) {
        vscode.window.showErrorMessage('CSX: Could not find Paper.CSX.Cli. Set csx.cliPath or run from a folder under the Paper repo.');
        return;
    }
    try {
        await executeCommand(`dotnet run --project "${cliPath}" -- build "${rootPath}"`);
        vscode.window.showInformationMessage('CSX compilation completed');
    }
    catch (error) {
        vscode.window.showErrorMessage(`Compilation failed: ${error}`);
    }
}
// ── Path resolution ───────────────────────────────────────────────────────────
function resolveRepoRootFromFile(fileOrDirPath) {
    try {
        let dir;
        if (fs.existsSync(fileOrDirPath)) {
            dir = fs.statSync(fileOrDirPath).isDirectory() ? fileOrDirPath : path.dirname(fileOrDirPath);
        }
        else {
            dir = path.dirname(fileOrDirPath);
        }
        const root = path.parse(dir).root;
        while (dir && dir !== root) {
            const cliCsproj = path.join(dir, 'Paper.CSX.Cli', 'Paper.CSX.Cli.csproj');
            const lsCsproj = path.join(dir, 'Paper.CSX.LanguageServer', 'Paper.CSX.LanguageServer.csproj');
            if (fs.existsSync(cliCsproj) && fs.existsSync(lsCsproj))
                return dir;
            const parent = path.dirname(dir);
            if (parent === dir)
                break;
            dir = parent;
        }
    }
    catch { /* ignore */ }
    return undefined;
}
function getCSXCliPath(fromFilePath) {
    const configPath = vscode.workspace.getConfiguration('csx').get('cliPath');
    if (configPath?.trim())
        return path.resolve(configPath.trim());
    const repoRoot = fromFilePath ? resolveRepoRootFromFile(fromFilePath) : undefined;
    if (repoRoot)
        return path.join(repoRoot, 'Paper.CSX.Cli');
    return undefined;
}
function getCSXLanguageServerPath(fromFilePath) {
    const configPath = vscode.workspace.getConfiguration('csx').get('languageServerPath');
    if (configPath?.trim())
        return path.resolve(configPath.trim());
    const repoRoot = fromFilePath ? resolveRepoRootFromFile(fromFilePath) : undefined;
    if (repoRoot)
        return path.join(repoRoot, 'Paper.CSX.LanguageServer');
    return undefined;
}
function executeCommand(command) {
    return new Promise((resolve, reject) => {
        (0, child_process_1.exec)(command, (error, stdout, stderr) => {
            if (error)
                reject(stderr || error.message);
            else
                resolve(stdout);
        });
    });
}
function deactivate() {
    console.log('CSX/CSSS Language Support deactivated');
    return client?.stop();
}
//# sourceMappingURL=extension.js.map
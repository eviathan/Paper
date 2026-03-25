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
const CSSS_PROPS = [
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
    { name: 'direction', detail: 'Text direction (ltr/rtl)', values: ['ltr', 'rtl'] },
    { name: 'text-transform', detail: 'Text transform', values: ['none', 'uppercase', 'lowercase', 'capitalize'] },
    { name: 'font-style', detail: 'Font style', values: ['normal', 'italic', 'oblique'] },
    { name: 'aspect-ratio', detail: 'Width/height ratio', values: [] },
    { name: 'translate-x', detail: 'Translate X offset', values: [] },
    { name: 'translate-y', detail: 'Translate Y offset', values: [] },
    { name: 'rotate', detail: 'Rotation degrees', values: [] },
    { name: 'scale-x', detail: 'Scale X factor', values: [] },
    { name: 'scale-y', detail: 'Scale Y factor', values: [] },
    { name: 'grid-area', detail: 'Grid area name', values: [] },
    { name: 'grid-template-areas', detail: 'Grid template areas', values: [] },
    { name: 'background-image', detail: 'Background image URL', values: [] },
    { name: 'object-position', detail: 'Image position', values: [] },
    { name: 'user-select', detail: 'Text selection', values: ['none', 'text', 'all'] },
    { name: 'transition-property', detail: 'Which properties to transition', values: [] },
    { name: 'transition-duration', detail: 'Transition duration (s/ms)', values: [] },
    { name: 'transition-timing-function', detail: 'Easing function', values: ['ease', 'linear', 'ease-in', 'ease-out', 'ease-in-out'] },
    { name: 'transition-delay', detail: 'Transition delay (s/ms)', values: [] },
    { name: 'animation', detail: 'Animation shorthand', values: [] },
    { name: 'animation-name', detail: 'Animation name', values: [] },
    { name: 'animation-duration', detail: 'Animation duration', values: [] },
];
// Paper element names (for selector completions in CSSS)
const PAPER_ELEMENTS = [
    'Box', 'Text', 'Button', 'Input', 'Scroll', 'Image',
    'Textarea', 'Checkbox', 'Table', 'TableRow', 'TableCell',
    'RadioGroup', 'RadioOption', 'Fragment', 'Viewport', 'Modal',
    'Select', 'Slider', 'Tabs', 'Tooltip', 'Popover', 'Toast',
    'List', 'ContextMenu', 'NumberInput', 'ToastContainer', 'ToastItem',
    'Radio', 'Switch', 'Progress', 'Avatar', 'Badge', 'Card',
    'Divider', 'Icon', 'ImageList', 'ListItem', 'Accordion',
];
// Detailed element info for completions/hovers
const PAPER_ELEMENT_INFO = {
    'Box': 'Generic container element with full props control',
    'Text': 'Renders text content',
    'Button': 'Clickable button element',
    'Input': 'Single-line text input',
    'Scroll': 'Scrollable container',
    'Image': 'Image element with src',
    'Textarea': 'Multiline text input',
    'Checkbox': 'Checkbox with label',
    'Table': 'Table container',
    'TableRow': 'Table row container',
    'TableCell': 'Table cell container',
    'RadioGroup': 'Radio button group',
    'RadioOption': 'Radio button option',
    'Radio': 'Single radio button option',
    'Fragment': 'Renders children without wrapper',
    'Viewport': 'External OpenGL texture viewport',
    'Modal': 'Full-screen modal overlay',
    'Select': 'Dropdown select component',
    'Slider': 'Horizontal range slider',
    'Tabs': 'Tab strip with panels',
    'Tooltip': 'Tooltip on hover',
    'Popover': 'Floating panel anchored to trigger',
    'Toast': 'Notification toast',
    'List': 'Virtual scrolling list for large datasets',
    'ContextMenu': 'Right-click context menu',
    'NumberInput': 'Numeric input with increment/decrement',
    'ToastContainer': 'Container for multiple toast notifications',
    'ToastItem': 'Individual toast notification item',
    'Switch': 'Toggle switch component',
    'Progress': 'Progress bar indicator',
    'Avatar': 'User avatar image',
    'Badge': 'Status badge overlay',
    'Card': 'Card container component',
    'Divider': 'Horizontal divider line',
    'Icon': 'SVG icon element',
    'ImageList': 'Grid of images',
    'ListItem': 'List item container',
    'Accordion': 'Collapsible accordion panel',
};
// ── Activation ────────────────────────────────────────────────────────────────
/**
 * Forces *.csx → 'csx' language association in workspace settings (once).
 * Prevents the C# extension from treating .csx files as C# script files,
 * which would cause spurious Roslyn diagnostics on JSX syntax.
 * Also excludes .csx and .generated.cs files from C# analysis.
 */
function ensureCsxLanguageAssociation() {
    try {
        const config = vscode.workspace.getConfiguration();
        const assoc = config.get('files.associations') ?? {};
        let changed = false;
        if (assoc['*.csx'] !== 'csx') {
            assoc['*.csx'] = 'csx';
            changed = true;
        }
        // Don't associate .generated.cs with csx - that causes the LSP to try to handle
        // C# files which breaks hover/completion for real .csx files
        if (changed) {
            config.update('files.associations', assoc, vscode.ConfigurationTarget.Workspace);
        }
    }
    catch { /* best effort */ }
}
function activate(context) {
    console.log('CSX/CSSS Language Support activated');
    // Ensure *.csx is mapped to our 'csx' language, not 'csharp'.
    // Without this the C# extension analyses .csx files and generates spurious
    // diagnostics (red squiggles) on JSX syntax like <Box /> and =>.
    ensureCsxLanguageAssociation();
    // Start LSP lazily for CSX files
    const isCsxFile = (doc) => doc.uri.scheme === 'file' && doc.uri.fsPath.endsWith('.csx');
    // Pre-open the .generated.cs file so the C# language server has it analysed
    // before the first hover request arrives. Without this, executeHoverProvider
    // returns empty because Roslyn hasn't had time to process the file.
    const preOpenGenerated = (csxPath) => {
        const genPath = csxPath.replace(/\.csx$/, '.generated.cs');
        if (fs.existsSync(genPath)) {
            vscode.workspace.openTextDocument(vscode.Uri.file(genPath)).then(undefined, () => { });
        }
    };
    context.subscriptions.push(vscode.workspace.onDidOpenTextDocument((doc) => {
        if (isCsxFile(doc)) {
            preOpenGenerated(doc.uri.fsPath);
            if (!client && !clientStarting) {
                tryStartLanguageServer(context, doc.uri.fsPath);
            }
        }
    }));
    const openCSX = vscode.workspace.textDocuments.find(isCsxFile);
    if (openCSX) {
        preOpenGenerated(openCSX.uri.fsPath);
        if (!client && !clientStarting) {
            tryStartLanguageServer(context, openCSX.uri.fsPath);
        }
    }
    // ── CSSS completions (in-process, no separate server needed) ─────────────
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ scheme: 'file', language: 'csss' }, new CSSSCompletionProvider(), '$', '.', '#', ':', ' ', '\n'));
    // ── CSX element completions ────────────────────────────────────────────────
    context.subscriptions.push(vscode.languages.registerCompletionItemProvider({ scheme: 'file', language: 'csx' }, {
        provideCompletionItems(document, position, token) {
            console.log('[CSX Extension] Completion requested at', position.line, position.character);
            return new CSXCompletionProvider().provideCompletionItems(document, position);
        }
    }, '<'));
    // ── CSX hover: delegate to C# extension via .generated.cs ────────────────
    // Registered as a direct provider (not middleware) so it works even before
    // the LSP client has fully connected. Returns null when the generated file
    // isn't ready; VS Code then shows nothing for that position.
    // The LSP middleware suppresses the in-process Roslyn hover to avoid duplicates.
    context.subscriptions.push(vscode.languages.registerHoverProvider({ scheme: 'file', language: 'csx' }, {
        async provideHover(document, position) {
            return delegateHoverToGenerated(document, position);
        }
    }));
    // ── CSSS hover ────────────────────────────────────────────────────────────
    context.subscriptions.push(vscode.languages.registerHoverProvider({ scheme: 'file', language: 'csss' }, new CSSSHoverProvider()));
    // ── CSSS go-to-definition (for $variables) ────────────────────────────────
    context.subscriptions.push(vscode.languages.registerDefinitionProvider({ scheme: 'file', language: 'csss' }, new CSSSDefinitionProvider()));
    // ── CSSS diagnostics ──────────────────────────────────────────────────────
    const csssCollection = vscode.languages.createDiagnosticCollection('csss');
    context.subscriptions.push(csssCollection);
    const updateCSSSDiags = (doc) => {
        if (doc.languageId === 'csss')
            updateCSSSDiagnostics(doc, csssCollection);
    };
    context.subscriptions.push(vscode.workspace.onDidOpenTextDocument(updateCSSSDiags), vscode.workspace.onDidChangeTextDocument(e => updateCSSSDiags(e.document)), vscode.workspace.onDidCloseTextDocument(doc => csssCollection.delete(doc.uri)));
    // Seed diagnostics for already-open CSSS files
    vscode.workspace.textDocuments.forEach(updateCSSSDiags);
    // ── CSSS color provider ────────────────────────────────────────────────────
    context.subscriptions.push(vscode.languages.registerColorProvider({ scheme: 'file', language: 'csss' }, {
        provideDocumentColors(document) {
            return parseCsssColors(document);
        },
        provideColorPresentations(color, ctx) {
            const hex = colorToHex(color);
            const presentations = [
                new vscode.ColorPresentation(hex),
            ];
            // Also offer rgb/rgba form
            const r = Math.round(color.red * 255);
            const g = Math.round(color.green * 255);
            const b = Math.round(color.blue * 255);
            if (color.alpha < 1) {
                presentations.push(new vscode.ColorPresentation(`rgba(${r}, ${g}, ${b}, ${color.alpha.toFixed(2)})`));
            }
            else {
                presentations.push(new vscode.ColorPresentation(`rgb(${r}, ${g}, ${b})`));
            }
            return presentations;
        },
    }));
    // ── CSSS formatter ────────────────────────────────────────────────────────
    context.subscriptions.push(vscode.languages.registerDocumentFormattingEditProvider({ scheme: 'file', language: 'csss' }, {
        provideDocumentFormattingEdits(document, options) {
            const formatted = formatCsss(document.getText(), options.tabSize, options.insertSpaces);
            const fullRange = new vscode.Range(document.positionAt(0), document.positionAt(document.getText().length));
            return [new vscode.TextEdit(fullRange, formatted)];
        },
    }));
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
        if (isCsxFile(doc)) {
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
            const prop = CSSS_PROPS.find((p) => p.name === valueMatch[1]);
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
        return CSSS_PROPS.map((p) => {
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
// ── CSSS hover provider ───────────────────────────────────────────────────────
const CSS_PROP_NAMES = new Set(CSSS_PROPS.map(p => p.name));
class CSSSHoverProvider {
    provideHover(document, position) {
        // Check if hovering over a $variable
        const varRange = document.getWordRangeAtPosition(position, /\$[\w-]+/);
        if (varRange) {
            const varName = document.getText(varRange);
            const text = document.getText();
            const declRe = new RegExp(`^\\s*(\\${varName})\\s*:\\s*(.+?)\\s*;`, 'm');
            const match = declRe.exec(text);
            if (match) {
                const md = new vscode.MarkdownString();
                md.appendCodeblock(`${varName}: ${match[2]}`, 'scss');
                return new vscode.Hover(md, varRange);
            }
            return null;
        }
        // Check if hovering over a property name (word before `:` at start of line)
        const wordRange = document.getWordRangeAtPosition(position, /[\w-]+/);
        if (!wordRange)
            return null;
        const word = document.getText(wordRange);
        const line = document.lineAt(position).text;
        const beforeWord = line.slice(0, wordRange.start.character);
        const afterWord = line.slice(wordRange.end.character);
        if (/^\s*$/.test(beforeWord) && /^\s*:/.test(afterWord)) {
            const prop = CSSS_PROPS.find(p => p.name === word);
            if (prop) {
                const md = new vscode.MarkdownString();
                md.appendCodeblock(prop.name, 'css');
                md.appendText(prop.detail);
                if (prop.values.length > 0) {
                    md.appendMarkdown(`\n\n**Values:** \`${prop.values.join('`, `')}\``);
                }
                return new vscode.Hover(md, wordRange);
            }
        }
        return null;
    }
}
// ── CSSS go-to-definition ─────────────────────────────────────────────────────
class CSSSDefinitionProvider {
    provideDefinition(document, position) {
        const varRange = document.getWordRangeAtPosition(position, /\$[\w-]+/);
        if (!varRange)
            return null;
        const varName = document.getText(varRange);
        const lines = document.getText().split('\n');
        for (let i = 0; i < lines.length; i++) {
            const m = lines[i].match(/^\s*(\$[\w-]+)\s*:/);
            if (m && m[1] === varName) {
                const col = lines[i].indexOf(varName);
                return new vscode.Location(document.uri, new vscode.Range(i, col, i, col + varName.length));
            }
        }
        return null;
    }
}
// ── CSX completion provider ─────────────────────────────────────────────────
class CSXCompletionProvider {
    provideCompletionItems(document, position) {
        const lineText = document.lineAt(position).text;
        const before = lineText.slice(0, position.character);
        // Only provide completions after '<' or when typing element name
        if (!before.endsWith('<') && !/<[\w]*$/.test(before)) {
            return [];
        }
        return PAPER_ELEMENTS.map(el => {
            const item = new vscode.CompletionItem(el, vscode.CompletionItemKind.Class);
            item.detail = PAPER_ELEMENT_INFO[el] || 'Paper element';
            item.insertText = new vscode.SnippetString(`${el} \${1:/>}`);
            item.insertText.appendText('>');
            if (el !== 'Fragment' && el !== 'Viewport') {
                item.insertText.appendTabstop(2);
                item.insertText.appendText('</');
                item.insertText.appendText(el);
                item.insertText.appendTabstop(0);
            }
            return item;
        });
    }
}
// ── CSX hover provider ─────────────────────────────────────────────────────
class CSXHoverProvider {
    provideHover(document, position) {
        const wordRange = document.getWordRangeAtPosition(position, /[\w]+/);
        if (!wordRange)
            return null;
        const word = document.getText(wordRange);
        if (!PAPER_ELEMENTS.includes(word))
            return null;
        // Only show Paper element docs when the word is used as a JSX tag (<List, </List).
        // In C# type context (List<string>, etc.) the C# extension hover is correct.
        const before = document.lineAt(position).text.slice(0, wordRange.start.character);
        if (!/<\/?$/.test(before.trimEnd()))
            return null;
        const md = new vscode.MarkdownString();
        md.appendCodeblock(word, 'tsx');
        md.appendText(PAPER_ELEMENT_INFO[word] || 'Paper element');
        return new vscode.Hover(md, wordRange);
    }
}
// ── CSSS diagnostics ──────────────────────────────────────────────────────────
function updateCSSSDiagnostics(doc, collection) {
    const text = doc.getText();
    const lines = text.split('\n');
    const diagnostics = [];
    // Collect all declared $variables
    const declaredVars = new Set();
    const varDeclRe = /^\s*(\$[\w-]+)\s*:/gm;
    let m;
    while ((m = varDeclRe.exec(text)) !== null)
        declaredVars.add(m[1]);
    let depth = 0;
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];
        // Track block depth
        for (const ch of line) {
            if (ch === '{')
                depth++;
            else if (ch === '}')
                depth--;
        }
        // Skip comment lines
        if (/^\s*\/\//.test(line))
            continue;
        if (depth > 0) {
            // Check for unknown property names  (word followed by `:` at line start)
            const propMatch = line.match(/^\s*([\w-]+)\s*:/);
            if (propMatch) {
                const propName = propMatch[1];
                if (!propName.startsWith('$') && !CSS_PROP_NAMES.has(propName)) {
                    const col = line.indexOf(propName);
                    diagnostics.push(new vscode.Diagnostic(new vscode.Range(i, col, i, col + propName.length), `Unknown CSS property '${propName}'`, vscode.DiagnosticSeverity.Warning));
                }
            }
            // Check for undefined $variable references
            const varRefRe = /\$([\w-]+)/g;
            let vm;
            while ((vm = varRefRe.exec(line)) !== null) {
                const ref = '$' + vm[1];
                if (!declaredVars.has(ref)) {
                    const col = vm.index;
                    diagnostics.push(new vscode.Diagnostic(new vscode.Range(i, col, i, col + ref.length), `Undefined variable '${ref}'`, vscode.DiagnosticSeverity.Warning));
                }
            }
        }
    }
    collection.set(doc.uri, diagnostics);
}
// ── CSSS color provider ────────────────────────────────────────────────────────
/** Parses all color values in a CSSS document for the color picker API. */
function parseCsssColors(document) {
    const text = document.getText();
    const result = [];
    // hex colors: #rgb, #rgba, #rrggbb, #rrggbbaa
    const hexRe = /#([0-9a-fA-F]{3,8})\b/g;
    let m;
    while ((m = hexRe.exec(text)) !== null) {
        const hex = m[1];
        let r, g, b, a = 1;
        if (hex.length === 3 || hex.length === 4) {
            r = parseInt(hex[0] + hex[0], 16) / 255;
            g = parseInt(hex[1] + hex[1], 16) / 255;
            b = parseInt(hex[2] + hex[2], 16) / 255;
            if (hex.length === 4)
                a = parseInt(hex[3] + hex[3], 16) / 255;
        }
        else if (hex.length === 6 || hex.length === 8) {
            r = parseInt(hex.slice(0, 2), 16) / 255;
            g = parseInt(hex.slice(2, 4), 16) / 255;
            b = parseInt(hex.slice(4, 6), 16) / 255;
            if (hex.length === 8)
                a = parseInt(hex.slice(6, 8), 16) / 255;
        }
        else
            continue;
        const pos = document.positionAt(m.index);
        const endPos = document.positionAt(m.index + m[0].length);
        result.push(new vscode.ColorInformation(new vscode.Range(pos, endPos), new vscode.Color(r, g, b, a)));
    }
    // rgb(r, g, b) and rgba(r, g, b, a)
    const rgbRe = /rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})(?:\s*,\s*([\d.]+))?\s*\)/g;
    while ((m = rgbRe.exec(text)) !== null) {
        const r = parseInt(m[1]) / 255;
        const g = parseInt(m[2]) / 255;
        const b = parseInt(m[3]) / 255;
        const a = m[4] !== undefined ? parseFloat(m[4]) : 1;
        const pos = document.positionAt(m.index);
        const endPos = document.positionAt(m.index + m[0].length);
        result.push(new vscode.ColorInformation(new vscode.Range(pos, endPos), new vscode.Color(r, g, b, a)));
    }
    return result;
}
function colorToHex(color) {
    const toHex = (v) => Math.round(v * 255).toString(16).padStart(2, '0');
    const hex = toHex(color.red) + toHex(color.green) + toHex(color.blue);
    return color.alpha < 1 ? '#' + hex + toHex(color.alpha) : '#' + hex;
}
// ── CSSS formatter ─────────────────────────────────────────────────────────────
/** Simple CSSS formatter: normalises indentation and spacing. */
function formatCsss(text, tabSize, insertSpaces) {
    const indent = insertSpaces ? ' '.repeat(tabSize) : '\t';
    const lines = text.split('\n');
    const out = [];
    let depth = 0;
    for (let i = 0; i < lines.length; i++) {
        const raw = lines[i].trim();
        if (raw === '') {
            out.push('');
            continue;
        }
        // Comment lines — preserve as-is with current indent
        if (raw.startsWith('//') || raw.startsWith('/*') || raw.startsWith('*')) {
            out.push(indent.repeat(depth) + raw);
            continue;
        }
        // Opening brace at end of selector line
        if (raw.endsWith('{')) {
            out.push(indent.repeat(depth) + raw);
            depth++;
            continue;
        }
        // Closing brace
        if (raw === '}') {
            depth = Math.max(0, depth - 1);
            out.push(indent.repeat(depth) + '}');
            continue;
        }
        // Declaration: ensure single space after `:` and `;` at end
        if (depth > 0 && raw.includes(':') && !raw.startsWith('//') && !raw.startsWith('@')) {
            const colonIdx = raw.indexOf(':');
            const prop = raw.slice(0, colonIdx).trim();
            let val = raw.slice(colonIdx + 1).trim();
            if (!val.endsWith(';') && !val.endsWith('{') && !val.endsWith('}'))
                val += ';';
            out.push(indent.repeat(depth) + prop + ': ' + val);
            continue;
        }
        out.push(indent.repeat(depth) + raw);
    }
    return out.join('\n');
}
// ── Hover delegation to .generated.cs ────────────────────────────────────────
/**
 * Delegates a CSX hover request to the corresponding .generated.cs file so the
 * C# extension provides the hover (with full docs, semantic colours, etc.)
 * instead of our hand-rolled Roslyn implementation.
 */
async function delegateHoverToGenerated(document, position) {
    try {
        const generatedPath = document.uri.fsPath.replace(/\.csx$/, '.generated.cs');
        if (!fs.existsSync(generatedPath)) {
            console.error('[CSX Hover] generated file not found:', generatedPath);
            return null;
        }
        const csxLines = document.getText().split('\n');
        const genLines = fs.readFileSync(generatedPath, 'utf8').split('\n');
        const mappedPos = mapCsxToGeneratedPosition(csxLines, genLines, position);
        if (!mappedPos) {
            console.error('[CSX Hover] no mapped position for csx line', position.line);
            return null;
        }
        console.error(`[CSX Hover] csx(${position.line},${position.character}) -> gen(${mappedPos.line},${mappedPos.character}) "${genLines[mappedPos.line]?.trim().slice(0, 60)}"`);
        const generatedUri = vscode.Uri.file(generatedPath);
        const hovers = await vscode.commands.executeCommand('vscode.executeHoverProvider', generatedUri, mappedPos);
        console.error(`[CSX Hover] got ${hovers?.length ?? 'null'} hovers`);
        return hovers?.[0] ?? null;
    }
    catch (e) {
        console.error('[CSX Hover] error:', e);
        return null;
    }
}
/** Extracts the identifier word that contains column {@link col} in {@link line}. */
function getWordAt(line, col) {
    if (col >= line.length)
        return '';
    let start = col;
    while (start > 0 && /\w/.test(line[start - 1]))
        start--;
    let end = col;
    while (end < line.length && /\w/.test(line[end]))
        end++;
    return start < end ? line.slice(start, end) : '';
}
/**
 * Maps a position in the CSX source to the equivalent position in the generated C# file.
 *
 * The generated file preserves blank lines 1:1 with the CSX preamble body so that
 * generatedPreambleLine = csxLine + (genPreambleStart - csxPreambleStart).
 * For the function-declaration line, we word-match against the generated method signature.
 */
function mapCsxToGeneratedPosition(csxLines, genLines, position) {
    // Find the UINode function declaration line and preamble start in the CSX file
    let csxFuncDeclLine = -1;
    let csxPreambleStart = -1;
    for (let i = 0; i < csxLines.length; i++) {
        if (/^UINode[\w<>]*\s+\w/.test(csxLines[i].trim())) {
            csxFuncDeclLine = i;
            csxPreambleStart = i + 1;
            break;
        }
    }
    // Find the generated method signature line and preamble start
    let genMethodSigLine = -1;
    let genPreambleStart = -1;
    for (let i = 0; i < genLines.length; i++) {
        if (/public static UINode \w+\(Props props\)/.test(genLines[i])) {
            genMethodSigLine = i;
            for (let j = i + 1; j < Math.min(i + 3, genLines.length); j++) {
                if (genLines[j].trim() === '{') {
                    genPreambleStart = j + 1;
                    break;
                }
            }
            break;
        }
    }
    if (csxPreambleStart < 0 || genPreambleStart < 0)
        return null;
    // Skip injected lines at the top of the generated preamble (e.g. `var props = props.As<T>();`)
    // that have no counterpart in the CSX source.
    let injectedLines = 0;
    while (genPreambleStart + injectedLines < genLines.length &&
        /^\s+var \w+ = props\.As<[^>]+>\(\);/.test(genLines[genPreambleStart + injectedLines])) {
        injectedLines++;
    }
    const genBodyStart = genPreambleStart + injectedLines;
    const line = position.line;
    if (line === csxFuncDeclLine) {
        // Map words on the declaration line to the generated method signature by token matching
        if (genMethodSigLine < 0)
            return null;
        const word = getWordAt(csxLines[line], position.character);
        if (!word)
            return null;
        const idx = genLines[genMethodSigLine].indexOf(word);
        if (idx < 0)
            return null;
        return new vscode.Position(genMethodSigLine, idx);
    }
    if (line < csxPreambleStart)
        return null; // @import lines, blank lines before function
    const csxLineText = csxLines[line] ?? '';
    // Blank lines have no tokens to hover over
    if (csxLineText.trim() === '')
        return null;
    // Count non-blank CSX preamble lines that appear before the target line.
    // Using non-blank counting makes mapping resilient to blank lines being stripped:
    // the CLI writes .generated.cs with StringSplitOptions.RemoveEmptyEntries but
    // LsGeneratedFile.BuildContent preserves them — either way this works.
    let csxNonBlank = 0;
    for (let i = csxPreambleStart; i < line; i++) {
        if (csxLines[i].trim() !== '')
            csxNonBlank++;
    }
    // Find the csxNonBlank-th non-blank line in the generated method body
    let genSeen = 0;
    for (let i = genBodyStart; i < genLines.length; i++) {
        if (genLines[i].trim() === '')
            continue;
        if (genSeen === csxNonBlank) {
            const leadingWs = csxLineText.length - csxLineText.trimStart().length;
            const genCol = Math.max(0, position.character - leadingWs) + 12; // PreambleIndent = 12
            return new vscode.Position(i, genCol);
        }
        genSeen++;
    }
    return null;
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
        middleware: {
            // Suppress in-process Roslyn hover — the direct registerHoverProvider above
            // delegates to the C# extension on the .generated.cs file instead.
            // Returning null here prevents the LSP server from also showing hover,
            // which would create a duplicate popup alongside the C# extension hover.
            async provideHover(_document, _position, _token, _next) {
                return null;
            },
        },
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
import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { exec } from 'child_process';
import { LanguageClient, LanguageClientOptions, ServerOptions, TransportKind } from 'vscode-languageclient/node';

let client: LanguageClient | undefined;
let clientStarting = false;

// ── CSS property data (mirrors LSP for CSSS completions) ─────────────────────

interface CssProp {
    name: string;
    detail: string;
    values: string[];
}

const CSS_PROPS: CssProp[] = [
    { name: 'display',             detail: 'How element is rendered',       values: ['flex','block','inline','inline-flex','inline-block','grid','inline-grid','none'] },
    { name: 'flex-direction',      detail: 'Main axis direction',           values: ['row','column','row-reverse','column-reverse'] },
    { name: 'flex-wrap',           detail: 'Allow wrapping',                values: ['nowrap','wrap','wrap-reverse'] },
    { name: 'flex',                detail: 'flex shorthand',                values: [] },
    { name: 'flex-grow',           detail: 'How much to grow',              values: [] },
    { name: 'flex-shrink',         detail: 'How much to shrink',            values: [] },
    { name: 'justify-content',     detail: 'Alignment along main axis',     values: ['flex-start','flex-end','center','space-between','space-around','space-evenly'] },
    { name: 'align-items',         detail: 'Alignment along cross axis',    values: ['flex-start','flex-end','center','stretch','baseline'] },
    { name: 'align-self',          detail: 'Self cross-axis alignment',     values: ['auto','flex-start','flex-end','center','stretch','baseline'] },
    { name: 'align-content',       detail: 'Multi-line cross alignment',    values: ['flex-start','flex-end','center','stretch','space-between','space-around'] },
    { name: 'gap',                 detail: 'Row and column gap',            values: [] },
    { name: 'row-gap',             detail: 'Row gap',                       values: [] },
    { name: 'column-gap',          detail: 'Column gap',                    values: [] },
    { name: 'grid-template-columns', detail: 'Grid column track sizes',     values: [] },
    { name: 'grid-template-rows',  detail: 'Grid row track sizes',          values: [] },
    { name: 'grid-column',         detail: 'Column span e.g. 1 / 3',       values: [] },
    { name: 'grid-row',            detail: 'Row span',                      values: [] },
    { name: 'justify-items',       detail: 'Justify all items in grid',     values: ['start','end','center','stretch'] },
    { name: 'width',               detail: 'Width',                         values: ['auto'] },
    { name: 'height',              detail: 'Height',                        values: ['auto'] },
    { name: 'min-width',           detail: 'Minimum width',                 values: [] },
    { name: 'min-height',          detail: 'Minimum height',                values: [] },
    { name: 'max-width',           detail: 'Maximum width',                 values: [] },
    { name: 'max-height',          detail: 'Maximum height',                values: [] },
    { name: 'padding',             detail: 'All-sides padding',             values: [] },
    { name: 'padding-top',         detail: 'Top padding',                   values: [] },
    { name: 'padding-right',       detail: 'Right padding',                 values: [] },
    { name: 'padding-bottom',      detail: 'Bottom padding',                values: [] },
    { name: 'padding-left',        detail: 'Left padding',                  values: [] },
    { name: 'margin',              detail: 'All-sides margin',              values: [] },
    { name: 'margin-top',          detail: 'Top margin',                    values: [] },
    { name: 'margin-right',        detail: 'Right margin',                  values: [] },
    { name: 'margin-bottom',       detail: 'Bottom margin',                 values: [] },
    { name: 'margin-left',         detail: 'Left margin',                   values: [] },
    { name: 'background',          detail: 'Background color',              values: [] },
    { name: 'color',               detail: 'Text color',                    values: [] },
    { name: 'opacity',             detail: 'Opacity 0..1',                  values: [] },
    { name: 'visibility',          detail: 'Element visibility',            values: ['visible','hidden'] },
    { name: 'font-size',           detail: 'Font size',                     values: [] },
    { name: 'font-weight',         detail: 'Font weight',                   values: ['normal','bold'] },
    { name: 'font-family',         detail: 'Font family',                   values: [] },
    { name: 'line-height',         detail: 'Line height multiplier',        values: [] },
    { name: 'letter-spacing',      detail: 'Letter spacing',                values: [] },
    { name: 'text-align',          detail: 'Text alignment',                values: ['left','center','right'] },
    { name: 'text-overflow',       detail: 'Text overflow',                 values: ['ellipsis','clip'] },
    { name: 'text-decoration',     detail: 'Text decoration',               values: ['none','underline'] },
    { name: 'white-space',         detail: 'White-space handling',          values: ['normal','nowrap','pre','pre-wrap'] },
    { name: 'word-wrap',           detail: 'Word wrap',                     values: ['normal','break-word'] },
    { name: 'border',              detail: 'Border shorthand',              values: [] },
    { name: 'border-top',          detail: 'Top border',                    values: [] },
    { name: 'border-right',        detail: 'Right border',                  values: [] },
    { name: 'border-bottom',       detail: 'Bottom border',                 values: [] },
    { name: 'border-left',         detail: 'Left border',                   values: [] },
    { name: 'border-radius',       detail: 'Corner radius',                 values: [] },
    { name: 'border-width',        detail: 'Border width',                  values: [] },
    { name: 'border-color',        detail: 'Border color',                  values: [] },
    { name: 'border-style',        detail: 'Border style',                  values: ['solid','dashed','dotted','none'] },
    { name: 'overflow',            detail: 'Content overflow',              values: ['hidden','visible','scroll','auto','clip'] },
    { name: 'overflow-x',          detail: 'Horizontal overflow',           values: ['hidden','visible','scroll','auto','clip'] },
    { name: 'overflow-y',          detail: 'Vertical overflow',             values: ['hidden','visible','scroll','auto','clip'] },
    { name: 'position',            detail: 'Positioning scheme',            values: ['relative','absolute','fixed','sticky'] },
    { name: 'top',                 detail: 'Top offset',                    values: [] },
    { name: 'left',                detail: 'Left offset',                   values: [] },
    { name: 'right',               detail: 'Right offset',                  values: [] },
    { name: 'bottom',              detail: 'Bottom offset',                 values: [] },
    { name: 'z-index',             detail: 'Z stack order',                 values: [] },
    { name: 'cursor',              detail: 'Mouse cursor shape',            values: ['default','pointer','text','crosshair','grab','grabbing','not-allowed'] },
    { name: 'pointer-events',      detail: 'Receive pointer events',        values: ['auto','none'] },
    { name: 'transform',           detail: 'CSS transforms',                values: [] },
    { name: 'transition',          detail: "CSS transition e.g. 'all 0.2s'", values: [] },
    { name: 'box-sizing',          detail: 'Box sizing model',              values: ['border-box','content-box'] },
    { name: 'object-fit',          detail: 'Image fit mode',                values: ['contain','cover','fill'] },
    { name: 'box-shadow',          detail: 'Box shadow',                    values: [] },
    { name: 'direction',           detail: 'Text direction (ltr/rtl)',       values: ['ltr','rtl'] },
    { name: 'text-transform',     detail: 'Text transform',                values: ['none','uppercase','lowercase','capitalize'] },
    { name: 'font-style',          detail: 'Font style',                     values: ['normal','italic','oblique'] },
    { name: 'aspect-ratio',        detail: 'Width/height ratio',            values: [] },
    { name: 'translate-x',         detail: 'Translate X offset',             values: [] },
    { name: 'translate-y',         detail: 'Translate Y offset',             values: [] },
    { name: 'rotate',              detail: 'Rotation degrees',               values: [] },
    { name: 'scale-x',            detail: 'Scale X factor',                 values: [] },
    { name: 'scale-y',            detail: 'Scale Y factor',                 values: [] },
    { name: 'grid-area',          detail: 'Grid area name',                 values: [] },
    { name: 'grid-template-areas', detail: 'Grid template areas',            values: [] },
    { name: 'background-image',   detail: 'Background image URL',            values: [] },
    { name: 'object-position',    detail: 'Image position',                 values: [] },
    { name: 'user-select',        detail: 'Text selection',                 values: ['none','text','all'] },
    { name: 'transition-property', detail: 'Which properties to transition', values: [] },
    { name: 'transition-duration', detail: 'Transition duration (s/ms)',     values: [] },
    { name: 'transition-timing-function', detail: 'Easing function',          values: ['ease','linear','ease-in','ease-out','ease-in-out'] },
    { name: 'transition-delay',     detail: 'Transition delay (s/ms)',        values: [] },
    { name: 'animation',          detail: 'Animation shorthand',            values: [] },
    { name: 'animation-name',     detail: 'Animation name',                 values: [] },
    { name: 'animation-duration',  detail: 'Animation duration',            values: [] },
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
const PAPER_ELEMENT_INFO: Record<string, string> = {
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
function ensureCsxLanguageAssociation(): void {
    try {
        const config = vscode.workspace.getConfiguration();
        const assoc = config.get<Record<string, string>>('files.associations') ?? {};
        let changed = false;
        if (assoc['*.csx'] !== 'csx') {
            assoc['*.csx'] = 'csx';
            changed = true;
        }
        // Don't associate .generated.cs with csx - that causes the LSP to try to handle
        // C# files which breaks hover/completion for real .csx files
        if (changed) {
            config.update(
                'files.associations',
                assoc,
                vscode.ConfigurationTarget.Workspace
            );
        }

        // Also exclude .csx from C# analyzer via OmniSharp settings
        const csharpExcludes = config.get<Record<string, boolean>>('csharp.excludeFilesFromRegistration') ?? {};
        const excludeChanged = [
            '**/*.csx'
        ].reduce((acc, pattern) => {
            if (!csharpExcludes[pattern]) {
                csharpExcludes[pattern] = true;
                acc = true;
            }
            return acc;
        }, false);

        if (excludeChanged) {
            config.update(
                'csharp.excludeFilesFromRegistration',
                csharpExcludes,
                vscode.ConfigurationTarget.Workspace
            );
        }
    } catch { /* best effort */ }
}

export function activate(context: vscode.ExtensionContext) {
    console.log('CSX/CSSS Language Support activated');

    // Ensure *.csx is mapped to our 'csx' language, not 'csharp'.
    // Without this the C# extension analyses .csx files and generates spurious
    // diagnostics (red squiggles) on JSX syntax like <Box /> and =>.
    ensureCsxLanguageAssociation();

    // Start LSP lazily for CSX files
    const isCsxFile = (doc: vscode.TextDocument) =>
        doc.uri.scheme === 'file' && doc.uri.fsPath.endsWith('.csx');

    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument((doc) => {
            if (isCsxFile(doc) && !client && !clientStarting) {
                tryStartLanguageServer(context, doc.uri.fsPath);
            }
        })
    );
    const openCSX = vscode.workspace.textDocuments.find(isCsxFile);
    if (openCSX && !client && !clientStarting) {
        tryStartLanguageServer(context, openCSX.uri.fsPath);
    }

    // ── CSSS completions (in-process, no separate server needed) ─────────────
    context.subscriptions.push(
        vscode.languages.registerCompletionItemProvider(
            { scheme: 'file', language: 'csss' },
            new CSSSCompletionProvider(),
            '$', '.', '#', ':', ' ', '\n'
        )
    );

    // ── CSX element completions ────────────────────────────────────────────────
    context.subscriptions.push(
        vscode.languages.registerCompletionItemProvider(
            { scheme: 'file', language: 'csx' },
            {
                provideCompletionItems(document: vscode.TextDocument, position: vscode.Position, token: vscode.CancellationToken) {
                    console.log('[CSX Extension] Completion requested at', position.line, position.character);
                    return new CSXCompletionProvider().provideCompletionItems(document, position);
                }
            },
            '<'
        )
    );

    // ── CSX hover for element names ─────────────────────────────────────────
    context.subscriptions.push(
        vscode.languages.registerHoverProvider(
            { scheme: 'file', language: 'csx' },
            {
                provideHover(document: vscode.TextDocument, position: vscode.Position, token: vscode.CancellationToken) {
                    console.log('[CSX Extension] Hover requested at', position.line, position.character);
                    return new CSXHoverProvider().provideHover(document, position);
                }
            }
        )
    );

    // ── CSSS hover ────────────────────────────────────────────────────────────
    context.subscriptions.push(
        vscode.languages.registerHoverProvider(
            { scheme: 'file', language: 'csss' },
            new CSSSHoverProvider()
        )
    );

    // ── CSSS go-to-definition (for $variables) ────────────────────────────────
    context.subscriptions.push(
        vscode.languages.registerDefinitionProvider(
            { scheme: 'file', language: 'csss' },
            new CSSSDefinitionProvider()
        )
    );

    // ── CSSS diagnostics ──────────────────────────────────────────────────────
    const csssCollection = vscode.languages.createDiagnosticCollection('csss');
    context.subscriptions.push(csssCollection);

    const updateCSSSDiags = (doc: vscode.TextDocument) => {
        if (doc.languageId === 'csss') updateCSSSDiagnostics(doc, csssCollection);
    };
    context.subscriptions.push(
        vscode.workspace.onDidOpenTextDocument(updateCSSSDiags),
        vscode.workspace.onDidChangeTextDocument(e => updateCSSSDiags(e.document)),
        vscode.workspace.onDidCloseTextDocument(doc => csssCollection.delete(doc.uri))
    );
    // Seed diagnostics for already-open CSSS files
    vscode.workspace.textDocuments.forEach(updateCSSSDiags);

    // ── CSX compile commands ──────────────────────────────────────────────────
    context.subscriptions.push(
        vscode.commands.registerCommand('csx.compile', async () => {
            const editor = vscode.window.activeTextEditor;
            if (!editor) { vscode.window.showErrorMessage('No file is open'); return; }
            if (editor.document.languageId !== 'csx') {
                vscode.window.showErrorMessage('Current file is not a CSX file'); return;
            }
            await compileFile(editor.document.uri.fsPath);
        })
    );

    context.subscriptions.push(
        vscode.commands.registerCommand('csx.compileAll', async () => {
            let rootPath: string | undefined;
            const folders = vscode.workspace.workspaceFolders;
            if (folders?.length) {
                rootPath = folders[0].uri.fsPath;
            } else {
                const ed = vscode.window.activeTextEditor;
                if (ed?.document.languageId === 'csx') {
                    rootPath = path.dirname(ed.document.uri.fsPath);
                } else {
                    const csxDoc = vscode.workspace.textDocuments.find(
                        (d) => d.languageId === 'csx' && d.uri.scheme === 'file'
                    );
                    rootPath = csxDoc ? path.dirname(csxDoc.uri.fsPath) : undefined;
                }
            }
            if (!rootPath) {
                vscode.window.showErrorMessage('CSX: Open a workspace folder or a CSX file to run Compile All.');
                return;
            }
            await compileAllFiles(rootPath);
        })
    );

    // Auto-compile CSX on save
    context.subscriptions.push(
        vscode.workspace.onDidSaveTextDocument(async (doc) => {
            if (isCsxFile(doc)) {
                await compileFile(doc.uri.fsPath);
            }
        })
    );
}

// ── CSSS completion provider ──────────────────────────────────────────────────

class CSSSCompletionProvider implements vscode.CompletionItemProvider {
    provideCompletionItems(
        document: vscode.TextDocument,
        position: vscode.Position
    ): vscode.CompletionItem[] {
        const lineText = document.lineAt(position).text;
        const before   = lineText.slice(0, position.character);

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

    private isInsideDeclarationBlock(doc: vscode.TextDocument, pos: vscode.Position): boolean {
        // Walk back through the document counting { and } — if opens > closes, we're inside a block
        const text = doc.getText(new vscode.Range(new vscode.Position(0, 0), pos));
        let depth = 0;
        for (const ch of text) {
            if (ch === '{') depth++;
            else if (ch === '}') depth--;
        }
        return depth > 0;
    }

    private selectorCompletions(): vscode.CompletionItem[] {
        const items: vscode.CompletionItem[] = [];

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

    private cssPropertyCompletions(): vscode.CompletionItem[] {
        return CSS_PROPS.map((p) => {
            const item = new vscode.CompletionItem(p.name, vscode.CompletionItemKind.Property);
            item.detail = p.detail;
            if (p.values.length > 0) {
                item.insertText = new vscode.SnippetString(
                    `${p.name}: \${1|${p.values.join(',')}|};`
                );
            } else {
                item.insertText = new vscode.SnippetString(`${p.name}: \${0};`);
            }
            item.documentation = p.detail;
            return item;
        });
    }

    private variableCompletions(document: vscode.TextDocument): vscode.CompletionItem[] {
        const text  = document.getText();
        const items: vscode.CompletionItem[] = [];
        const seen  = new Set<string>();
        // Find all $variable declarations in the document
        const re = /\$([\w-]+)\s*:/g;
        let m: RegExpExecArray | null;
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

const CSS_PROP_NAMES = new Set(CSS_PROPS.map(p => p.name));

class CSSSHoverProvider implements vscode.HoverProvider {
    provideHover(document: vscode.TextDocument, position: vscode.Position): vscode.Hover | null {
        // Check if hovering over a $variable
        const varRange = document.getWordRangeAtPosition(position, /\$[\w-]+/);
        if (varRange) {
            const varName = document.getText(varRange);
            const text    = document.getText();
            const declRe  = new RegExp(`^\\s*(\\${varName})\\s*:\\s*(.+?)\\s*;`, 'm');
            const match   = declRe.exec(text);
            if (match) {
                const md = new vscode.MarkdownString();
                md.appendCodeblock(`${varName}: ${match[2]}`, 'scss');
                return new vscode.Hover(md, varRange);
            }
            return null;
        }

        // Check if hovering over a property name (word before `:` at start of line)
        const wordRange = document.getWordRangeAtPosition(position, /[\w-]+/);
        if (!wordRange) return null;

        const word      = document.getText(wordRange);
        const line      = document.lineAt(position).text;
        const beforeWord = line.slice(0, wordRange.start.character);
        const afterWord  = line.slice(wordRange.end.character);

        if (/^\s*$/.test(beforeWord) && /^\s*:/.test(afterWord)) {
            const prop = CSS_PROPS.find(p => p.name === word);
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

class CSSSDefinitionProvider implements vscode.DefinitionProvider {
    provideDefinition(document: vscode.TextDocument, position: vscode.Position): vscode.Location | null {
        const varRange = document.getWordRangeAtPosition(position, /\$[\w-]+/);
        if (!varRange) return null;

        const varName = document.getText(varRange);
        const lines   = document.getText().split('\n');
        for (let i = 0; i < lines.length; i++) {
            const m = lines[i].match(/^\s*(\$[\w-]+)\s*:/);
            if (m && m[1] === varName) {
                const col = lines[i].indexOf(varName);
                return new vscode.Location(
                    document.uri,
                    new vscode.Range(i, col, i, col + varName.length)
                );
            }
        }
        return null;
    }
}

// ── CSX completion provider ─────────────────────────────────────────────────

class CSXCompletionProvider implements vscode.CompletionItemProvider {
    provideCompletionItems(
        document: vscode.TextDocument,
        position: vscode.Position
    ): vscode.CompletionItem[] {
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

class CSXHoverProvider implements vscode.HoverProvider {
    provideHover(document: vscode.TextDocument, position: vscode.Position): vscode.Hover | null {
        const wordRange = document.getWordRangeAtPosition(position, /[\w]+/);
        if (!wordRange) return null;

        const word = document.getText(wordRange);
        if (PAPER_ELEMENTS.includes(word)) {
            const md = new vscode.MarkdownString();
            md.appendCodeblock(word, 'tsx');
            md.appendText(PAPER_ELEMENT_INFO[word] || 'Paper element');
            return new vscode.Hover(md, wordRange);
        }
        return null;
    }
}

// ── CSSS diagnostics ──────────────────────────────────────────────────────────

function updateCSSSDiagnostics(
    doc: vscode.TextDocument,
    collection: vscode.DiagnosticCollection
): void {
    const text        = doc.getText();
    const lines       = text.split('\n');
    const diagnostics: vscode.Diagnostic[] = [];

    // Collect all declared $variables
    const declaredVars = new Set<string>();
    const varDeclRe    = /^\s*(\$[\w-]+)\s*:/gm;
    let   m: RegExpExecArray | null;
    while ((m = varDeclRe.exec(text)) !== null) declaredVars.add(m[1]);

    let depth = 0;
    for (let i = 0; i < lines.length; i++) {
        const line = lines[i];

        // Track block depth
        for (const ch of line) {
            if (ch === '{') depth++;
            else if (ch === '}') depth--;
        }

        // Skip comment lines
        if (/^\s*\/\//.test(line)) continue;

        if (depth > 0) {
            // Check for unknown property names  (word followed by `:` at line start)
            const propMatch = line.match(/^\s*([\w-]+)\s*:/);
            if (propMatch) {
                const propName = propMatch[1];
                if (!propName.startsWith('$') && !CSS_PROP_NAMES.has(propName)) {
                    const col = line.indexOf(propName);
                    diagnostics.push(new vscode.Diagnostic(
                        new vscode.Range(i, col, i, col + propName.length),
                        `Unknown CSS property '${propName}'`,
                        vscode.DiagnosticSeverity.Warning
                    ));
                }
            }

            // Check for undefined $variable references
            const varRefRe = /\$([\w-]+)/g;
            let   vm: RegExpExecArray | null;
            while ((vm = varRefRe.exec(line)) !== null) {
                const ref = '$' + vm[1];
                if (!declaredVars.has(ref)) {
                    const col = vm.index;
                    diagnostics.push(new vscode.Diagnostic(
                        new vscode.Range(i, col, i, col + ref.length),
                        `Undefined variable '${ref}'`,
                        vscode.DiagnosticSeverity.Warning
                    ));
                }
            }
        }
    }

    collection.set(doc.uri, diagnostics);
}

// ── Generated-C# forwarding ───────────────────────────────────────────────────
// Maps a CSX (line, character) position to the corresponding position in the
// co-located .generated.cs file by dynamically scanning both files for their
// respective preamble start lines.

const PREAMBLE_COL_OFFSET = 8;

/** Find the first line of the preamble (method body) in a generated .cs file. */
function findGenPreambleStart(genLines: string[]): number {
    for (let i = 0; i < genLines.length; i++) {
        if (/^\s*public static UINode\s/.test(genLines[i])) {
            for (let j = i + 1; j < genLines.length; j++) {
                if (genLines[j].trim() === '{') return j + 1;
            }
        }
    }
    return 10; // safe fallback
}

/** Find the first preamble line in the CSX source (line after function declaration). */
function findCsxPreambleStart(lines: string[]): number {
    let i = 0;
    while (i < lines.length && lines[i].trimStart().startsWith('@import')) i++;
    for (let j = i; j < lines.length; j++) {
        if (/^\s*function\s+\w/.test(lines[j])) return j + 1;
    }
    return i;
}

function mapCsxToGeneratedPos(
    csxText: string,
    genLines: string[],
    pos: vscode.Position
): vscode.Position | null {
    const csxLines      = csxText.split('\n');
    const csxPreamble   = findCsxPreambleStart(csxLines);
    const genPreamble   = findGenPreambleStart(genLines);

    if (pos.line < csxPreamble) return null; // JSX / import area — not in generated preamble

    const origLine  = pos.line < csxLines.length ? csxLines[pos.line] : '';
    const leadingWs = origLine.length - origLine.trimStart().length;

    const genLine = (pos.line - csxPreamble) + genPreamble;
    const genChar = Math.max(0, pos.character - leadingWs) + PREAMBLE_COL_OFFSET;
    return new vscode.Position(genLine, genChar);
}

async function forwardHoverToGeneratedCs(
    document: vscode.TextDocument,
    position: vscode.Position
): Promise<vscode.Hover | null> {
    const genPath = document.uri.fsPath.replace(/\.csx$/, '.generated.cs');
    if (!fs.existsSync(genPath)) return null;

    const genText = fs.readFileSync(genPath, 'utf-8');
    const genLines = genText.split('\n');
    const genPos  = mapCsxToGeneratedPos(document.getText(), genLines, position);
    if (!genPos) return null;

    const genUri = vscode.Uri.file(genPath);
    const hovers = await vscode.commands.executeCommand<vscode.Hover[]>(
        'vscode.executeHoverProvider', genUri, genPos
    );
    return hovers && hovers.length > 0 ? hovers[0] : null;
}

async function forwardCompletionsToGeneratedCs(
    document: vscode.TextDocument,
    position: vscode.Position,
    triggerChar: string | undefined
): Promise<vscode.CompletionList | null> {
    const genPath = document.uri.fsPath.replace(/\.csx$/, '.generated.cs');
    if (!fs.existsSync(genPath)) return null;

    const genText  = fs.readFileSync(genPath, 'utf-8');
    const genLines = genText.split('\n');
    const genPos   = mapCsxToGeneratedPos(document.getText(), genLines, position);
    if (!genPos) return null;

    const genUri = vscode.Uri.file(genPath);
    const list   = await vscode.commands.executeCommand<vscode.CompletionList>(
        'vscode.executeCompletionItemProvider', genUri, genPos, triggerChar
    );
    return list && list.items.length > 0 ? list : null;
}

/** Map a position in the generated .cs file back to its original .csx position. */
function mapGeneratedToCsxPos(
    genText: string,
    csxText: string,
    genPos: vscode.Position
): vscode.Position | null {
    const genLines  = genText.split('\n');
    const csxLines  = csxText.split('\n');
    const genPreamble = findGenPreambleStart(genLines);
    const csxPreamble = findCsxPreambleStart(csxLines);

    if (genPos.line < genPreamble) return null;

    const csxLine = (genPos.line - genPreamble) + csxPreamble;
    if (csxLine >= csxLines.length) return null;

    const origLine  = csxLines[csxLine] || '';
    const leadingWs = origLine.length - origLine.trimStart().length;
    const csxChar   = Math.max(0, genPos.character - PREAMBLE_COL_OFFSET) + leadingWs;
    return new vscode.Position(csxLine, csxChar);
}

async function forwardDefinitionToGeneratedCs(
    document: vscode.TextDocument,
    position: vscode.Position
): Promise<vscode.Location[] | null> {
    const genPath = document.uri.fsPath.replace(/\.csx$/, '.generated.cs');
    if (!fs.existsSync(genPath)) return null;

    const genText  = fs.readFileSync(genPath, 'utf-8');
    const genLines = genText.split('\n');
    const genPos   = mapCsxToGeneratedPos(document.getText(), genLines, position);
    if (!genPos) return null;

    const genUri = vscode.Uri.file(genPath);
    const locs   = await vscode.commands.executeCommand<vscode.Location[]>(
        'vscode.executeDefinitionProvider', genUri, genPos
    );
    if (!locs || locs.length === 0) return null;

    const csxText = document.getText();
    return locs.map(loc => {
        if (loc.uri.fsPath === genPath) {
            const mappedStart = mapGeneratedToCsxPos(genText, csxText, loc.range.start);
            if (mappedStart) {
                const mappedEnd = mapGeneratedToCsxPos(genText, csxText, loc.range.end) ?? mappedStart;
                return new vscode.Location(document.uri, new vscode.Range(mappedStart, mappedEnd));
            }
        }
        return loc;
    });
}

async function forwardReferencesToGeneratedCs(
    document: vscode.TextDocument,
    position: vscode.Position
): Promise<vscode.Location[] | null> {
    const genPath = document.uri.fsPath.replace(/\.csx$/, '.generated.cs');
    if (!fs.existsSync(genPath)) return null;

    const genText  = fs.readFileSync(genPath, 'utf-8');
    const genLines = genText.split('\n');
    const genPos   = mapCsxToGeneratedPos(document.getText(), genLines, position);
    if (!genPos) return null;

    const genUri = vscode.Uri.file(genPath);
    const locs   = await vscode.commands.executeCommand<vscode.Location[]>(
        'vscode.executeReferenceProvider', genUri, genPos
    );
    if (!locs || locs.length === 0) return null;

    const csxText = document.getText();
    return locs.map(loc => {
        if (loc.uri.fsPath === genPath) {
            const mappedStart = mapGeneratedToCsxPos(genText, csxText, loc.range.start);
            if (mappedStart) {
                const mappedEnd = mapGeneratedToCsxPos(genText, csxText, loc.range.end) ?? mappedStart;
                return new vscode.Location(document.uri, new vscode.Range(mappedStart, mappedEnd));
            }
        }
        return loc;
    });
}

// ── LSP startup ───────────────────────────────────────────────────────────────

function tryStartLanguageServer(context: vscode.ExtensionContext, fromFilePath: string): void {
    const lsPath = getCSXLanguageServerPath(fromFilePath);
    if (!lsPath || !fs.existsSync(path.join(lsPath, 'Paper.CSX.LanguageServer.csproj'))) return;
    clientStarting = true;

    // Prefer the pre-built DLL (instant startup) — fall back to dotnet run (builds first)
    const tfm    = 'net10.0';
    const dllPath = path.join(lsPath, 'bin', 'Debug', tfm, 'Paper.CSX.LanguageServer.dll');
    const serverOptions: ServerOptions = fs.existsSync(dllPath)
        ? { command: 'dotnet', args: [dllPath], transport: TransportKind.stdio }
        : { command: 'dotnet', args: ['run', '--project', lsPath, '--no-build', '--'], transport: TransportKind.stdio };
    const clientOptions: LanguageClientOptions = {
        documentSelector: [{ scheme: 'file', language: 'csx' }],
        synchronize:      { fileEvents: vscode.workspace.createFileSystemWatcher('**/*.csx') },
        middleware: {
            // Forward hover to the generated .cs file so the C# extension provides
            // full documentation (XML docs, overloads, type signatures).
            // Falls back to our custom LSP hover for JSX-specific positions.
            async provideHover(document, position, token, next) {
                console.log('[CSX Middleware] provideHover called, calling next()');
                // For Paper elements (Box, Text, etc.) go straight to LSP — it has element docs.
                // For everything else forward to the generated .cs for C# type info.
                const wordRange = (document as vscode.TextDocument).getWordRangeAtPosition(position as vscode.Position);
                const word = wordRange ? (document as vscode.TextDocument).getText(wordRange) : '';
                const isPaperElement = PAPER_ELEMENTS.includes(word);
                if (!isPaperElement) {
                    try {
                        const csHover = await forwardHoverToGeneratedCs(
                            document as vscode.TextDocument,
                            position as vscode.Position
                        );
                        if (csHover) return csHover;
                    } catch { /* generated.cs not in project — fall through to LSP */ }
                }
                const result = await next(document, position, token);
                console.log('[CSX Middleware] next() returned', result ? 'hover' : 'null');
                return result;
            },

            // Forward go-to-definition to generated.cs (maps result back to .csx)
            async provideDefinition(document, position, token, next) {
                try {
                    const locs = await forwardDefinitionToGeneratedCs(
                        document as vscode.TextDocument,
                        position as vscode.Position
                    );
                    if (locs && locs.length > 0) return locs;
                } catch { /* fall through */ }
                return next(document, position, token);
            },

            // Forward find-references to generated.cs (maps results back to .csx)
            async provideReferences(document, position, context, token, next) {
                try {
                    const locs = await forwardReferencesToGeneratedCs(
                        document as vscode.TextDocument,
                        position as vscode.Position
                    );
                    if (locs && locs.length > 0) return locs;
                } catch { /* fall through */ }
                return next(document, position, context, token);
            },

            async provideCompletionItem(document, position, context, token, next) {
                console.log('[CSX Middleware] provideCompletionItem called');
                const result = await next(document, position, context, token);
                const count = result && 'items' in result ? result.items?.length : (Array.isArray(result) ? result.length : 0);
                console.log('[CSX Middleware] completion result:', result ? `${count} items` : 'null');
                return result;
            },
        },
    };
    client = new LanguageClient(
        'paperCSXLanguageServer',
        'Paper CSX Language Server',
        serverOptions,
        clientOptions
    );
    context.subscriptions.push(client);
    client.start().finally(() => { clientStarting = false; });
}

// ── Compile helpers ───────────────────────────────────────────────────────────

async function compileFile(filePath: string): Promise<void> {
    const cliPath = getCSXCliPath(filePath);
    if (!cliPath || !fs.existsSync(path.join(cliPath, 'Paper.CSX.Cli.csproj'))) {
        vscode.window.showErrorMessage(
            'CSX: Could not find Paper.CSX.Cli. Open a CSX file under the Paper repo, or set csx.cliPath in settings.'
        );
        return;
    }
    try {
        await executeCommand(`dotnet run --project "${cliPath}" -- parse "${filePath}"`);
        vscode.window.showInformationMessage(`Compiled ${path.basename(filePath)}`);
    } catch (error) {
        vscode.window.showErrorMessage(`Compilation failed: ${error}`);
    }
}

async function compileAllFiles(rootPath: string): Promise<void> {
    const cliPath = getCSXCliPath(rootPath);
    if (!cliPath || !fs.existsSync(path.join(cliPath, 'Paper.CSX.Cli.csproj'))) {
        vscode.window.showErrorMessage(
            'CSX: Could not find Paper.CSX.Cli. Set csx.cliPath or run from a folder under the Paper repo.'
        );
        return;
    }
    try {
        await executeCommand(`dotnet run --project "${cliPath}" -- build "${rootPath}"`);
        vscode.window.showInformationMessage('CSX compilation completed');
    } catch (error) {
        vscode.window.showErrorMessage(`Compilation failed: ${error}`);
    }
}

// ── Path resolution ───────────────────────────────────────────────────────────

function resolveRepoRootFromFile(fileOrDirPath: string): string | undefined {
    try {
        let dir: string;
        if (fs.existsSync(fileOrDirPath)) {
            dir = fs.statSync(fileOrDirPath).isDirectory() ? fileOrDirPath : path.dirname(fileOrDirPath);
        } else {
            dir = path.dirname(fileOrDirPath);
        }
        const root = path.parse(dir).root;
        while (dir && dir !== root) {
            const cliCsproj = path.join(dir, 'Paper.CSX.Cli',            'Paper.CSX.Cli.csproj');
            const lsCsproj  = path.join(dir, 'Paper.CSX.LanguageServer', 'Paper.CSX.LanguageServer.csproj');
            if (fs.existsSync(cliCsproj) && fs.existsSync(lsCsproj)) return dir;
            const parent = path.dirname(dir);
            if (parent === dir) break;
            dir = parent;
        }
    } catch { /* ignore */ }
    return undefined;
}

function getCSXCliPath(fromFilePath?: string): string | undefined {
    const configPath = vscode.workspace.getConfiguration('csx').get<string>('cliPath');
    if (configPath?.trim()) return path.resolve(configPath.trim());
    const repoRoot = fromFilePath ? resolveRepoRootFromFile(fromFilePath) : undefined;
    if (repoRoot) return path.join(repoRoot, 'Paper.CSX.Cli');
    return undefined;
}

function getCSXLanguageServerPath(fromFilePath?: string): string | undefined {
    const configPath = vscode.workspace.getConfiguration('csx').get<string>('languageServerPath');
    if (configPath?.trim()) return path.resolve(configPath.trim());
    const repoRoot = fromFilePath ? resolveRepoRootFromFile(fromFilePath) : undefined;
    if (repoRoot) return path.join(repoRoot, 'Paper.CSX.LanguageServer');
    return undefined;
}

function executeCommand(command: string): Promise<string> {
    return new Promise((resolve, reject) => {
        exec(command, (error, stdout, stderr) => {
            if (error) reject(stderr || error.message);
            else       resolve(stdout);
        });
    });
}

export function deactivate() {
    console.log('CSX/CSSS Language Support deactivated');
    return client?.stop();
}

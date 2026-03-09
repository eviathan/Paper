# CSX Language Support

Language support for Paper CSX files. **Compile on save** and **autocomplete** work when the extension finds the Paper repo by walking up from the current file path (no workspace required). You can also set `csx.cliPath` and `csx.languageServerPath` in settings.

## Use the extension from this repo (recommended)

VS Code may be using an **installed** copy of the extension (e.g. under `~/.vscode/extensions/`) which has old code. To run the version in this repo:

1. Open the **Paper** repo folder in VS Code (File → Open Folder → select the `Paper` directory).
2. Build the extension: in a terminal run  
   `cd vscode-extensions/csx-language && npm run compile`
3. Run **Launch CSX Extension (use this for CSX editing)** from the Run and Debug view (or F5).
4. A new VS Code window opens with this extension loaded. In that window, open your CSX file or the Paper folder. Save and autocomplete will use the CLI/LS found by walking up from the file path.

## Or install this version as the default

1. Uninstall the existing "CSX Language Support" / "paper.csx-language" extension if present.
2. Build and create the VSIX:
   ```bash
   cd vscode-extensions/csx-language
   npm run package
   ```
   This creates **`csx-language-1.0.0.vsix`** in that folder (e.g. `Paper/vscode-extensions/csx-language/csx-language-1.0.0.vsix`).
3. In VS Code: Extensions view → ⋯ menu → **Install from VSIX…** → choose `csx-language-1.0.0.vsix`.

After that, opening any CSX file under the Paper repo (with or without a workspace) will find the CLI and language server by searching upward from the file path.

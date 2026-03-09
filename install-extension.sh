#!/bin/bash
set -e

REPO="$(cd "$(dirname "$0")" && pwd)"
EXT_DIR="$REPO/vscode-extensions/csx-language"
VSIX="$EXT_DIR/csx-language-1.1.0.vsix"

echo "==> Building language server..."
dotnet build "$REPO/Paper.CSX.LanguageServer" -c Debug --no-restore

echo "==> Packaging extension..."
cd "$EXT_DIR"
npm run package --silent

echo "==> Installing extension..."
code --install-extension "$VSIX" --force

echo ""
echo "Done. Reload VSCode window (Cmd+Shift+P → 'Developer: Reload Window') to activate."

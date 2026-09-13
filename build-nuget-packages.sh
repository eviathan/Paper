#!/usr/bin/env bash
# build-nuget-packages.sh — build and pack all of Paper's NuGet packages into the
# local feed. Lives at the repo root; safe to run on its own.
#
# Run this whenever Paper source changes. Consumers (e.g. Prism Engine's
# Prism.Engine.Rendering.Paper adapter) point their nuget.config at this local
# feed so they can restore and build without Paper's source on the machine.
#
# The version stays pinned at 1.0.0 during local dev — each run overwrites that
# version in the feed. NuGet extracts packages by id+version into
# ~/.nuget/packages/ once and never re-reads the feed for a version it already
# has, so this script also evicts the extracted paper.* folders from the
# global cache. Without that step, consumers keep compiling against the
# previous build and new APIs show up as CS0103.
#
# Local feed: ~/.nuget/paper-local/
set -e

REPO_ROOT="$(cd "$(dirname "$0")" && pwd)"
LOCAL_FEED="$HOME/.nuget/paper-local"
GLOBAL_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"

mkdir -p "$LOCAL_FEED"

# Projects to pack (order: leaves first so transitive deps are available).
# Paper.CSX/Paper.CSSS are packaged too (not bundled into HotReload) — a nuget
# ProjectReference to a non-packed project doesn't get flattened into the
# referencing package, it just becomes an unresolvable dependency edge, so
# they need to be real, independently-resolvable packages in this same feed.
PROJECTS=(
    "Paper.Core"
    "Paper.Layout"
    "Paper.Icons"
    "Paper.Rendering.Silk.NET"
    "Paper.CSX"
    "Paper.CSSS"
    "Paper.Rendering.Silk.NET.HotReload"
)

echo "Packing Paper packages → $LOCAL_FEED"
echo ""

for proj in "${PROJECTS[@]}"; do
    csproj="$REPO_ROOT/$proj/$proj.csproj"
    echo "  packing $proj..."
    dotnet pack "$csproj" -c Release -o "$LOCAL_FEED" --nologo -p:SymbolPackageFormat=snupkg \
        2>&1 | grep -E "^Build|error|warning|Successfully" || true
done

echo ""
echo "Evicting extracted paper.* packages from $GLOBAL_CACHE"
shopt -s nullglob
for dir in "$GLOBAL_CACHE"/paper.*; do
    [ -d "$dir" ] || continue
    rm -rf "$dir"
    echo "  - $(basename "$dir")"
done
shopt -u nullglob

echo ""
echo "Done. Packages in $LOCAL_FEED:"
ls "$LOCAL_FEED"/*.nupkg 2>/dev/null | xargs -I{} basename {} || echo "  (none found)"
echo ""
echo "Consumers using this feed will pick up the new build on their next"
echo "'dotnet restore' / 'dotnet build' — no version bump needed."

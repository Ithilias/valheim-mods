#!/usr/bin/env bash
# Builds mods and packages each as a Hexium/Thunderstore zip in dist/.
# Usage: ./build.sh [Mod ...] [-- extra dotnet build options]
# Without mod names every mod is built and dist/ is emptied first.
set -euo pipefail
cd "$(dirname "$0")"
command -v zip >/dev/null || { echo "build.sh needs zip; see the README." >&2; exit 1; }

mods=()
while [ $# -gt 0 ] && [ "$1" != "--" ]; do mods+=("${1%/}"); shift; done
[ "${1:-}" = "--" ] && shift
extra=("$@")

if [ ${#mods[@]} -eq 0 ]; then
  for manifest in */manifest.json; do mods+=("$(dirname "$manifest")"); done
  rm -rf dist
fi

for mod in "${mods[@]}"; do
  [ -f "$mod/manifest.json" ] || { echo "No mod named '$mod' (no $mod/manifest.json)." >&2; exit 1; }
done

for mod in "${mods[@]}"; do
  dotnet build "$mod" -c Release --nologo -v quiet "${extra[@]}"

  package="dist/$mod"
  rm -rf "$package" "dist/$mod.zip"
  mkdir -p "$package"
  cp "$mod/bin/Release/$mod.dll" "$package/"
  cp "$mod"/{manifest.json,icon.png,README.md,CHANGELOG.md} "$package/"
  (cd "$package" && zip -qr "../$mod.zip" .)
  echo "Packaged dist/$mod.zip"
done

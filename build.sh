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

all=0
if [ ${#mods[@]} -eq 0 ]; then
  for manifest in */manifest.json; do mods+=("$(dirname "$manifest")"); done
  all=1
fi

for mod in "${mods[@]}"; do
  [ -f "$mod/manifest.json" ] || { echo "No mod named '$mod' (no $mod/manifest.json)." >&2; exit 1; }
done

# The zip is what gets uploaded, CHANGELOG included, and a version can be uploaded only once:
# never package changes that are not released yet. Test builds go through dotnet build (README).
for mod in "${mods[@]}"; do
  version="$(grep -oP '"version_number"\s*:\s*"\K[^"]+' "$mod/manifest.json" || true)"
  if grep -qx '## Unreleased' "$mod/CHANGELOG.md"; then
    echo "$mod/CHANGELOG.md still has '## Unreleased'; release $mod first (bump version_number, rename the section)." >&2
    exit 1
  fi
  grep -qxF "## $version" "$mod/CHANGELOG.md" \
    || { echo "$mod/CHANGELOG.md has no '## $version' entry." >&2; exit 1; }
  [ -z "$(git status --porcelain -- "$mod")" ] \
    || echo "warning: $mod has uncommitted changes; the zip will not match what is committed." >&2
done

if [ "$all" -eq 1 ]; then rm -rf dist; fi

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

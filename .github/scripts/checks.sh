#!/usr/bin/env bash
# Repository checks, run on every pull request and locally before pushing.
# Usage: .github/scripts/checks.sh [base-ref]   (default: origin/main)
set -euo pipefail
shopt -s nullglob
cd "$(git rev-parse --show-toplevel)"

base="${1:-origin/main}"
failed=0
fail() { echo "::error::$*"; failed=1; }
fail_at() { echo "::error file=$1,line=$2::$3"; failed=1; } # file, line, message

git rev-parse -q --verify "$base^{commit}" >/dev/null \
  || { echo "::error::base ref '$base' not found; run git fetch origin main"; exit 1; }

# Versions and CHANGELOGs. A version already uploaded to Hexium cannot be uploaded again, and
# the CHANGELOG ships in the zip. Changes collect under '## Unreleased' until a release pull
# request bumps version_number and renames that section to the new version.
mb="$(git merge-base "$base" HEAD)"
# Lines of the CHANGELOG section under '## <name>', up to the next '## ' heading, blanks dropped.
section() { awk -v h="## $1" '$0 == h {p=1; next} /^## / {p=0} p' | grep -v '^[[:space:]]*$' || true; }
at_base() { git show "$mb:$1" 2>/dev/null || true; } # empty if the file is new
version_of() { grep -oP '"version_number"\s*:\s*"\K[^"]+' || true; }
# The shared build files go into every DLL.
shared="$(git diff --name-only "$base"...HEAD -- Directory.Build.props Directory.Build.targets)"

for manifest in */manifest.json; do
  mod="$(dirname "$manifest")"
  log="$mod/CHANGELOG.md"
  version="$(version_of <"$manifest")"

  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] \
    || fail "$manifest: version_number '$version' is not Major.Minor.Patch"

  # Headings are '## x.y.z' or '## Unreleased', newest first, and the current version has one.
  while read -r heading; do fail "$log: heading '$heading' is not '## Major.Minor.Patch' or '## Unreleased'"; done \
    < <(grep -E '^## ' "$log" | grep -vE '^## ([0-9]+\.[0-9]+\.[0-9]+|Unreleased)$' || true)
  grep -qxF "## $version" "$log" || fail "$log has no '## $version' entry"
  first="$(grep -m1 '^## ' "$log" || true)"
  [[ "$first" == "## Unreleased" || "$first" == "## $version" ]] \
    || fail "$log: '$first' is on top; '## Unreleased' or '## $version' must come first"
  if grep -qx '## Unreleased' "$log" && [ -z "$(section Unreleased <"$log")" ]; then
    fail "$log: '## Unreleased' is empty; add an entry or remove the heading"
  fi

  changed="$shared$(git diff --name-only "$base"...HEAD -- "$mod/src" "$mod"/*.csproj "$mod/icon.png")"
  old="$(at_base "$manifest" | version_of)"
  if [ "$version" != "$old" ]; then
    # Release: the version goes up and '## Unreleased' becomes the new version's entry.
    newest="$(printf '%s\n%s\n' "$old" "$version" | sort -V | tail -n1)"
    [ "$newest" = "$version" ] || fail "$mod: version_number $version is lower than $old on $base"
    ! grep -qx '## Unreleased' "$log" \
      || fail "$mod releases $version but $log still has '## Unreleased'; rename it to '## $version'"
    ! at_base "$log" | grep -qxF "## $version" \
      || fail "$log: '## $version' already existed on $base; write a new entry for this release"
    [ -n "$(section "$version" <"$log")" ] || fail "$log: the '## $version' entry is empty"
  elif [ -n "$changed" ]; then
    # A change without a release must be described for players under '## Unreleased'.
    [ "$(section Unreleased <"$log")" != "$(at_base "$log" | section Unreleased)" ] \
      || fail "$mod changed but the '## Unreleased' section of $log did not; describe the change there"
  fi
done

# BepInEx throws on these in config sections and keys, and the mod then silently does nothing:
# any of = \ " ' [ ] (escapes such as \n and \t show up as a backslash here), or whitespace at
# either end. Bracket order matters: ']' first and '[' last, or '[=' opens an equivalence class.
forbidden='[]\\"'"'"'=[]'
while IFS=$'\x1f' read -r file section key; do
  for name in "$section" "$key"; do
    if [[ "$name" =~ $forbidden ]]; then
      fail "$file: config name '$name' contains one of = \\ \" ' [ ] or an escape"
    elif [[ "$name" =~ ^[[:space:]] || "$name" =~ [[:space:]]$ ]]; then
      fail "$file: config name '$name' starts or ends with whitespace"
    fi
  done
done < <(git ls-files '*.cs' | xargs -r perl -0777 -ne \
  'while (/Config\.Bind\(\s*[\$\@]*"((?:[^"\\]|\\.)*)"\s*,\s*[\$\@]*"((?:[^"\\]|\\.)*)"/g) { print "$ARGV\x1f$1\x1f$2\n" }')

# Writing style: no em-dashes.
while IFS=: read -r file line _; do fail_at "$file" "$line" "em-dash; use a comma, colon or full stop"; done \
  < <(git grep -nI $'\xe2\x80\x94' -- . || true)

# No local machine paths in tracked files.
while IFS=: read -r file line _; do fail_at "$file" "$line" "local machine path"; done \
  < <(git grep -nIE '(^|[^A-Za-z0-9./])/(home|Users)/[A-Za-z]|/mnt/[a-z]/|[A-Za-z]:\\Users\\' \
        -- . ':!.github/scripts/checks.sh' || true)

# The pull request title becomes the squash commit message.
if [ -n "${PR_TITLE:-}" ]; then
  [[ "$PR_TITLE" =~ ^(feat|fix|docs|build|ci|chore|refactor|perf|test|style|revert)(\([a-z0-9-]+\))?!?:\ .+ ]] \
    || fail "PR title '$PR_TITLE' is not a Conventional Commit, e.g. 'fix(roundminimap): ...'"
fi

[ "$failed" -eq 0 ] && echo "All checks passed."
exit "$failed"

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

# Every mod whose code or icon changed needs a higher version and a CHANGELOG entry for it:
# a version already uploaded to Hexium cannot be uploaded again.
for manifest in */manifest.json; do
  mod="$(dirname "$manifest")"
  version="$(grep -oP '"version_number"\s*:\s*"\K[^"]+' "$manifest" || true)"

  [[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] \
    || fail "$manifest: version_number '$version' is not Major.Minor.Patch"
  grep -qE "^## ${version//./\\.}\$" "$mod/CHANGELOG.md" \
    || fail "$mod/CHANGELOG.md has no '## $version' entry"

  changed="$(git diff --name-only "$base"...HEAD -- "$mod/src" "$mod"/*.csproj "$mod/icon.png")"
  if [ -n "$changed" ] && git cat-file -e "$base:$manifest" 2>/dev/null; then
    old="$(git show "$base:$manifest" | grep -oP '"version_number"\s*:\s*"\K[^"]+' || true)"
    newest="$(printf '%s\n%s\n' "$old" "$version" | sort -V | tail -n1)"
    if [ "$version" = "$old" ]; then
      fail "$mod changed but its version is still $old; bump version_number in $manifest"
    elif [ "$newest" != "$version" ]; then
      fail "$mod: version_number $version is lower than $old on $base"
    fi
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

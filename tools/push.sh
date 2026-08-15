#!/bin/bash
# Takes the newest delivery bundle in this folder and puts it on GitHub.
#
# The whole handover in one command: clear the locks OneDrive leaves behind, fetch the
# bundle into from-claude, fast-forward main onto it, push. Run it from anywhere inside
# the working copy.
#
#   bash tools/push.sh              # newest claude-*.bundle in the repository root
#   bash tools/push.sh some.bundle  # that one
#
# It refuses rather than guesses: a merge that would not fast-forward stops and says so,
# because the alternative is a rebase nobody asked for at the moment they are least
# expecting it.

set -u

cd "$(git rev-parse --show-toplevel 2>/dev/null)" || { echo "not inside a git working copy"; exit 1; }

# The locks. OneDrive keeps handles on .git, so a git command that is interrupted — or one
# run by a tool that cannot delete inside .git — leaves index.lock or ORIG_HEAD.lock
# behind, and every later command refuses with "another git process seems to be running".
# Nothing here is running: this script is the only thing about to.
mkdir -p .git/stale-locks
for lock in .git/index.lock .git/ORIG_HEAD.lock .git/HEAD.lock .git/refs/heads/*.lock; do
  [ -e "$lock" ] && mv -f "$lock" ".git/stale-locks/$(basename "$lock").$(date +%s)" 2>/dev/null
done

bundle="${1:-}"
if [ -z "$bundle" ]; then
  bundle=$(ls -t claude-*.bundle 2>/dev/null | head -1)
fi

[ -n "$bundle" ] && [ -f "$bundle" ] || { echo "no bundle to push — pass one, or drop a claude-*.bundle here"; exit 1; }

echo "bundle:  $bundle"

git fetch "$bundle" 'refs/heads/main:refs/heads/from-claude' --force || exit 1

echo "from:    $(git log --oneline -1 from-claude)"
echo "main:    $(git log --oneline -1 main)"

branch=$(git rev-parse --abbrev-ref HEAD)
[ "$branch" = "main" ] || { echo "on '$branch', not main — switch first"; exit 1; }

# Untracked files the merge is about to write. They are byte for byte what it would
# write, and git still refuses rather than overwrite them, so they are cleared here —
# only the ones the merge actually names.
git diff --name-only --diff-filter=A HEAD from-claude 2>/dev/null | while read -r new; do
  [ -f "$new" ] && ! git ls-files --error-unmatch "$new" >/dev/null 2>&1 && rm -f "$new"
done

git merge --ff-only from-claude || {
  echo
  echo "not a fast-forward: main has commits the bundle does not."
  echo "the bundle was built on an older main, so ask for a rebuilt one rather than merging by hand."
  exit 1
}

git push origin main || exit 1

echo
echo "pushed:  $(git log --oneline -1)"

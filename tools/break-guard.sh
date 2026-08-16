#!/usr/bin/env bash
#
# Break one guard on purpose, run the tests that should notice, and put it back.
#
# The restore is `git checkout -- <file>`, which does not mean "undo what this script just
# did" — it means "make this file look like HEAD". On a tree with uncommitted work those are
# not the same thing, and the difference is every uncommitted change in that file, gone in
# about four seconds and gone quietly, because the output of a successful break looks exactly
# like the output of a successful break.
#
# That has now happened twice in this project. The second time cost one edit; the first time
# cost the source half of a whole milestone. So the check is not advice in a comment any more,
# it is the first thing this script does and it refuses.
#
# Usage:
#   tools/break-guard.sh <file> <filter> <<'EOF'
#   the exact text to replace
#   ---
#   what to replace it with
#   EOF

set -u

if [ "$#" -lt 2 ]; then
    echo "usage: $0 <file> <test-filter>" >&2
    exit 2
fi

file="$1"
filter="$2"

if [ -n "$(git status --porcelain)" ]; then
    echo "REFUSING: the tree is dirty." >&2
    echo >&2
    echo "Breaking a guard restores with 'git checkout', which restores to HEAD rather than" >&2
    echo "to a moment ago. Anything uncommitted in a touched file would be lost, silently." >&2
    echo "Commit first, then break things." >&2
    git status --short >&2
    exit 1
fi

payload="$(cat)"
from="${payload%%$'\n'---*}"
to="${payload#*$'\n'---$'\n'}"

python3 - "$file" "$from" "$to" <<'PY'
import sys
path, old, new = sys.argv[1], sys.argv[2], sys.argv[3]
body = open(path).read()
found = body.count(old)
if found != 1:
    sys.exit(f"the text to break appears {found} times in {path}, not once")
open(path, "w").write(body.replace(old, new))
PY

if [ $? -ne 0 ]; then
    echo "nothing was changed, so nothing needs restoring" >&2
    exit 1
fi

export PATH="$PATH:$HOME/.dotnet"

raw="$(timeout 600 dotnet test --nologo -v q --filter "$filter" 2>&1)"

echo "$raw" | grep -E "\[FAIL\]|error CS|Failed!|Passed!" | sed 's/^ */    /'

# A run that produced no summary at all did not pass — it died. That is a legitimate way for
# a guard to be caught (a broken recursion guard takes the whole process with it) and it must
# not be reported as silence, which is indistinguishable from "no test noticed".
if ! echo "$raw" | grep -qE "Failed!|Passed!"; then
    echo "    the run produced no summary — it did not finish. That is the guard being caught,"
    echo "    loudly, and it is worth seeing rather than swallowing."
fi

git checkout -- "$file"

# Said out loud, because a restore that silently did nothing is the failure this whole script
# exists to prevent.
if [ -n "$(git status --porcelain)" ]; then
    echo "WARNING: the tree is dirty after restoring. Look at it before doing anything else." >&2
    git status --short >&2
    exit 1
fi

echo "    restored"

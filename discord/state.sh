#!/usr/bin/env bash
#
# Keep the automation's state off `main`.
#
#   bash discord/state.sh load
#   bash discord/state.sh save "daily recap"
#
# State lives as `sync-state.json` on a branch of its own (`discord-state`),
# which nothing ever checks out. `main` then only moves when a human moves it,
# so `git push` stops racing the workflows.
#
# `save` writes the commit with plumbing — hash-object, mktree, commit-tree —
# so the working tree and the checked-out branch are never touched. No stashing,
# no checkout, nothing for a concurrent job to trip over.
#
# SEEDING: if the branch does not exist yet, `load` leaves whatever is committed
# at discord/.sync-state.json in place and the first `save` creates the branch
# from it. To re-seed later, delete the branch on GitHub — the next run will
# recreate it from main's copy.
#
# State is never worth failing a run over: every failure path here exits 0.

set -uo pipefail

BRANCH="${STATE_BRANCH:-discord-state}"
LOCAL="discord/.sync-state.json"
REMOTE_PATH="sync-state.json"
ACTION="${1:-}"

fetch_branch() {
  git fetch origin "+refs/heads/${BRANCH}:refs/remotes/origin/${BRANCH}" >/dev/null 2>&1 || true
}

case "$ACTION" in
  load)
    fetch_branch
    if git cat-file -e "origin/${BRANCH}:${REMOTE_PATH}" 2>/dev/null; then
      mkdir -p "$(dirname "$LOCAL")"
      git show "origin/${BRANCH}:${REMOTE_PATH}" > "$LOCAL"
      echo "state: loaded $(wc -c < "$LOCAL" | tr -d ' ') bytes from ${BRANCH}"
    else
      echo "state: no ${BRANCH} branch yet — seeding from the copy committed on this branch"
    fi
    ;;

  save)
    MSG="${2:-state update}"
    if [ ! -f "$LOCAL" ]; then
      echo "state: no ${LOCAL} to save"
      exit 0
    fi

    git config user.name  "github-actions[bot]"
    git config user.email "41898282+github-actions[bot]@users.noreply.github.com"

    for attempt in 1 2 3; do
      fetch_branch
      PARENT="$(git rev-parse --verify -q "refs/remotes/origin/${BRANCH}" || true)"

      # Unchanged? Then there is nothing to push and no commit to make.
      if [ -n "$PARENT" ] && git cat-file -e "origin/${BRANCH}:${REMOTE_PATH}" 2>/dev/null; then
        if git show "origin/${BRANCH}:${REMOTE_PATH}" | cmp -s - "$LOCAL"; then
          echo "state: unchanged, nothing to push"
          exit 0
        fi
      fi

      BLOB="$(git hash-object -w "$LOCAL")" || { echo "state: could not hash"; exit 0; }
      TREE="$(printf '100644 blob %s\t%s\n' "$BLOB" "$REMOTE_PATH" | git mktree)" || { echo "state: could not build tree"; exit 0; }

      if [ -n "$PARENT" ]; then
        COMMIT="$(git commit-tree "$TREE" -p "$PARENT" -m "${MSG}")"
      else
        COMMIT="$(git commit-tree "$TREE" -m "${MSG}")"
      fi
      [ -n "${COMMIT:-}" ] || { echo "state: could not build commit"; exit 0; }

      if git push -q origin "${COMMIT}:refs/heads/${BRANCH}" 2>/dev/null; then
        echo "state: pushed to ${BRANCH} (${MSG})"
        exit 0
      fi

      echo "state: another job updated ${BRANCH} first (attempt ${attempt}/3) — refetching"
      sleep 2
    done

    echo "state: gave up updating ${BRANCH}; the run itself was fine" >&2
    exit 0
    ;;

  *)
    echo "usage: state.sh load | save [message]" >&2
    exit 2
    ;;
esac

# How to write a post in here

**A devlog post is an update, not an overview.** It says what changed since the
last one and stops. Anyone reading has already read the previous posts;
re-explaining the market or the battle engine every time trains people to skim.

## The shape

1. **One thing explained properly** — the most interesting change, in a short
   paragraph. Usually a bug, a measurement, or a decision that went against the
   obvious answer.
2. **A bullet list** of everything else that landed. One line each.
3. **The test count.**
4. **What's still open**, briefly.

## Rules of thumb

- **One Discord message where possible.** Under 2000 characters. Two if the week
  genuinely earned it. If it's running to four, it's an overview and needs
  cutting.
- **Point at #milestones** for the long version rather than reproducing it. That
  is what that channel is for.
- **Don't restate the project.** No "MonMMO is a from-scratch client for…" —
  that lives in #welcome and gets synced separately.
- **Don't re-announce things already announced.** If a previous post covered it,
  it's history, not news.
- **Numbers, not adjectives.** "1938 tests", "34 of 66 effects", "62% fewer
  messages" — not "big improvements to stability".
- **Say what's unfinished or unproved.** The count printed rather than rounded
  up is the house voice; keep it.

## What not to write

A post that opens with a heading per subsystem and works through all of them.
That's the pinned channel copy's job, and `sync.js` already keeps it current.

## Checking before you post

```bash
node post.js "" posts/<file>.md --dry-run
```

It prints the message split. **More than two messages is the signal to cut.**

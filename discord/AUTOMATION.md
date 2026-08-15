# Keeping the server current

Two separate jobs, two separate tools.

| You want to… | Use | Effort |
|---|---|---|
| Fix the FAQ, add a known issue, reword a rule | edit `content.js` → push | one commit |
| Ship a writeup, devlog, or announcement | drop a `.md` in the repo → push | one commit |
| Announce a build | publish a GitHub release | zero |
| Keep the server alive on quiet weeks | nothing — it posts Mondays | zero |
| Post something right now, by hand | `node post.js …` | one command |

Everything is **idempotent**. Re-running a workflow, re-pushing a branch, or
running a script twice does not double-post. Each thing that goes out is
recorded in `.sync-state.json` by an id, and anything already recorded is
skipped.

---

## Install (one time)

Copy this whole folder into the repo as `discord/`, and the four files in
`workflows/` to `.github/workflows/`.

**First, find the repo.** `$REPO` below is a real path on your machine — set it
once and the rest copy-pastes:

```bash
find ~ -maxdepth 5 -type d -name monmmo 2>/dev/null    # where is it?
REPO="$HOME/dev/monmmo"                                # <- put the real path here
ls "$REPO/.git" >/dev/null && echo "found the repo"    # sanity check
```

No local clone yet?

```bash
git clone https://github.com/ZealSM/monmmo ~/dev/monmmo
REPO="$HOME/dev/monmmo"
```

Then:

```bash
cp -r discord-setup "$REPO/discord"
mkdir -p "$REPO/.github/workflows"
mv "$REPO/discord/workflows/"*.yml "$REPO/.github/workflows/"
rmdir "$REPO/discord/workflows"
rm -rf "$REPO/discord/node_modules" "$REPO/discord/.env"
```

That last line matters: `node_modules` does not belong in the repo, and `.env`
holds a live bot token. `.gitignore` covers both, but deleting them removes the
chance of a `git add -f` accident entirely.

Keep the clone **outside OneDrive**. OneDrive syncing a `.git` directory while
git is writing to it produces corrupted objects and phantom conflict files, and
it is a miserable thing to debug.

Then in the repo on GitHub, **Settings → Secrets and variables → Actions**:

| Type | Name | Value |
|---|---|---|
| Secret | `DISCORD_TOKEN` | the bot token |
| Secret | `GUILD_ID` | the server id |
| Variable | `PROJECT_NAME` | e.g. `Overworld` |

Commit `.sync-state.json` — it is not secret, and the workflows push updates to
it so runs on different days know what has already gone out. Keep `.env`
untracked; `.gitignore` already handles that.

---

## Updating what the channels say

**The old pin is never touched.** Edit `content.js`, push, and the changed
section is posted as a *new* message underneath the existing one and pinned as
well. `#rules` and `#faq` end up with a visible history of what they used to
say, which is the correct behaviour for a document people agreed to.

An update carries a small dated subtext line so the newest version is obvious:

> `Updated 2026-08-29 — the earlier pinned version above is kept for history.`

Preview before it goes anywhere:

```bash
node sync.js --dry-run          # what changed, nothing sent
node sync.js --only faq         # one section
node sync.js                    # send it
```

**Only sections whose text actually changed are posted.** A hash of each
section lives in `.sync-state.json`; reformatting the file or editing a comment
sends nothing. Running `sync.js` twice in a row does nothing the second time.

The first run after `setup-server.js` records a baseline and posts nothing —
otherwise it would repost all 24 sections as "updates". That is automatic; you
do not have to remember it.

If you ever *do* want in-place editing: `node sync.js --replace`. It edits the
bot's own messages rather than adding new ones. Not the default, and not what
the workflow does.

---

## Posting writeups

Write a markdown file with front matter:

```markdown
---
channel: devlog
title: The three flags
ping: devlog
thread: true
---

Sixteen milestones of work and the walkable world moved by four maps…
```

| Field | Does what |
|---|---|
| `channel` | `devlog`, `milestones`, `announcements`, `changelog`, `general`, `build-drops`, … |
| `title` | Becomes an `##` heading, and the thread name |
| `ping` | `devlog`, `build`, `playtest` — the opt-in roles. Omit for no ping. |
| `thread` | `true` opens a discussion thread on the post |
| `pin` | `true` pins it |
| `crosspost` | `true` publishes to servers following `#announcements` |

Commit it to `discord/posts/` and push. The workflow posts it and never posts it
again, even if you edit the file later.

`docs/milestones/*.md` is watched too, and gets special handling: posted to
`#milestones`, threaded, and pinged to `devlog pings`. A new milestone doc
announces itself.

By hand, any time:

```bash
node post.js devlog posts/2026-09-01-flags.md --dry-run   # see it first
node post.js devlog posts/2026-09-01-flags.md
node post.js announcements notes.md --crosspost --pin
echo "server going down for 10 min" | node post.js general -
```

Long posts are split at line boundaries automatically, and **a split never lands
inside a code block** — a 300-line log paste arrives as several messages, each
with its fences intact, rather than turning to soup halfway down.

---

## Build drops

Publish a GitHub release, or push a `v*` tag. The workflow posts to
`#build-drops` with the tag, short sha, test count, your release notes (or the
commit list since the previous tag), a thread for feedback, and a ping to
`build pings`. A terser entry goes to `#changelog` with no ping.

```bash
git tag v0.9 && git push --tags
```

---

## The weekly pulse

Mondays at 10:00 Vancouver, `#devlog` gets commits, files touched, lines
`+/-`, and the test count **with its week-on-week delta**. No ping — a
heartbeat that pings is a heartbeat people mute.

Two deliberate choices:

- **A quiet week still posts.** "No commits this week. Nothing shipped, nothing
  broken." A log that only appears in good weeks is not a log.
- **A falling test count is called out, not buried.** If the number drops, the
  post says so and asks whether that was a deletion or a regression.

GitHub cron is UTC and ignores daylight saving, so it drifts an hour in winter.
Change `0 17` to `0 18` in `discord-weekly.yml` if that bothers you.

---

## Checks

```bash
node verify.js
```

32 offline checks, no network, no token. Run it after editing `content.js` —
the sync workflow runs it before sending anything, so a message over Discord's
2000-character limit fails the build rather than half-posting.

It covers the permission merge (including a test literally named *THE GATE
LEAKED*), the AutoMod filter in both directions, message splitting, front
matter parsing, and the weekly post's honesty about bad news.

---

## Files

```
discord/
  setup-server.js   builds the server            (run once)
  content.js        all the channel copy         (edit this)
  sync.js           pushes copy changes          (append, never overwrite)
  post.js           posts a markdown file
  weekly.js         the Monday pulse
  lib.js            shared plumbing
  verify.js         32 offline checks
  posts/            markdown you want posted
  .sync-state.json  what has already gone out    (commit this)
  .env              token + server id            (never commit this)
```

---

## When something misfires

**A workflow ran but nothing appeared.** Check the Actions log — `post.js`
prints `already posted "<id>"` when it is skipping. Delete that id from
`.sync-state.json` and re-run to force it.

**A post went to the wrong channel.** The `channel:` front-matter key, or the
first CLI argument, wins. `node post.js <channel> <file>` overrides the file.

**Sync posted nothing after an edit.** Only text changes count. Changing a
comment or reindenting `content.js` doesn't alter any message, so nothing goes
out — that's the design.

**Sync reposted everything.** `.sync-state.json` was missing or reset, so it had
no baseline. Run `node sync.js --seed` to re-record without posting.

**`npm ci` fails in Actions.** There's no lockfile in the bundle. The workflow
falls back to `npm install` automatically; commit a `package-lock.json` if you
want the faster path.

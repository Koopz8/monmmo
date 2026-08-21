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

Keep `.env` untracked; `.gitignore` already handles that.

### Where state lives

What has already been posted is tracked in `.sync-state.json`. **The live copy
lives on a branch of its own — `discord-state` — which nothing ever checks
out.** Workflows read it at the start of a run and write it at the end, so
`main` only ever moves when *you* move it. No `[skip ci]` commits, no losing a
race to a robot between `git commit` and `git push`.

The write uses git plumbing (`hash-object`, `mktree`, `commit-tree`) rather than
a checkout, so the working tree is never touched and two workflows finishing at
once cannot corrupt each other — the second one refetches and retries, up to
three times. A state failure never fails the run; the worst case is a repeated
post next time.

The copy of `.sync-state.json` committed on `main` is only a **seed**. It is
used once, when the `discord-state` branch does not exist yet. After that the
branch wins and edits to main's copy are ignored.

**To reset state** — after a history rewrite, or to stop a stale baseline
producing a nonsense delta — delete the `discord-state` branch on GitHub. The
next run recreates it from main's copy.

```bash
git push origin --delete discord-state     # next run re-seeds from main
git fetch origin discord-state && git show origin/discord-state:sync-state.json   # read it
```

---

## Updating what the channels say

**By default the old pin is never touched.** Edit `content.js`, push, and the
changed section is posted as a *new* message underneath the existing one and
pinned as well, so the channel keeps a visible history of what it used to say.

**Two channels are the exception: `#welcome` and `#rules` are replaced.** The
bot's own messages there are deleted and the new copy posted clean. Those two are
reference documents rather than a log, and `#rules` is what Onboarding makes
people accept — nobody should have to work out which of three pinned versions
they agreed to.

Change the list with `REPLACE_CHANNELS="welcome,rules,faq"`, or `REPLACE_CHANNELS=""`
for none. Three guards sit behind it:

- **Only the bot's own messages** are ever deleted — the filter is on author id.
- **Only read-only channels** may be listed, checked by `verify.js`. In an open
  channel the bot might have posted something that wasn't copy.
- **A ceiling of 20.** More than that and it refuses and tells you to look first;
  `--force` overrides. Messages older than 14 days are deleted individually,
  because Discord's bulk delete silently refuses them.

`node sync.js --dry-run` prints `REPLACE` in red for those channels, so the
destructive path is never a surprise.

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

## The figures update themselves

The prose in `content.js` is written by hand. The **numbers in it are not.**

Anything that moves every few days — the test count, how far the automated walk
gets, how much of the script reads to an end — is written in the copy as a
placeholder:

```
**{{TESTS}} tests**, none of which need a cartridge.
It reaches **{{MAPS_REACHED}} of {{MAPS_TOTAL}} places**
```

`facts.js` reads those out of the repo's own notes — `claude/next-session-prompt.md`
for the *Where the reading stands* block and the floor table, and the newest
`claude/milestone-<n>-*.md` for its closing test count — and writes `.facts.json`.
`sync.js` fills them in.

```bash
node facts.js            # print what it found and which line each came from
node facts.js --write    # write .facts.json  (exit 10 = a figure moved)
node facts.js --check    # exit 1 if a figure is missing or two sources disagree
```

**`discord-facts.yml` runs this daily** and reposts only the channels that are
wiped and reposted — `#welcome`, `#plain-english`, `#rules`, `#open-roles`. The
appending channels are deliberately left out: a figure ticking up is not worth a
new pinned message stacked under the old one in four technical channels every few
days. Those still go out on push, when the prose changes.

### Three rules it is built around

**A missing figure is an error, never a blank.** If `facts.js` cannot find
something it refuses to write the file at all, `verify.js` then fails on
`no placeholder survives substitution`, and nothing posts. A channel saying
`** tests**` would go unnoticed for weeks; a red build does not.

**The test count is read from two places and they have to agree.** If the prompt
and the newest milestone disagree, the job goes red and posts nothing. It does
not pick a side and it does not average them.

**A number nothing computes cannot come back wrong**, which is worse than a
number that is stale. Every fact prints the file and line it was read from, so
you can go and look.

### Adding a figure

Add the pattern to `facts.js`, use `{{YOUR_KEY}}` in `content.js`, run
`node verify.js`. The check *every figure the copy asks for is one facts.js can
actually produce* fails if you use a placeholder nothing supplies.

### What this does not do

It does not write prose. A number changing is not a story, and the story is the
part worth reading — that still gets written by hand, in `content.js` for the
pinned copy and in `posts/` for a devlog update.

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

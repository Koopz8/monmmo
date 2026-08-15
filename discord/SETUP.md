# Discord server setup

One script builds the whole server: 9 roles, 6 categories, 25 channels, every
permission overwrite, 14 forum tags, all the pinned copy, and 4 AutoMod rules —
including a hard filter on ROM sharing.

It is **safe to re-run**. Anything that already exists by name is skipped.
Nothing in the script deletes a channel you made.

---

## 0. Pick a name

`PROJECT_NAME` shows up in the welcome message. Options that stay clear of the
IP line (your own scope doc: don't put a Pokémon name in the project or domain):

| Name | Why it works |
|---|---|
| **Overworld** | The engine term for the walkable map. Instantly legible to anyone who'd join, zero IP surface. Common word, so the domain is harder. |
| **Route Zero** | Implies a region that was never shipped. Ownable, searchable, sounds like a product. |
| **Metatile** | What your renderer actually pipelines. Dev-flavoured, unique, and the most searchable of the five. |
| **Tall Grass** | The safest possible name — two ordinary English words, one obvious logo, and everyone knows what it means. |
| **Cartridge** | Foregrounds the bring-your-own model, which is the thing you most want understood on sight. |

Default is `Overworld`. Change it in `.env`.

---

## 1. Make an empty server

Discord → **+** → **Create My Own** → skip the template. Don't reuse a server
that already has channels you care about.

Then get its ID: **User Settings → Advanced → Developer Mode: on**, then
right-click the server icon → **Copy Server ID**.

---

## 2. Make the bot

1. https://discord.com/developers/applications → **New Application**
2. **Bot** in the sidebar → **Reset Token** → copy it. You only see it once.
3. Turn **off** "Public Bot" — nobody else should be able to add it.

No privileged intents are needed. Leave all three toggles off.

---

## 3. Invite the bot

Replace `YOUR_APP_ID` (Developer Portal → **General Information** →
Application ID) and open this URL:

```
https://discord.com/api/oauth2/authorize?client_id=YOUR_APP_ID&permissions=8&scope=bot%20applications.commands
```

`permissions=8` is Administrator. The script needs it — it creates a role that
has Administrator, and Discord won't let a bot grant a permission it lacks.
You can remove the bot entirely once the run finishes.

---

## 4. Configure

```bash
cd discord-setup
cp .env.example .env
```

Edit `.env`:

```
DISCORD_TOKEN=the token from step 2
GUILD_ID=the id from step 1
PROJECT_NAME=Overworld
REPO_URL=https://github.com/ZealSM/monmmo
```

`.env` holds a live bot token. Don't commit it.

---

## 5. Run

```bash
npm install
node verify.js          # offline checks — 21 of them, no network
node setup-server.js --dry-run
node setup-server.js
```

`verify.js` catches the mistakes that are expensive to find in a half-built
server: a message over Discord's 2000-character limit, a permission in both
allow and deny, a gated category leaking, an AutoMod pattern that would block
ordinary dev conversation. Run it after any edit to `content.js`.

Takes about 40 seconds. Output is a line per object, marked `created` or
`exists`.

---

## 6. Five things to do by hand

The API can't do these.

1. **Server Settings → Roles** — drag **Operator** and **Archivist** above the
   bot's own role. Discord creates every new role below the creator's, so they
   land too low to moderate anyone.
2. **Give yourself Operator.**
3. **Server Settings → Onboarding** — require new members to accept **#rules**
   before they can talk. This is the single highest-value manual step; it makes
   rule 1 something people have actively agreed to.
4. **#welcome** — add three reactions and wire them to `devlog pings`,
   `build pings`, `playtest pings`. Carl-bot or Zeppelin does this in a minute,
   or use Onboarding's opt-in role prompts and skip a bot entirely.
5. **#commits** — Channel Settings → Integrations → Webhooks → New Webhook →
   copy the URL. On GitHub: Settings → Webhooks → Add webhook, paste the URL
   with `/github` appended, content type `application/json`.

---

## What gets built

**Roles**, high to low: Operator (admin) · Archivist (mods) · Cartographer
(contributors who've landed work) · Field Tester (alpha access) · Bug Hunter
(awarded for a confirmed repro) · three opt-in ping roles · Bots.

**📍 START HERE** — welcome, rules, announcements, changelog *(all locked)*
**🌿 THE LOBBY** — general, introductions, screenshots, off-topic, Lobby VC
**🔧 THE WORKSHOP** — devlog\*, milestones\*, engine-and-netcode, battle-engine,
data-and-extraction, suggestions, commits, Workshop VC
**🧪 THE TESTING GROUNDS** *(Field Tester only)* — build-drops, bug-reports
(forum), playtest-coordination, tester-lounge, Playtest VC
**🧭 THE FIELD GUIDE** — setup-help, faq, known-issues
**🗄️ THE BACK ROOM** *(staff only)* — staff-chat, triage, mod-log, Staff VC

\* locked to posts, open in threads — the log stays a log.

**#bug-reports forum tags** are your actual solution layout, so triage sorts
itself: `Core/Battle` `Core/World` `RomExtract` `Client` `Server` `rendering`
`UI` `save/persistence` `crash` `desync`, plus workflow tags `needs repro`
`confirmed` `fixed` `duplicate` (staff-only, so nobody self-marks confirmed).

**AutoMod**: a ROM-sharing filter (keywords, filenames, magnet links, "where do
I get" phrasings, known ROM sites) that blocks the message with an explanation
and alerts #mod-log; Discord's slur preset; mention spam; generic spam. Staff
are exempt from all four. The filter is tested both ways — `verify.js` asserts
it catches six real attempts and doesn't fire on seven real sentences from your
own milestone docs.

---

## Keeping it current afterwards

See **AUTOMATION.md**. Short version: edit `content.js` and push, and the
changed sections post themselves underneath the existing pins (the old pin is
never edited or removed). Markdown dropped in `posts/` posts itself. A GitHub
release announces itself in `#build-drops`. Mondays, `#devlog` gets a pulse with
the test count and its delta.

## Editing

- **Copy** → `content.js`. First message in each array is the one that gets
  pinned. Re-run the script; channels that already have a pin are left alone.
- **Structure** → `TREE` in `setup-server.js`.
- **Roles** → `ROLES`, listed lowest-first.
- **Filter** → `ROM_KEYWORDS` / `ROM_REGEX`. Discord only honours `*` at the
  start or end of a keyword; anything more flexible goes in the regex list.
  AutoMod uses Rust regex — no lookarounds, `(?i)` for case-insensitive.

Always `node verify.js` before `node setup-server.js`.

---

## If something goes wrong

**"An invalid token was provided"** — the token is wrong or was reset. Reset it
again and copy the whole string.

**"The bot is not in a server with id …"** — the invite in step 3 didn't land,
or `GUILD_ID` is a channel id rather than a server id.

**Error 50013 / Missing Permissions** — the bot's role is too low. Server
Settings → Roles, drag it near the top.

**"could not enable Community automatically"** — do it by hand: Server Settings
→ **Enable Community** → follow the four prompts → re-run the script. Without
it, #announcements and #bug-reports are created as plain text channels instead
of announcement and forum channels. Everything else is unaffected.

**A channel came out in the wrong category** — the script only matches channels
already inside the category it's building, so a stray same-named channel
elsewhere gets a duplicate rather than being moved. Delete the stray and re-run.

/**
 * All human-readable copy for the server, in one file so you can rewrite it
 * without touching the setup logic.
 *
 * Placeholders available in every string:
 *   {{PROJECT}}  - CONFIG.projectName
 *   {{REPO}}     - CONFIG.repoUrl
 *
 * Each entry is an array of messages. The FIRST message in each array is pinned.
 * Keep every individual message under 2000 characters (Discord's hard limit) --
 * the script checks this for you and refuses to run if one is too long.
 */

module.exports = {
  // ─────────────────────────────────────────────────────────────── START HERE
  welcome: [
`# {{PROJECT}}

A from-scratch multiplayer client for a Generation III cartridge. The world, the
sprites, the maps, the moves and the battle maths are all read out of **a copy of
the ROM you already own, on your own machine, at runtime**. The client ships none
of it. The server never sees any of it.

This is a solo engineering project, built in public, one measured milestone at a
time. **1,568 tests**, none of which need a cartridge. An authoritative server, a
Gen III battle engine accurate down to truncation order, and three things two
people can do to each other: **see each other, trade, and fight**.

It is also built to hold a crowd. A load generator that speaks the real protocol
found the wall was the *door*, not the game; the server now admits people at a
published rate it prints at startup, tells you only about what you could actually
see, writes only the parts of a save that changed, and runs **more than one copy
of a busy place** so forty people in a room never becomes four hundred.

**Where to go**

> **#rules** — read this first. One of them will get you banned instantly.
> **#announcements** — the only channel that will ever ping you unprompted.
> **#devlog** — what got built this week, and what it cost.
> **#milestones** — the long-form writeups. Start at the bottom.
> **#general** — say hello.
> **#setup-help** — stuck getting a build running.

**Roles you can pick up**

React or ask in #general for notification roles: **devlog pings**,
**build pings**, **playtest pings**. All three are opt-in and off by default.

**Field Tester** is granted, not requested — see #rules.

Repo: {{REPO}}`,
  ],

  rules: [
`# Rules

## 1. Never share, link, request, or offer a ROM. Ever.

This is the only rule with no warning attached. Posting a ROM, a link to one, a
torrent, a magnet link, a "DM me", or asking anyone here where to get one is an
**immediate permanent ban**, first offence, no appeal, whether or not you were
joking.

This is not us being precious. The entire legal footing of this project is that
**it distributes nothing and facilitates nothing** — you bring a cartridge dump
you made from hardware you own, and the client reads it locally. A single link in
this server converts "a tool" into "a distribution channel," and that is the
difference between being ignored and being sued. An automated filter blocks most
attempts; the ban is manual and it is not negotiable.

Talking *about* the ROM's data structures, offsets, tables and formats is the
entire point of this server and is completely fine. The line is the file.

**The remaining rules are in the message below.**`,

`## 2. No piracy adjacency

No emulator-site links, no "just search X," no pre-patched builds, no
distribution of extracted assets — sprites, music, text dumps, tilesets. What
your client extracts stays on your disk.

## 3. Nothing here is for sale

No monetization, no donations tied to anything in-game, no selling accounts,
items, or access. Anyone advertising a paid service related to this project is
removed.

## 4. Be a decent person

No harassment, bigotry, slurs, sexual content, or dogpiling. Disagreement is
welcome and expected — this project has changed direction four times because
someone pointed out the measurement was wrong. Contempt is not.

## 5. Keep channels on-topic

Wrong channel is a nudge, not a strike. Long tangents get a thread.

## 6. No unsolicited DMs to members

Especially not to staff about bugs — that's what #bug-reports is for.

## 7. Discord ToS and age 13+ apply

Standard, and enforced.

---

Staff are **Operator** and **Archivist**. Moderation decisions are logged.
If you think one was wrong, say so — politely, once, in a ticket or a DM to an
Archivist.`,
  ],

  announcements: [
`Announcements land here and nowhere else. This channel is locked so it stays
readable.

Pick up **devlog pings**, **build pings** or **playtest pings** in **#welcome**
if you want to be notified. Without them, nothing here will ever ping you.`,
  ],

  changelog: [
`Build-by-build changes, newest at the bottom. Every entry names the commit and
the test count at that commit, because a change that dropped the test count is a
change worth arguing about.

Format:

\`\`\`
### <version> — <commit sha> — <n> tests
Added    …
Fixed    …
Changed  …
Known    …
\`\`\``,
  ],

  // ──────────────────────────────────────────────────────────────── THE LOBBY
  general: [
`General chat. Anything project-adjacent, plus normal human conversation.

Deep technical threads are better off in **#engine-and-netcode**,
**#battle-engine** or **#data-and-extraction** — not because they're unwelcome
here, but because nobody can find them again afterwards.`,
  ],

  introductions: [
`Optional, one message, whatever you want it to be. A prompt if you'd like one:

> **Name/handle:**
> **How you found this:**
> **Do you write code, and if so what in:**
> **The Gen III thing you know an unreasonable amount about:**`,
  ],

  screenshots: [
`Screenshots, clips, and recordings of the client. Bugs that look funny go here;
bugs that need fixing go to **#bug-reports**.

Please don't post extracted assets on their own — a screenshot of the game
running is fine, a folder of ripped sprites is not.`,
  ],

  // ───────────────────────────────────────────────────────────── THE WORKSHOP
  devlog: [
`Written updates from the dev. Locked so the log stays a log — **every post here
opens a thread, and the thread is where you reply.**

If you want these to ping you, grab the **devlog pings** role in #welcome.`,
  ],

  milestones: [
`The long-form writeups. Each one is a single question the project had to answer,
what was measured, what the measurement got wrong, and what it cost to find out.

They are worth reading in order, and they are the honest record — including the
four separate times a roadmap was written from an instrument pointed at the wrong
thing.

Locked. Discussion happens in the thread on each post.`,
  ],

  'engine-and-netcode': [
`Movement, collision, warps, the authoritative server, prediction and correction,
the shared \`Core\` library, persistence.

The design bet the whole project rests on: **client and server run the same code
out of \`Core\`.** Movement is applied locally the instant a key is pressed and the
server almost never disagrees, so rejection is an exception path, not the normal
flow. No reconciliation, no rubber-banding.

**Three multiplayer verbs so far:** seeing each other, trading one thing each,
and fighting. Trade and duel are deliberately the same shape — one at a time,
an invitation that dies when either side walks away, and asking somebody who has
already asked you is how it begins. Two verbs that behave the same way are two
verbs a player only has to learn once.

**The scaling run (111–124)** rebuilt most of this underneath the game: a door
with a measured width, interest instead of broadcast, an index instead of a scan,
an outbound queue per connection, a sight circle, saves moved off the input path
and then reduced to only what changed, and **instancing** — past forty in a copy,
the next arrival opens another.

Instancing is a design decision, not an optimisation. Capping who gets drawn or
shrinking the sight circle both leave a crowd standing there that the player
cannot see and can walk into. **A copy has no crowd in it that anybody is being
lied to about.**

Open questions worth arguing about, any time:
- **A thousand players on hardware that could hold them.** Every number in the
  scaling notes is two cores, with the load generator sharing them with the
  server it measures. The only open question code cannot answer.
- A flag set in memory that the save does not have.
- Simulating maps nobody is standing on (walk away and back, and the street
  resets).`,
  ],

  'battle-engine': [
`Damage, type chart, stat stages, natures, turn order, status, the LCG, move
effects.

Two Gen III rules that silently poison everything if you get them wrong:
1. **Physical vs special is decided by the move's TYPE, not the move.** Normal
   through Steel draw on Attack; Fire onward on Special Attack. Hyper Beam being
   physical is the proof this is right.
2. **Critical hits double damage** and ignore stat stages favouring the defender.
   The 1.5x version is a generation later.

Every division truncates and the order matters.

**Recently modelled:** PP, which outlives the fight and cost the save format a
field. Held items — 112 species name what a wild one may be carrying, and the
data was extracted long before anything read it. Duels, which needed no engine
change at all.

**Still silent: ~119 move-effect groups** — and most of them should probably
stay that way. What is left is largely numbers nobody can derive from the
cartridge: FLAIL's power table, SONICBOOM's twenty. Inventing them would be
inventing the game rather than reading it, so the honest remainder is small.

That distinction matters more than the count. "Not modelled" and "cannot be
derived" are different problems, and only one of them is work.`,
  ],

  'data-and-extraction': [
`Structures, offsets, pointer tables, compression, tilesets, metatile behaviour,
scripts, flags, encounter tables.

**Data talk, not file talk.** Offsets, formats and findings: yes, always. Files,
links and "where do I get": see rule 1.

**The most useful question in this project right now is not "what should I build
next" — it's "what reads this?"** Five milestones running, the next thing built
turned out to be a field already extracted, already carried, and read by nothing:
a message with no sender, doors on no square, the byte saying who a move was
for, PP, and held items.

Still unread in the species table, if you want one: \`Ability1\`, \`Ability2\`, the
six EV yields, \`SafariZoneFleeRate\`, \`EggCycles\`, \`BaseFriendship\`,
\`EggGroup1/2\`, \`BodyColor\`. The EV yields are the biggest, and the one with a
real save-format cost — exactly like PP was.

Standing unsolved problems:
- **The font.** Four mechanical methods ruled out. The mapping is not identity,
  the sheet is not one of the four candidates, and the geometry may not be 8×8.
- **The box count.** Stated nowhere on the cartridge, so more than one box would
  have to be remembered rather than read.`,
  ],

  suggestions: [
`Ideas, feature requests, and "why is it like that". Use a thread per idea.

Two things worth knowing before you post:
- **Scope is Kanto on one hash-locked ROM.** More ROMs come after the whole
  stack works with one.
- **Nothing gets sold, ever.** Cosmetics, subs, and donation perks are all
  permanently off the table — that's a legal position, not a business one.`,
  ],

  commits: [
`Wire this channel to the GitHub repo:

**Channel Settings → Integrations → Webhooks → New Webhook → Copy URL**, then on
GitHub: **Settings → Webhooks → Add webhook**, paste the URL with \`/github\`
appended, content type \`application/json\`, and select the events you want
(pushes, PRs, releases).

Repo: {{REPO}}`,
  ],

  // ──────────────────────────────────────────────────────── THE TESTING GROUNDS
  'build-drops': [
`Builds land here. Locked — questions go to **#playtest-coordination**, breakage
goes to **#bug-reports**.

Every drop states: version, commit sha, test count, what's new, what's known
broken, and the expected ROM SHA-1.

**Builds are for you, not for redistribution.** Don't mirror them, don't post
them outside this server, don't hand them to someone who hasn't read #rules.
Losing Field Tester is the least of what happens.

Want a ping when one lands? Grab **build pings** in #welcome.`,
  ],

  'bug-reports': [
`Post one bug per thread. Tag it with the subsystem you *think* it's in — a wrong
guess is fine and better than no tag.

**Template — copy this:**

\`\`\`
**Build:**        version + commit sha from #build-drops
**What happened:**
**What should have happened:**
**Steps to reproduce:**
  1.
  2.
  3.
**Reproducible?**  every time / sometimes / once
**Map / species / move involved:**
**Log output:**   (paste, or attach the file)
\`\`\`

**Steps to reproduce is the whole report.** A bug that can't be reproduced can't
be fixed, and this project has already shipped one correction that could never
apply and one test that proved nothing — both of which a real repro would have
caught.

Attach the log. Screenshot the screen. Say the commit.

Confirmed repros earn the **Bug Hunter** role.`,
  ],

  'playtest-coordination': [
`Organising sessions: who's on, which build, what we're trying to break.

Sessions get scheduled events. Grab **playtest pings** in #welcome to hear about
them.

Useful things to try that aren't "play normally": desync hunting (two clients,
same map, one walks into the other), boundary walking, save-and-reload at odd
moments, and battling with moves in the long tail.`,
  ],

  'tester-lounge': [
`Off-topic for testers. No agenda.`,
  ],

  // ─────────────────────────────────────────────────────────── THE FIELD GUIDE
  'setup-help': [
`Trouble getting a build running. Post:

- your OS and version
- the build version and commit
- the exact error, as text if you can
- the SHA-1 your client reported for your ROM, if it got that far

**Do not post the ROM, its filename with a link, or ask where to obtain one.**
See rule 1. Asking *"my client says the hash doesn't match, what does that
mean"* is completely fine and is answered in #faq.`,
  ],

  faq: [
`# FAQ

**What is this?**
A multiplayer client for a Gen III Kanto, built from scratch — own renderer, own
netcode, own battle engine. Not an emulator: it reads data structures out of a
cartridge image and renders them with its own engine.

**Do I need a ROM?**
Yes. You supply your own, it is read locally on your machine, and it is never
uploaded anywhere. The client ships zero copyrighted data and the server stores
none. Where you get it is not a question anyone here will answer — see rule 1.

**Which ROM?**
One specific build, verified by SHA-1 on load. Anything else is refused, because
offsets are build-specific and a different revision produces silent garbage
rather than an error. More ROM support comes only after the whole stack works
with one.

**Is this an emulator?**
No. An emulator executes the cartridge's code. This reads its data and runs
entirely original code over it. The distinction is load-bearing and it is true in
fact, not just in the README.

**Can I play right now?**
Closed alpha. **Field Tester** is granted by staff — being helpful, patient, and
good at reproducing things is how people get it. Asking for it does not help.

**Will there be a cash shop / subs / donations?**
No. Never. Monetization is the single fastest way to turn "ignored" into "sued."

**Can I contribute code?**
Yes. Say so in #general. The **Cartographer** role goes to people who've landed
work.

**Is my progress safe?**
It is an alpha. Assume every save is disposable until told otherwise.

**Why not original creatures and a made-up region?**
Because art and content production is the thing that kills solo
creature-collectors. Everything visual already exists in the player's own file
and is extracted at runtime. What's left is pure engineering.`,
  ],

  'known-issues': [
`Standing issues, so they don't get reported forty times. Check here before
posting in #bug-reports.

**Known and open**
- **A flag set in memory that the save does not have.** The sharpest lead on this
  list — it has a reproduction path and an obvious symptom (a named trainer
  missing from a room).
- **The flag race.** Older, vaguer, possibly the same animal: a script's flags
  reach the server from the client. Still without evidence, and one previous
  sighting was withdrawn after turning out to be two events a second apart read
  as one.
- **The battle engine's silent half.** ~119 move-effect groups the engine steps
  over — though most are numbers nobody can derive (FLAIL's power table,
  SONICBOOM's twenty) and should stay silent rather than be invented.
- **A thousand players on real hardware.** Every scaling number so far is from
  two cores with the load generator sharing them. Needs a second machine; the
  only open question code cannot answer.
- **No shop yet** for the cosmetics that exist. Money is what makes wearing
  something a choice, and it's the last piece of a feature that has been
  three-quarters built for fifteen milestones.
- **Text rendering.** The cartridge's font has not been located. Four mechanical
  methods are ruled out; the mapping is not identity, and the geometry may not
  even be 8×8.
- **Storage.** More than one box is not implemented — the count is stated
  nowhere in the data, so it would have to be remembered rather than read.
- **Unsimulated maps.** Only maps with a player on them tick. Walk away and back
  and the townsfolk have reset to their starting positions.
- **Switching in a duel** is refused, and says so in the code rather than
  hiding it.

**Closed items and non-bugs are in the message below.**`,

`**Closed, so please stop reporting them**
- ~~The bedroom PC's behaviour byte.~~ There is no byte. The bedroom machine is
  scripted, not a tile — proved, not assumed.
- ~~Joining takes forever under load.~~ The door was one unbounded 997 ms hash
  per arrival. It is now a measured 91 ms behind a permit per spare core, and
  the rate is printed at startup.
- ~~Everyone in a room sees every step of everyone else.~~ Interest replaced
  broadcast, and past forty people a place opens another copy.
- ~~A save rewrites everything about a character.~~ It now writes only the
  sections that changed — about thirty statements down to one.

**Not bugs**
- The client refusing a ROM whose SHA-1 doesn't match. Working as intended.
- A duel costing you nothing — no experience, no money, no black-out, and your
  copies arriving on their feet however the real party is doing. All deliberate:
  a fight that could cost an afternoon is a fight nobody agrees to twice.`,
  ],

  // ────────────────────────────────────────────────────────────── THE BACK ROOM
  'staff-chat': [
`Staff only.

**Standing policy**
- Rule 1 (ROM sharing) is ban-on-sight, first offence, no warning, including
  jokes and including "asking for a friend". Screenshot to #mod-log before you
  delete it.
- Everything else is warn → timeout → kick → ban, and the warning is a real
  conversation, not a template.
- AutoMod alerts land in #mod-log. Treat a block as a prompt to look, not as a
  decision already made — false positives on words like "data" adjacency happen.
- Field Tester is granted for demonstrated care, not for enthusiasm. Cartographer
  is granted for landed work.`,
  ],

  triage: [
`Where staff sort **#bug-reports** into real / duplicate / not-a-bug / needs-repro
before anyone spends an evening on it.

The bar for "confirmed": someone other than the reporter reproduced it from the
written steps, on a stated build.`,
  ],

  'mod-log': [
`Moderation and AutoMod actions. Wire your moderation bot's log output here too.`,
  ],
};

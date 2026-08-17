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
time. **2,841 tests**, none of which need a cartridge. An authoritative server, a
Gen III battle engine accurate down to truncation order, and a growing list of
things two people can do together: **see each other, chat, add friends, form a
guild, trade, duel, climb a ladder, buy and sell on a player market, and travel
the story as a company**. **Sound and animations are in** too — songs, cries, and
a move's animation read as the 48-opcode program it is.

Breeding, IVs, EVs, natures, abilities, weather and held items are all in. The
tier list is **recomputed from the cartridge's own base stats** rather than
curated by anybody — five bands at the quintiles, so a different image gives
different boundaries without a line changing.

It is also built to hold a crowd: an admission rate the server publishes at
startup, interest instead of broadcast, saves that write only what changed, and
**more than one copy of a busy place** — so forty people in a room never becomes
four hundred.

Repo: {{REPO}}`,

`**Where to go**

> **#rules** — read this first. One of them will get you banned instantly.
> **#plain-english** — what all this actually means, with the jargon taken out.
> **#announcements** — the only channel that will ever ping you unprompted.
> **#devlog** — what got built each day, and what it cost.
> **#milestones** — the long-form writeups. Start at the bottom.
> **#general** — say hello.
> **#setup-help** — stuck getting a build running.

**Roles you can pick up**

React or ask in #general for notification roles: **devlog pings**,
**build pings**, **playtest pings**. All three are opt-in and off by default.

**Field Tester** is granted, not requested — see #rules.`,
  ],

  'plain-english': [
`# In plain English

No jargon in this channel. If anything here needs a glossary, it is written
wrong — say so in #general and it gets rewritten.

**What is being built?**
A version of a Game Boy game you can play online with other people. You supply
your own copy of the game. The program reads the maps, the creatures, the music
and the rules straight out of your file, on your own computer, and draws them
itself. Nothing is copied, nothing is uploaded, nothing is sold.

**Why is it taking so long?**
Because nobody ever wrote down how that file is arranged. Every single thing —
where the maps are, what a move does, which song plays where — has to be found by
staring at the raw contents until the pattern shows up. Then it has to be
*proved*, because "it looks right" and "it is right" are not the same thing, and
this project has been fooled by the difference more times than is comfortable.

**How do you know it works?**
**2,841 automatic checks** run every time anything changes. If a change breaks
something that used to work, they go red and it does not ship. None of them need
a copy of the game.

**What is being worked on right now — message below.**`,

`## Right now: a robot that plays the game

It starts a brand new save and plays as far as it can get on its own. The point
is not to beat the game. It is to find every place a real player would get stuck
**automatically**, instead of waiting for somebody to walk into it and complain.

It currently gets to **381 of the game's 425 places.**

**What it has found, roughly in order of how silly they are:**

- **The player had no bag.** Every character in the game who asks "are you
  carrying one of these?" had been told no. For months. Silently.
- **It never bought anything in a shop.** Not because it was broke —
  shopkeepers stand behind a counter, and the robot would only talk to somebody
  standing directly beside it. It walked past every till in the game.
- **Then it turned out it had no money**, so it got refused at all of them. It
  gets handed a purse now.
- **A scene where somebody steps out of your way** was flinging people clean off
  the edge of the map.

**The honest part.** Fixing that last one made our headline number go *down* — we
thought the robot reached 390 places and it really reaches 381. The lower one is
the true one, and it stays.

**Next:** get the walk further, then work out what is behind the last 44 doors
that nothing in the game appears to open.`,
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

**Trade and duel are deliberately the same shape** — one at a time, an invitation
that dies when either side walks away, and asking somebody who has already asked
you is how it begins. Two verbs that behave the same way are two verbs a player
only has to learn once. Guilds, the market and co-op all reuse that shape.

**The scaling run (111–124)** rebuilt most of this underneath the game: a door
with a measured width, interest instead of broadcast, an index instead of a scan,
an outbound queue per connection, a sight circle, saves moved off the input path
and then reduced to only what changed, and **instancing** — past forty in a copy,
the next arrival opens another.

Instancing is a design decision, not an optimisation. Capping who gets drawn or
shrinking the sight circle both leave a crowd standing there that the player
cannot see and can walk into. **A copy has no crowd in it that anybody is being
lied to about.**

**More on the shared shape in the message below.**`,

`**One shape, reused.** The market, guilds, friends and the ladder all split the
same way: the world reports, and something outside the lock writes it down. A
database transaction is not something to hold the world's lock for, because that
lock is what every other player is waiting on.

Guilds and the market also share a front-end pattern — a request is turned into
the console line it is equivalent to and run through the path the console runs,
so there is **one implementation and two front ends**. Two implementations of
"one guild each" is how somebody ends up in two guilds.

Open questions worth arguing about, any time:
- **A thousand players on hardware that could hold them.** Every number in the
  scaling notes is two cores, with the load generator sharing them with the
  server it measures. The only open question code cannot answer.
- A flag set in memory that the save does not have.
- Simulating maps nobody is standing on (walk away and back, and the street
  resets).`,

`**Co-op: travelling as a company.** Invite somebody and you land in the same
copy of every map, and doors move you together instead of quietly splitting you
up.

The interesting part is what your friend can *see*. Join three gyms behind
somebody and, while you are travelling together, the world shows you your own
progress **or** anything the people with you have opened — so you can follow them
through a door you have not earned yet.

**Nothing is written to their save.** They are borrowing a world, not being
handed one. When the company breaks up, everybody keeps exactly what they played
for, and there is nothing to undo because nothing was ever done to them.

Same rule as everywhere else in here: the world reports, and something outside
the lock writes it down. Borrowing is a question asked at the moment somebody
opens a door, not a copy of anybody's save sitting somewhere going stale.

Still open: a parcel handed to the company still goes to one person.`,
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

**Recently modelled:** PP, held items, duels — and **abilities**, the first
system here where half of it simply is not in the cartridge. The names and which
species has which are read out of the image. What any of them *does* is ARM code,
the same boundary the \`special\` routines sit behind, so every rule is modelled
and the file says so once rather than seventy-eight times.

The count is printed rather than rounded to "abilities: yes" — some are modelled,
the rest **carried, named, shown, and silent**, which is a different state from
"not supported". A test fails if anything is listed as modelled without a rule
behind it, *or* has a rule and isn't listed.

**More on abilities in the message below.**`,

`Since abilities landed: **weather**, the **contact flag** that sat unread on
every move record, the abilities that refuse to be made worse at something, and
the consumed half of held items — every berry, both herbs. **34 of 66 held-item
effects** and **44 of 76 fielded abilities** do something; the rest are carried
and silent, and both counts are printed at export rather than rounded up to
"yes".

**GUTS is worth reading twice.** A burn does not halve its Attack — halving the
Attack of the ability whose whole point is that being ill helps would leave it
doing three quarters of what an unburned one does, which is the opposite of the
rule.

Where each half lives is deliberate: **the names stay with the client**, which
owns the cartridge, and **the effects live on the server**, which owns the rules.
The server is told a number and never learns what it is called — the same
arrangement every other name in this project has.

And which of its two abilities a creature has is a **slot**, rolled once and
stored, exactly as its sex is. A creature asked twice would be immune to a move
on one turn and not the next. The slot rather than the resolved ability, because
the slot is what the dice decided and the ability is a lookup — keeping both
would be two copies of one fact, and the second copy is the one that goes stale.

**The silent half is finished — the lesson is in the message below.**`,

`**Every effect family is now modelled**, including the ones that needed real
machinery of their own: SUBSTITUTE, BIDE, FUTURE SIGHT.

The lesson from the last batch is worth more than the count: **fourteen of the
final twenty-three groups needed no new machinery at all.** They needed a line
pointing at something the engine already had.

The out-of-turn family — COUNTER, MIRROR COAT, REVENGE, FOCUS PUNCH — sat on the
roadmap as "needs to act out of turn". The ordering had been there since moves
were first read: priority is a signed byte on the move's own record. All that was
missing was two fields of memory.

**A family named for the machinery it appears to need is usually named wrong.**
The naming comes from how a move *feels* to a player, and how a move feels and
what it costs to implement are unrelated.`,

`**Switching creatures mid-duel is in.** It was refused for a long time and said
so in the code rather than quietly pretending otherwise — and it was the one
missing piece under the last ordering bug still on the list, the moves that are
meant to act on somebody running away. Both are closed.`,
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

The ability bytes and the move records' contact flag have since been read, which
is two more off that list. Still unread: \`SafariZoneFleeRate\`, \`EggCycles\`,
\`BaseFriendship\`, \`BodyColor\`.

**Sound is read the same way as everything else** — a sixteen-byte sample header
found by shape, then instruments pointing at confirmed samples, voicegroups at
confirmed instruments, songs at confirmed voicegroups. Each layer proved by the
one below, so none of it needs to know where anything sits on a given cartridge.

**Two traps worth knowing about if you go looking.** Ability names are found by
anchoring on \`STENCH\`, the same way the move table anchors on \`POUND\` — one
English word to find an address, and then none, so what comes out is whatever the
cartridge says in whatever language it says it.

And the exporter's anonymise step is a hand-written allowlist. A field left off it
is a field the server never hears about however well it was extracted. That has
now bitten twice: first the EV yields, then the ability bytes, both extracted and
stored and arriving as nought.

**Rule 1 is enforced by a test now — details in the message below.**`,

`**One line this project won't cross.** The standard way to find the song table
is to scan for the sound driver's function by its prologue and read the address
out of the instruction stream. Every existing tool does it, and it works. It is
also reading compiled code. The corroboration used here is taken from the data
side and labelled as weaker, rather than pretending a disassembler is a file
reader.

**The repo asks git whether anybody committed a cartridge.**

Every *tracked* file is checked three ways: the extensions of cartridge images
and saves, the names of the exporter's own outputs and the account database, and
— the one that matters — **the bytes at offset four**, the logo every cartridge of
this family carries.

An extension list is a rule about filenames, and the failure it misses is the one
that would actually happen: an image renamed to something harmless.

**Tracked, not present.** What's in your working tree is your own business, and a
cartridge sitting beside the checkout is exactly where a cartridge is supposed to
be.

Before this, "the client ships no cartridge data" was held up by \`.gitignore\`
alone — and that file's own comment says what that was worth: the exporter's
outputs "were only ever kept out of the repository by nobody having put one in
the root, which is not the same as a rule."

It was checked by putting a cartridge header in a file called \`assets-bundle.bin\`
and watching it fail with the reason. **A guardrail nobody has seen fail is a
guardrail nobody has tested.**

Standing unsolved problems:
- **The font.** Four mechanical methods ruled out. The mapping is not identity,
  the sheet is not one of the four candidates, and the geometry may not be 8×8.
- **The box count.** Stated nowhere on the cartridge. Eight is a decision, not a
  reading, and it is labelled as one.

**Reading the file rather than the world — message below.**`,

`**Reading the file, not the world.** Every search here used to start at a map
and follow the doors out of it. That can never answer *is there anything the maps
do not point at* — and the output looks identical to a search that went
everywhere and found nothing. Three separate times this month, "nothing in the
game does X" turned out to mean "nothing I opened does X".

There is a sweep now that reads all sixteen megabytes for the bytes that move a
story flag, asks of every hit whether the map search had ever decoded it, and
climbs until it reaches something with a name. It is run a second time on the
same file **backwards**, as a control — whatever it finds there is what it finds
when there is nothing to find. 600 hits against 787 is noise. 7 against 0 is not.

It also prints how many scripts it opened, and of what kind. A kind with no line
is a kind nothing looked at, which is the single thing the old output could never
tell you.

**Where that leaves the reading:** 2,915 scripts on 425 maps, 3,856 blocks read
to a proper end and 32 that still stop on a byte nobody has identified. **322
flags gate something in the world**; the widest walk opens 212 of them and leaves
110 shut. Of those 110, 48 would open if the walk got further, and 62 would not —
44 have no opener anywhere in the file at all.

That last group is the boundary: the cartridge opens them from its own compiled
code, which this project does not read and does not intend to. A boundary with an
address on it, rather than a mystery.

**The thing worth carrying:** a long run of faults closed here recently and
**every one of them was in this project, not on the cartridge.** Nothing fails
when it is wrong; it comes back confident and slightly off.

**A note on measuring — message below.**`,

`**How you tell a finding from noise here.** Search sixteen megabytes for a
pattern and you will always find some. The test is to run the same search on the
same file **backwards** and compare: whatever turns up there is what the search
finds when there is nothing to find.

That control was wrong for a while, and the way it was wrong is worth knowing.
**Reversing a file keeps things clumped together.** Nine hits scattered across
the whole image and nine hits crammed into one kilobyte printed exactly the same
sentence — so both sides of the comparison were counting clumps as though they
were evidence. Once that was fixed, one previously published result changed which
side of the line it fell on.

The rule that came out of it: **a count with no denominator cannot come back
empty.** "Nothing does this" and "nothing looked" read identically until both
halves are printed. Anything quoted here now carries the floor it beat.`,
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

  // ──────────────────────────────────────────────────────── JOIN THE PROJECT
  'open-roles': [
`# What we need

Everything here is **unpaid, and always will be.** Nothing in {{PROJECT}} is ever
sold, so there is no money to share out. What there is: your name on the work,
a role in here, and credit in the writeups.

**Code — C#**
The biggest need. Server, client, or the shared engine underneath both. Open
right now: a screen for the ladder, a proper way for a player to join a friend's
copy of a map, and switching creatures mid-duel.

**Digging through the game file**
The lettering has beaten four attempts. A few record fields are still unread.
Good if you like a locked door.

**Testing**
Writing tests, or playing builds and filing bugs somebody else can follow. Not a
junior job here — most of our worst bugs were things the tests could not catch.

**Art**
Cosmetics are drawn from scratch, never taken from the game, so there is no legal
risk in this one at all. Twelve slots to fill, and a placeholder rectangle in the
wardrobe mirror that badly wants replacing.

**Writing and moderation**
Around 160 devlogs that could use an editor, and rules that need enforcing.

**A spare computer**
Genuinely. Every speed measurement we have is from two cores, so nobody has ever
run a thousand players. One afternoon on a decent machine closes the last open
question on the list.

---

**To apply:** post one line in **#apply**. There is no bar to clear — "I know some
C# and want to get better" is a real application. Read **#contributing** first if
you write code.`,
  ],

  apply: [
`**Post one line here** naming what you'd like to work on. That's all — for
example: *"Applying: client/UI, some raylib experience"*.

A staff member will open a **private thread** with you from that message. Skill
level, timezone, age, links, anything else — all of that goes in the thread,
where only you and staff can see it. Don't post personal details in this channel.

**In the thread we'll ask:**

\`\`\`
Which role(s):
What you've built before:   (links if you have them, none needed)
Roughly how much time:
Timezone:
Anything you want to learn:
\`\`\`

**There is no bar to clear.** "I know some C# and I want to get better" is a
real application and always has been. This project is one person; the useful
question is whether you'll enjoy it, not whether you're senior enough.

**What happens next:** we find you something small and real to do. Not a test
task invented to grade you — an actual open item off **#open-roles**. If it goes
well you get **Cartographer** and commit credit. If it doesn't, no harm done and
no explanation owed.

Slowest part is usually me. Nudge in #general if it's been a week.`,
  ],

  contributing: [
`How work actually lands. Read before your first pull request.

**One rule above all others: never commit cartridge data.** Not sprites, not
maps, not text dumps, not the exporter's outputs. This is checked — every
*tracked* file is tested for cartridge and save extensions, the exporter's own
output names, and **the bytes at offset four**, the logo every cartridge of this
family carries. A file renamed to something harmless still fails.

That test is not there because anybody is suspected. It is there because the
project's entire legal footing is that it distributes nothing, and a guardrail
that depends on everyone remembering is not a guardrail.

**Tests are the review.** New behaviour arrives with tests. The bar isn't
coverage for its own sake — it's that a rule enforced on one side of the
client/server split needs its counterpart checked on the other. That specific
mistake has been made three times here.

**Numbers before opinions.** If a change is about speed, measure it first and
put the number in the pull request. The scaling work found the wall was the
login door when everybody assumed it was the database.

**Say what you got wrong.** The milestone writeups include the withdrawn
findings and the roadmaps written from an instrument pointed at the wrong thing.
That's the house style. A correction is worth more than a clean story.

**Small first.** One contained change beats a large one — not as a test of you,
but because reviewing is the scarce resource in a one-person project.

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
- **The story does not finish yet.** A tool plays the game from a fresh save and
  walks as far as it can get. It reaches **381 of 425 places** and opens **212 of
  the 322 switches** that gate something. Of the 110 it never opens, 48 would come
  with a longer walk — and **44 have no opener anywhere in the file at all**,
  because the cartridge flips those from its own compiled code, which this
  project reads none of.
- **32 pieces of script still stop early**, on bytes whose meaning is unknown.
  Several turned out to be a wrong guess made earlier in the file rather than a
  command at all, which is the usual shape of this.
- **11 places have no way in at all**, five of them islands with no dock in what
  gets read.
- **Money is modelled and the payout table has never been located.** Nothing in
  the walk earns anything, so what a fight pays out is a decision, not a reading.
- **Sound is paused.** 31 song headers unconfirmed, and battle music still open.
- **A flag set in memory that the save does not have**, and the older, vaguer
  **flag race** that may be the same animal: a script's flags reach the server
  from the client, so two conversations inside one round trip both see the old
  state.
- **A thousand players on real hardware.** Every scaling number so far is from
  two cores with the load generator sharing them. The only open question code
  cannot answer.
**More below.**`,

`**Known and open, continued**
- **LOW KICK**, which wants species weight — on the dex table rather than the
  base-stat record, so it needs a locator of its own.
- **Ten held-item effects**, every one about something outside a fight, and
  **thirty-two abilities**, which now have the hooks they were waiting for.
- **Four more regions.** The largest item by far and the least like the others:
  extraction work against cartridges this project does not have.
- **Text rendering.** The cartridge's font has not been located. Four mechanical
  methods are ruled out; the mapping is not identity, and the geometry may not
  even be 8×8.
- **A ladder screen.** \`/ladder\`, \`/rating\` and \`/tier\` read it from the console;
  there is no picture yet.
- **Whether a duel should be refused across bands at all.** The ladder records
  which band a fight counted in rather than restricting who may fight whom —
  measure first, then decide whether to forbid.
- **Unsimulated maps.** Only maps with a player on them tick. Walk away and back
  and the townsfolk have reset to their starting positions.
- **In co-op, a parcel handed to the company still goes to one person.**
- **One thing stated but not proved:** that a duel's result is taken exactly
  once. Breaking it leaves every test green, because reaching it needs a real
  duel driven to a finish through the world. Said out loud rather than assumed.

**Closed items and non-bugs are in the messages below.**`,

`**Closed, so please stop reporting them**
- ~~The walker never buys anything.~~ It was not broke. Shopkeepers stand behind
  a counter, and it would only talk to somebody it could stand right beside — so
  in the whole game it never once stood next to a shop clerk. It talks across a
  counter now, and every shop in the game opened up.
- ~~People flung off the edge of the map by a scripted scene.~~ The scene was
  applying a whole walk as one leap instead of a step at a time. Fixed — and the
  reach it had been inflating came **down**, 390 places to 381. The lower number
  is the true one and it is not being rounded back up.
- ~~The player had no bag.~~ Every character in the game who asks whether you are
  carrying something had been told "no" since the walker was written. Silently,
  for months. There is a bag now.
- ~~Switching creatures in a duel is refused.~~ It is in, and it was the missing
  piece under the last ordering bug on this list.
- ~~The walk cannot cross water.~~ It can. Which move crosses water is read out
  of the file twice over rather than handed in as a setting.
- ~~The shop sells nothing.~~ A carrying limit named for one pocket was being
  counted across the whole bag, so every purchase past sixty items was refused
  without saying so. A refused purchase now says which of four reasons it was.
- ~~Nobody ever answered a yes-or-no.~~ Every run had stopped dead at the first
  "would you like to…?" since the walker was written — and a script that stopped
  mid-sentence looks exactly like somebody with nothing more to say.
- ~~The party never gains a level.~~ It does. Three at level 75 with the sea open.
**Still closed, continued below.**`,

`**Closed, continued**
- ~~SAFFRON's shut doors.~~ Ten measurements went into one door before the counts
  sat side by side and showed it to be one ordinary member of a set of 245.
  Closed in the reading and in the walk.
- ~~The market's window between committing and the in-memory copy.~~ It could put
  a creature in your box **and** on the market at once, both halves internally
  consistent and nothing throwing, until two people owned it.
- ~~The battle engine's silent half.~~ Every remaining effect family is modelled.

**More closed items, and the things that are not bugs, below.**`,

`**Closed, continued**
- ~~The bedroom PC's behaviour byte.~~ There is no byte. The bedroom machine is
  scripted, not a tile — proved, not assumed.
- ~~Joining takes forever under load.~~ The door was one unbounded 997 ms hash
  per arrival. It is now a measured 91 ms behind a permit per spare core, and
  the rate is printed at startup.
- ~~Everyone in a room sees every step of everyone else.~~ Interest replaced
  broadcast, and past forty people a place opens another copy.
- ~~A save rewrites everything about a character.~~ It now writes only the
  sections that changed — about thirty statements down to one.
- ~~There is nowhere to buy cosmetics.~~ There is a counter now, and money is
  what turned a wardrobe into a choice.
- ~~The species table's ability bytes are read by nothing.~~ Read, and 44 of 76
  fielded abilities now do something. The rest are carried and silent, and the
  count is printed rather than rounded up.
- ~~One box holds everything.~~ Eight now. Nothing on the cartridge says how
  many there should be, so the number is modelled and the reason sits beside it.
- ~~Nineteen warps leading to maps that are not exported.~~ Measured. It is 11
  places with no way in, and they are on the open list above with reasons.

**Not bugs**
- The client refusing a ROM whose SHA-1 doesn't match. Working as intended.
- A duel costing you nothing — no experience, no money, no black-out, and your
  copies arriving on their feet however the real party is doing. All deliberate:
  a fight that could cost an afternoon is a fight nobody agrees to twice.
- Your friend seeing a door you opened while you travel together. That is what
  co-op **is**. Nothing is written to their save, and they leave with exactly
  what they earned.`,
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

> **This file is the prompt.** It lives in the repo at `claude/next-session-prompt.md` and in the
> attached Claude Project at the same path, and the two are written together. You do not have to
> paste it — opening a session with *"read `claude/next-session-prompt.md` from the project and
> carry on"* is enough, and it cannot go stale against the repo copy the way a paste can.

I'm building MonMMO, a from-scratch MMO whose data is extracted from my own Pokémon FireRed
cartridge. C# / .NET 8, xUnit, Raylib-cs client, SQLite server. Repo is at
`~/OneDrive/Desktop/pokemmo`, branch `main`, everything merged. Base is the tip of
`claude-283`, 3057 tests green.

Standing rules — do not break these:

* Never commit cartridge images or anything derived from them. Every player supplies their own
  file; extraction happens locally. This is the project's most important rule. Do not ask me to
  upload `firered.gba` to your container.
* Don't ship anything to speed things up. Users keep needing their own ROM.
* Every number is marked read (off the cartridge) or modelled (a decision), never conflated.
* Find things by what they look like, print what was found, hardcode nothing.
* Every new guardrail gets proven by deliberately breaking the code it guards — use
  `tools/break-guard.sh`, which refuses on a dirty tree. If breaking a rule fails no test, that
  guard needs a decoy fixture or needs removing.
* **The cartridge may be staged into the session.** Two sessions running it has been, with
  permission, over the desktop bridge from `~/OneDrive/Desktop/pokemmo/firered.gba`. It stayed
  out of the repository, out of every commit and out of the bundle. It turns one measurement
  per round trip into a dozen in one turn and it is how the actual answers get found — by
  disassembling forty bytes by hand. Ask first; do not assume.
* You have no credentials and can't push. Deliver work as git bundles, rehearsed from a clean
  clone at the base. **Bundles sent to chat do not reach my disk** — write them into
  `~/OneDrive/Desktop/pokemmo/` with the device bridge as well, or they are not delivered.
  Write BOTH names, same bytes: `claude-<n>.bundle` for the archive and **`incoming.bundle`**,
  so my side never depends on picking the newest file.
  **The handover on my side is one command: `bash tools/push.sh`.** It clears the `index.lock`
  OneDrive leaves behind, fetches the bundle into `from-claude`, **fast-forwards** main onto it
  and pushes to `github.com/Koopz8/monmmo`. So build every bundle on the tip of my main and
  keep it linear — if it would not fast-forward, the script stops and asks for a rebuilt one,
  which is the right answer. Don't hand me `git merge` lines; say "run `bash tools/push.sh`".
  Don't run `git` through the bridge inside that folder — it leaves a stale `index.lock`.
  `tools/push.sh` is the tracked one and prefers `incoming.bundle` by name; the untracked copy
  that used to sit in the repo root was moved to `_to_delete/` at 233 because two copies of the
  handover script is one too many.

## Getting the session running

Four steps, and none of them is thinking. Ask before staging the cartridge; everything else is
mechanical.

1. `device_request_folder_access` on `~/OneDrive/Desktop/pokemmo`.
2. `device_bash`:
   `rm -rf /tmp/repo.git /tmp/repo.tar.gz && git clone --no-hardlinks --bare "$HOME/mnt/pokemmo" /tmp/repo.git && tar czf /tmp/repo.tar.gz -C /tmp repo.git && cp /tmp/repo.tar.gz "$HOME/mnt/pokemmo/_transfer.tar.gz"`
   — a local clone READS the working copy and writes nothing to it, which is why it is safe
   where running `git` in that folder is not.
3. `device_stage_files` on `_transfer.tar.gz` and, **with permission**, `firered.gba`.
4. In the container:
   ```
   mkdir -p ~/work && tar xzf /mnt/user-data/uploads/pokemmo/_transfer.tar.gz -C ~/work
   git -c safe.directory='*' clone -q ~/work/repo.git ~/pokemmo
   bash ~/pokemmo/tools/session-setup.sh
   ```
   That installs the .NET 8 SDK, puts the cartridge in place, drops the transfer remote (there
   is no remote to push to and leaving one makes every hook ask), sets the git identity and
   builds RomDump. It is idempotent — run it again after a resume.

`_transfer.tar.gz` is scratch and lives in that folder because the bridge can only stage from
inside it. Overwrite it rather than adding another.

## The method that works here

When two rounds of reasoning haven't converged, stop inferring and print the bytes.

Milestone 174 took thirteen measurements. **Seven killed a prediction, and every one of those
seven was an instrument written that same turn.** 190 was the same shape again: two of its own
readings disagreed about where a `setvar` lived, and forty bytes of hexdump settled it in one
turn. Knowing about the pattern in advance has never helped once. What helps is building every
instrument able to come back empty, and believing it when it does.

Traps worth carrying:

1. **The answer is often in a part of the file the scan does not open**, and the output is
   byte-identical to a scan that looked and found nothing. **Before believing any "nothing in
   the world does X", check what the scan is enumerating.** `--in-the-image` exists for this.
2. **When two of your own readings disagree, the stricter one is not automatically right.**
   Ask which reading follows fewer edges before deciding which is more rigorous. In 190
   `--trace` printed `0x081655ED` and `--who-writes` printed `0x0816569A` for the same write,
   and neither was wrong: **`--trace`'s address column is the script that ran, not the site of
   the write.** Two hexdumps and a goto chain, not an argument.
3. **A count is not a ranking.** Rank by the thing you actually care about.
4. **A break that comes back green is a claim about the break as well as about the guard.**
   175 had one: `Array.Sort(bytes, (a, b) => 0)` looks like a no-op and isn't — introsort is
   unstable. Re-broken properly, the guard caught it.
5. **A fallback that names a cause is worse than one that says nothing.** "It ran to the end,
   so the setflag is on an ordinary branch it had no reason to take" was the `else` of a
   three-case switch. There was no branch; the run had lost to GIOVANNI. Two sessions.
6. **A filter that keeps output readable must never decide which question gets asked.** 175's
   climb skipped sites the map scan had opened "because --flags already answers those". The one
   site that mattered was opened, and --flags had not answered it.
7. **A misalignment INVENTS things as readily as it hides them, and the two are indistinguishable
   from outside.** Fixing `[0x6F]` took flags moved from 259 to 258 and the playthrough's own
   count from 286 to 284 — *down*. **Do not assume a fix will make a number go up.** A number
   moving the wrong way is not a regression until you have read why.
8. **A number printed with no denominator cannot come back empty.** "Nothing was handed over
   twice" and "nothing hands anything over" read identically until 190 printed both halves.
   195 is the same trap a third time: "5051 calls to 28 routines" was the fixpoint's own passes,
   and the number about the cartridge is 319 places.
9. **Before believing "X is wrong everywhere", print how many places ask X.** 196 fixed a key
   that had been wrong for nineteen milestones and it moved nothing at any lever setting: the
   only consumer in the repository is asked about ONE setter at three settings and NONE at the
   other three. The fault was real and the blast radius was nought, and only a denominator on
   the CONSUMER could say so. A count of how wrong something is is not a count of who cares.
10. **The widest agreement is usually the WRONG width — three times running now.** 200's `0x92` resumes on `0x00` at ALL
    NINE sites read two, three or four wide — and that is one agreement, not nine: every site is
    landing in the same run of zero bytes inside the same argument. `0x95` at 202 did it across
    THREE wrong widths at seven sites each; `0x43` at 203 did it with `0x0D` five times and then
    `0x80` five times — the two halves of `0x800D` read as opcodes. Each time the right answer
    was the width whose sites DISAGREED. In this cartridge's script stream a column of identical
    resume-bytes is evidence of a misalignment, not of a boundary. Count what the sites agree
    ON, not how many agree.
11. **A shape that matters somewhere does not matter everywhere.** 193 found that a scene played
   once per door wrecked the walking, because a walk ACCUMULATES. 194 predicted the same for
   every count the run keeps. Measured, it is six in five thousand — a counter accumulates
   nothing. The prediction was mine and reasonable and wrong, and only measuring said so.
12. **A number that is only ever copied is never wrong out loud.** The floor table above was
    stale in five of its six rows for thirteen milestones and nothing anybody wrote about it was
    false: every *difference* it is quoted for — `--surf` costs two, `--in-order` adds two and
    one and a party member — was still exactly right, because each milestone re-ran the pair it
    cared about and pasted the delta onto a base nobody re-ran. (`--surf` costs ONE since 239,
    printed by the command; the sentence was true for twenty-two milestones and is not now.) **A table maintained by deltas
    drifts and stays self-consistent.** The only thing that catches it is running the whole
    block, which is why the prompt says to start with `--play` — and 207 is the first session
    that read the output against the table instead of past it.

13. **A number that cannot depend on the lever is the one that catches a wrong label** (211).
    A three-bucket sort of the run's shut gates put 134 in "nothing in the file sets it" at the
    floor and 56 with the levers on. Whether anything in the file sets a flag is a property of
    the FILE; it cannot move with a lever, so the label was wrong — the run sets sixty-five
    flags no `setflag` names, because picking a thing up sets its hide flag in compiled code.
    **When a classification has a bucket that is about the cartridge rather than about the run,
    print it at two lever settings and check it does not move.** The fixed version reads 44 at
    all three.

14. **A block nobody re-runs does not need a delta to be wrong** (230). Trap 12 was about a
    table maintained by deltas drifting while staying self-consistent. The block called *Where
    the reading stands*, and items 8 and 9 of the task list, are simpler than that: they entered
    this file in ONE commit at milestone 190 and were copied forward thirty-nine times without
    anybody re-running the instrument that produced them. Three of the eight lines checked were
    wrong, one of them wrong in the same commit message that announced the change. **When a
    number in this prompt matters to what you are about to do, run its instrument first.** The
    milestone that discovers the true number is not the act that corrects the block — 228 wrote
    "its 264 was right" in its own document and left `258` standing here.

15. **A bucket is not an operation** (236). 235 reported one routine as the exception to
    all-or-nothing: `0x194`, waited at 1 of its 34 places. It is not one operation — 31 of those
    places set `0x8004` first, to eighteen different values. Keyed on what is actually being asked
    there is no exception in the file: **0 of 95 multi-place askings are mixed, against 26.6 by
    chance**. And the null moved with the question: 235 asked how many groups would be all-waited
    (0.21, a null dominated by groups that wait for nothing) where the thing observed is that none
    is MIXED (26.6). **Before reporting an item as an exception, check the bucket is the thing the
    rule is about — and check the null is about the outcome you actually saw.**

16. **A number nothing computes cannot even be wrong** (231). Trap 8 says a number with no
    denominator cannot come back empty. This is one turn further on: `936`, `45`, `62`, `240`,
    `146` and `158` were quoted in this file and **no instrument in the repository printed any of
    them**. They read like measurements, they were quoted like measurements, and nothing could
    have contradicted them. `936` turned out to be right after six milestones of being
    uncheckable, which is the least satisfying way for an audit to end and the only honest one.
    **Before quoting a number, know which command prints it.** If none does, that is the finding.

17. **A test that is right for a one-way step is silently wrong for any other** (239). The
    playthrough decided it had finished by comparing a pass with the one before it, and that
    finds a fixed point and nothing else. It was correct for as long as everything the run did
    was one-way: flags got set, things got picked up, and a pass that changed nothing had
    nothing left to change. Running the signs put the first thing in that can take something
    BACK — `9.6`'s fifteen doors share a block that sets and CLEARS `0x0001` — and the test
    never fired again: every `--say-yes` row ran to the twenty-four-pass backstop reporting that
    something never settles. **When you add a way for the run to undo something, the settle test
    is the first thing that broke.** The fix keeps every state it has been in, and reports a
    cycle as a THIRD answer rather than folding it into "nothing more opened", because a run
    that settles and a run that oscillates are different facts. And the state is the CONTENTS of
    the sets, not their sizes: a pass that clears one flag and sets another has the same count
    and is a different state, and getting that wrong stops a run with somewhere left to go.

18. **Writing a rule down is not applying it** (240). 239 put that last sentence in
    `WhereItHasBeen`'s documentation and left the settle test THREE LINES ABOVE IT comparing six
    counts — how many flags, how many moves, how big the party — so a pass that cleared one flag
    and set another matched all six and stopped the run. The rule and its violation were in the
    same screen of the same file, added in the same commit. **When you write down why a
    comparison has to be made a particular way, grep for the other comparisons of the same thing
    in that file before you commit.** There were two, and they now share one definition.

19. **A control the reader cannot re-run is not a control** (241). 239 measured what putting
    signs into the walk was worth by running the playthrough twice, one commit apart, and
    writing the two tables side by side. Every number in it was right — 241's control
    reproduces 183/153, 243/231 and 381/294 exactly — and nobody without that commit built
    could have found out. **A before-and-after across two builds is a measurement with no
    instrument.** The fix was a parameter and one extra run inside the same command, which is
    the same shape as the reversed-image floor this project measures every reading against.

20. **A break that fails LESS than it should is the same signal as one that passes** (242). The
    rule "a sign is read from its own square or any of the four around it" was broken to ask
    only its own square, and one test went red where two should have. A sign's own square is
    SOLID — that is what a sign is — so the wrong rule reads every sign in the game as one
    nothing could stand beside, and the only fixture that noticed did so by accident. **Count
    what a break kills against what it should have killed.** A fixture was added, then the same
    break re-run.

21. **The same trap can be sprung by the milestone that quotes it** (242). 241's own document
    cites 224 — five copies of "every script on a map", counted by the wrong key — one line
    below a count it had made by the wrong key. `215` was addresses-per-map reported as signs;
    the answer is `317`. **When you write a sentence about what a number is keyed on, go and
    look at the key.**

22. **A bare number is not an identity — the COMMAND is** (243). `--trace 0x003F` said "nothing
    the run executed touched it" about a flag a script had cleared on that same run, because
    `--trace` watches a VARIABLE and 0x003F is both. 27 numbers in the map scan are named both
    ways against a floor of 1.71, so this is not one odd case. Every reading in this repository
    decides by the command and is safe; every ARGUMENT on the command line is a bare number and
    is not. **Before believing an instrument's silence about a number, check which namespace it
    was asking about.**

## Where things are

Read `claude/milestone-243-one-number-two-namespaces.md` first, then `242`, `241`, `240`, `239`,
`238`, `237`,
`236`, `235`, `234`, `233`, `232`, `231`, `230`, `229`, `228`, `227`, `226`, `225`, `224`, `223`, `222`, `221`, `220`, `219`, `218`, `217`, `216`, `215`, `214`, `213`, `212`, `211`, `210`, `209`, `208`, `207`, `206`, `205`, `204`, `203`, `202`, `201`, `200`, `199`, `198`, `197`, `196`, `195`, `194`, `193`, `192`, `191`, `190`, `189`,
`188`, `187`, `186`, `185`, `184`, `183`, `182`, `181`, `180`, `179`, `178`, `177`, `176`.
**Twenty-three faults closed and every one was in this project, not on the cartridge.** A walk that
stopped at a conditional call; one byte with no width; three scans that rolled their own "every
script" list; a list ranked by a count instead of by what it costs; a party that could not gain
a level; a roadmap line that called a fix a cost; a continuation that carried flags and not
variables; a trainer marked fought before the fight; a reader that was never told who had been
beaten; a walker never told it could swim; two argument widths that were wrong rather than
missing (`0x1F`, `0x6F`); a map's arrival script running after every person on that map; and
**a beaten trainer resuming inside the fight's own script instead of the bytes after it**,
which skipped a `checkflag` at all eight gyms; and the floor table at the top of this file,
stale in five of six rows for thirteen milestones while every sentence written about it stayed
true (207); and at 239 **the exported map record carrying no signs at all**, so the walk went
over a world with 519 sign scripts it could not see — 224's fault standing in the other half of
the project, and the settle test that broke the moment they went in; and at 240 **that settle
test itself, made of six counts** — so a pass that cleared one flag and set another matched all
six and stopped the run, three lines below the documentation saying why it must not.

`175-reading-the-file-not-the-world` is the instrument set (`--in-the-image`, `--climb`, the
reversal control). `184` adds `--who-writes`. `187`/`188` are the two wrong widths and
`--stops`. `189` is `--trace` and the ordering. `190` is `--fights` and the handover count. `191` is
`--who-knows` and the sea. `192` is the walk. `193` is the one that
retired both of 192's proposed designs by reading the bytes instead, and `194` is `--entries`
and the fault 193 shipped. `195` is places against times, and a prediction of 193's that turned
out to be wrong. `173-reading-the-other-arm` still has the best table of wrong turns.

**The pattern, thirteen times over: right at every step and quietly wrong at the end.** Nothing
in this project fails when it is wrong. Assume the number in front of you is distorted until an
instrument says which direction — and note that 190 moved the map count by **zero** at every
lever setting while moving flags at all six. A fix that changes no headline is not evidence it
was not a fix.

## The instruments

```
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --say-yes --boat --surf
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --flags
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --scripts
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --fights
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-knows
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --coins
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --entries
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --counters
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --in-the-image 0x003E,0x003F
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-writes 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-reads 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --through-a-call
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --say-yes --in-order --trace 0x4055
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --stops 0xC0
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --script-map 6.2
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --routines
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --standard
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --the-scan
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --two-commands
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --arrivals
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --the-floor
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --read-from 0x081BE06F
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --field-effects
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --slots 0x9D,0x7F,0x82
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --signs
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --play --moved 0x003F
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --namespaces
```

`--play --signs` is the fourth list with **its own control in the same process**: which sign
scripts ran, at how many addresses, on how many maps, **why each of the rest did not** (three
buckets, and the first is about the FILE and must not move with a lever), then THE SAME RUN WITH
SIGNS SWITCHED OFF and the two subtracted. It reproduces 239's before-numbers off one build — 183/153, 243/231,
381/294 — which is what a control is for. Signs are worth **0 maps at every lever setting** and
7 / 3 / 2 flags. Keyed by (map, address): one block read in two towns is two signs and one
address, which is 224 in the run rather than in the scan.

**`--trace N` watches a VARIABLE, not a flag — and `--moved N` is the flag half.** They share the
number space and the cartridge really does use 27 numbers both ways (`--namespaces`, floor 1.71),
so each command now says when the number it was handed is used in the other one. `--moved` prints
every set and clear with its script, its map, its pass and which of the four lists ran it.
`--namespaces` asks the map scan — 238 flags, 236 variables, 27 shared — and prints the
whole-image version beside it (2117 / 12659 / 1182) as the noise it is.

** They share the number space, so `--trace 0x003F`
answers — "nothing the run executed touched it" — about something else entirely. What moved a
FLAG during a run is printed by `--play` itself since 240: every set and clear with its map, its
script and its pass, and the ones that move BOTH ways with the ones that do it inside one pass
called out separately, because those are what make a run go round.

`--slots N[,N]` asks one question of any command that takes a byte and a word: **is the byte an
index?** Runs of it counted in byte positions, whether every run counts 0,1,2 from nought, and a
floor drawn from the values that byte actually takes. It comes back **unanswerable** when the
byte has one value — `0x7F` is 0 at all three of its places and would otherwise read as a yes at
a floor of one in one. `0x9D` is one in 3^9; `0x82`'s byte is 1 at all seven.

`--field-effects` pairs every block that asks who knows a move with the number its `dofieldeffect`
takes: 7 blocks, 6 moves, 6 numbers, no move with two — and it says out loud that the only direct
evidence is the ONE repeated move repeating its number. It also prints the four numbers no move
drives and the one-in-210 floor on them all being above all six, and the raw whole-image sweep
beside its reversal, which is ahead.

`--read-from 0xADDR[,0xADDR]` prints an address: the bytes and what they read as **off the same
command**, every block it reaches, and which byte stopped a read and where. This project's
method section says to stop inferring and print the bytes and there was no command that printed
them — 190, 199, 228 and 232 all hand-dumped and hand-copied a width table. It follows the four
pointer forms only, never a fall-through, and reads each block once.

`--the-floor` is the block below, read rather than remembered: six runs at the six lever
settings in one process, printed with **the differences between them worked out by subtracting
two of those same six rows**. A difference is only reported for a pair exactly ONE lever apart
and it names both rows, so no sentence about a lever can outlive the base it was measured
against — which is precisely how the block below went stale in five of six rows while every
sentence quoted from it stayed true. **It earned itself at 239**: running the signs changed
`--surf` from costing two flags to costing one, and the command printed the new difference off
the same six runs that printed the new rows, so the sentence moved in the output rather than in
somebody's memory. It also prints `--boat`'s flag cost as +61 or +60 depending on `--in-order`,
which is the kind of thing a hand-kept table rounds off.

`--the-scan` is the error bar on every map-scan number: reads against byte positions for **every**
command code, and a per-kind table with the ALONE columns — what each of the five kinds of script
reaches, asks and moves that no other kind does. `--two-commands` measures what `0x63` and `0x65`
take, with floors. `--arrivals` reads the condition on every script a map runs on arrival — **a
variable AND a value** — and asks whether any `setvar` in the scan ever writes that value. Nought
name a variable nothing writes; **28 of 69 want a value nobody writes**. Only a `setvar` says what
value it writes, so a condition satisfiable through a `copyvar` reads as satisfiable by nothing:
that overstates the boundary rather than understating it, which is the safe direction.

`--standard` is the routines reached by NUMBER. It counts what the maps ask for, hunts the table
by shape with a reversed-image floor beside it — **24 candidates in the file and 0 in the
reversal, and no way to choose between the 24**, because a pointer to `nop ; end` passes "reads
as a script" — and then answers the question the table was wanted for from the other end: **if
`callstd N ; compare 0x800D ; if` has nothing in front of it that could have answered, the
compare is reading what N left.** `0x05` has 152 such sites and `0x00` has two. Derived only
from sites where nothing else could have answered and applied to sites where something could,
which is the opposite direction and not circular.

`--routines` prints **calls AND call places per routine** since 231 — the places-not-reads rule
asked of a routine number rather than of a command code, which nothing had ever done. It is 936
byte positions for 4461 calls, and 60 of the 178 routines answer differently depending which you
ask for; `0x0AB` is 97 calls at ONE address. It also has the barrier (220) and prints what it does
not credit: the sites whose
compare is only past a `call`, another `special`, a `callstd` or a `0xA0`, in their own section
with the values they were being credited with. **It also prints branches as sites AND as byte
positions**, because a block hanging off two triggers is read twice and only one of those two
numbers is about the cartridge. And since 221 it says, for every site whose whole claim is past
a barrier, **what was in the way and whether it can have answered** — three verdicts, the third
being that the reading does not know, which is what a `callstd` gets because nobody here has
ever read a standard routine.

`--through-a-call` follows a `call` one level and says what it leaves in the answer variable:
a routine's answer, a number the block says out loud, another variable, or nothing. **A literal
on the straight line is a constant only when nothing anywhere in the block asks a routine** —
`0x081BBB1E` ends `setvar 0x800D, 1` and its LESS arm ends `setvar 0x800D, 0`. One level only,
and a call inside a call leaves nothing rather than being chased. `Returns` reads one level of
ARMS as well and says what a block can leave and which routines the choice turns on — and
**a block that ends by jumping away is reported as that, not as leaving the variable alone**,
because those are different facts and one of them is about the instrument. Where the call
provably leaves the variable alone — and **only** there — it walks BACK in the caller for the
answer the compare is really reading, stopping at the same barrier list going the other way. `--who-reads` is `--who-writes`'s mirror and is eleven milestones late: it finds every
`compare`, `comparevars` and copy-from that looks at a variable, with the reversed-image floor
beside it. **The source of a copy is a read and the destination is a write** — counting both
would make every write a read as well. Its aggregate ("650 in the save's band are written and
never read") is BELOW its own floor of 1070 and the instrument says so; only the per-variable
answers mean anything. `--in-the-image` scans all 16 MiB for the bytes that move a flag, says of every hit whether the
map scan ever decoded that byte, and climbs to whatever names it. `--who-writes` is its mirror
for variables — **and both of them answer about the IMAGE, down every arm of every branch.** A
run takes one arm. `--trace 0xNNNN` is the same question asked of the RUN: every write and
every read, in order, with what the variable held at the moment somebody looked — but **its
address column is the script that ran, not the site of the write.** `--stops 0xNN` prints every
stopped read of one command with the run-up and **where the read started**. `--fights` reads
**both** exits of every `trainerbattle` and sorts the fall-through into four shapes; it comes
back "nothing of this kind skips a guard" for six of the eight kinds, which is the answer it
has to be able to give. `--who-knows` asks the WHOLE FILE who knows a move — the obstacle scan
asks the maps, and the maps are 0.6% of it — and prints the reversed-image floor beside the
count, because 600 against 787 is noise and 7 against 0 is not. `--coins` reads the three commands that move a count, and derives the one number none of them
holds: five places read the count, compare it against a bound, branch and hand a quantity over,
and every bound plus its own gift is 10000. Four different pairs, one sum, and the same chain
hunt on the reversed image finds NOUGHT. `--entries` counts the scenes
this cartridge writes as several doors into one room, and separates them from the shared
routines that look identical — by the number each door says, which is different per door for a
scene and the same for a crowd.

## The floor, restated

`--play` alone is not a floor: below the floor on reach, above it on anything a hanging script
hands over. **Two levers are MODELLED — `--say-yes` and `--boat`.** `--surf` is now only an
override: the walk crosses water on its own when the party knows the move, which is READ.
`--in-order` is the one lever that makes it stricter. Say which every time.

**RUN `--the-floor` AND PASTE. It runs all six settings in one process (twelve seconds, one
export between them) and prints the rows AND the differences between them, subtracted from those
same six rows.** Do not apply a delta to this block by hand — that is what put it thirteen
milestones out of date, and 230 built the command so that the absolutes and the sentences about
them cannot come apart. Re-measured at 207, at 230, and **rewritten wholesale at 239**, which is
the first milestone in ten to move a single number in it.

**RE-MEASURED AT 207, all six rows, and five of them had drifted.** The map counts were right;
every flag count was wrong, four party sizes were wrong and one row had the wrong number of
passes. `--play --say-yes` had been carrying **milestone 193's** reading for thirteen milestones.
Every *difference* the table is quoted for survived — `--surf` still costs two, `--in-order`
still adds two and one and a party member — which is why nobody noticed. See `milestone-207`.
If you change anything the run touches, re-run these six and rewrite this block; do not apply a
delta to it.

```
--play                                      183 / 160 in 6, party of 6 at 52, 11 of 104 handed twice
                                            crossing water: nobody ever knew move 57 — a wall
--play --say-yes                            243 / 234 in 6, party of 4 at 67, 10 of 155 handed twice
--play --say-yes --in-order                 243 / 236 in 6, party of FIVE at 67, 0 of 152 handed twice
--play --say-yes --boat                     381 / 295 in 7, party of 4 at 77, 11 of 204 handed twice
--play --say-yes --boat --in-order          381 / 296 in 7, party of FIVE at 77, 0 of 200 handed twice
--play --say-yes --boat --surf --in-order   381 / 295 in 5, party of five at 75, 0 of 200 handed twice
                                            <- --surf now costs ONE flag, not two (239)

the differences, printed by subtracting two of those same six rows:
  --say-yes  (MODELLED)  +60 maps, +74 flags, +0 passes, -2 party
  --boat     (MODELLED)  +138 maps, +61 flags (+60 with --in-order on), +1 pass, +0 party
  --in-order (stricter)  +0 maps, +2 flags (+1 with --boat on), +0 passes, +1 party
  --surf     (override)  +0 maps, -1 flag, -2 passes, +0 party

--play stops because a pass opened nothing new. THE OTHER FIVE STOP BECAUSE THE STATE CAME BACK
TO ONE IT HAD ALREADY BEEN IN — a CYCLE, not a fixed point (239). That is a third answer and not
a failure: a two-cycle has opened everything it will ever open. Do not fold it into "nothing
more opened" — a run that settles and a run that oscillates are different facts about the world.
WHAT MAKES IT GO ROUND is NOT 9.6's 0x0001, which 239 read off the scripts and asserted about a
run: 0x0001 does not move at all in the --say-yes rows and those cycle. It is 0x026C and 0x0807
— scratch flags set on one map and cleared on another, whose value at the END of a pass depends
on which map the walk reached last (240).
```

**381 of 425 no longer needs `--surf`** — and it was 390 until 193 stopped the run playing each
scene once per door into it. Down is the honest direction there: the extra nine were reached by
walking people repeatedly out of their own doorways. The party learns move 57 on pass 3 and swims. The
starter arrives on the floor with `--in-order`, and with `--in-order` on **nothing in the game
is handed over twice**.

Shut doors at 381, counted by reason: **41 never reached the door, 1 arrived on an island, 1
somebody standing in the way** — MT. EMBER `1.103`, behind `0x0089`, which nothing in the world
sets. CERULEAN CAVE is closed: the run now reaches it, off the SAPPHIRE thread.

## Where the reading stands

**AUDITED LINE BY LINE AT 231, against a run of every instrument.** 45 lines checked: 39 were
right, 4 were wrong (fixed here), and 4 quote numbers **no instrument in this repository prints
any more** — those are marked. A number nothing computes cannot come back wrong, which is worse
than a number that is stale. If you change what an instrument reads, re-run it and fix this block
in the same commit; the alternative is what 230 and 231 spent a session undoing.

```
2915 scripts on 425 maps, reaching 3888 blocks
275 of them do nothing but hand over; 26 scenes are one scene entered several ways, 112 doors
doors announce themselves in 0x4001 x63, 0x8008 x25, 0x8004 x23, 0x4002 x6 — TWO bands (237)
3856 read to a proper end, 32 stopped at 19 codes — [0x89]=2 would make it 3857/31 and is DECLINED
729 trainerbattle sites on 104 maps; 27 carry a second exit, 10 of those skipped a guard
7 places in the file ask who knows a move and are jumped into; 0 in the reversal; 5 OFFER
7 blocks in the WHOLE IMAGE offer — the other 2 are CUT's and WATERFALL's, jumped into by nothing
322 flags gate something; 264 are moved by a script somewhere; 233 are the code boundary
  259 of the 264 are on an arm a run could take; the other 5 are behind a switch the script decides
9 people on or beside a door behind 5 flags — the wall list
21 people never arrive at all
11 of 425 maps have no way in at all
5 places guard a coin hand-over; every bound plus its own gift is 10000; 0 chains in the reversal
2 places sell coins for money at 20 each — READ; 3 price lists, 15 rows, all READ
the floor is asked for money in ONE place and it is the coin counter; 8 at --say-yes and above
the widest run sets 212 of the 322 gating flags — 110 gates it never opens; 199 at the floor
  the floor's own gating count went 121 -> 123 at 239 and the widest run's 212/110 did not move
BUT THE RUN ALSO TAKES FLAGS BACK: 164 ever on at the floor against the 160 it stops with (240)
  4 / 6 / 4 / 10 / 9 / 6 taken back at the six settings; 3 of the floor's 4 are on at the start
  and NO script in the run sets them — one script each turns them off (1.57, 10.16, 14.3)
  a set flag HIDES somebody, so clearing one is how the cartridge puts people INTO the world
  costs 0 maps at all six settings, and only that direction can be non-empty — the walk is
  monotone in flags, asserted in TheFlagsItTookBackTests rather than believed
702 signs: 519 a script at 360 addresses on 143 maps, 183 a hidden item — `--export-world` (239)
  317 of the 519 RUN at the floor (214 addresses, 79 maps); 465 at the widest (327, 134) — 242
    241 said 215 and 328: it keyed the read set on (map, ADDRESS) and a sign is a SQUARE. The
    address and map columns were right throughout. 224 for the THIRD time, this one self-inflicted
  the 54 unread at the widest: 36 on maps it never reached, 17 walls on maps it walks, and
    EXACTLY ONE nothing could ever stand beside — 10.6 (4,1), 0x0816C153, same at every lever
  the floor's seven: 0x0031, 0x0032, 0x0233, 0x0234, 0x0235, 0x026D, 0x0834; TWO gate and each
    holds one person — 3.43 p1 and 30.0 p2. Only 2 of the 7 were moved by a sign ITSELF
  every control stops with "nothing more opened", so signs ARE what makes the run cycle (241)
  the RUN could not see ONE of them until 239, because MapData carried no sign list at all
  they move NO map count at any lever setting — not one square of this game is behind a sign
those 110 are 35 no opener, 30 never run, 16 never picked up, 15 obstacles, 7 boundary, 7 TAKEN
  BACK (240) — the sixth bucket, first in the order, and it took from THREE of the other five
35 and 15 are the same at every lever setting, which is how a property of the FILE has to behave
3 scripts hold 27 gating flags: CUT and ROCK SMASH (15, 2 scripts), STRENGTH (12, 1) — CHECKED
  [62 gates / 240 people / 146 trees and rocks / 158 objects: NOTHING PRINTS THESE ANY MORE]
the 12 STRENGTH boulders are SEAFOAM and VICTORY ROAD, and their flags split THREE ways
766 places call 63 routines the widest run cannot answer; 186 have an answer nothing branches on
--routines: 1118 branching sites at 437 byte positions in the file; 48 routines are branched on
0x188's one place comes to nothing
0x4059 has one writer and NO readers anywhere; 0x4055 has 21 readers against a floor of 0
0x083 and 0x084 are asked THREE times between them (1 and 2) and carry 39 of the 64 branches
  nought takes in the widest run's mixed bucket — 3 of its 19 byte positions of 44
336 places read an answer through a call: 225 belong to 6 routines, 57 turn on an arm
40 leave the answer alone and 9 jump somewhere the reading does not follow — those are different
of the 40, 38 read 0x01C's or 0x01D's answer across a call that is `copyvar 0x8012, 0x8013`
11 of the 336 have NO owner: 2 behind a jump here and 9 from 218
the map scan is 2915 entries at 1959 addresses, 90624 command reads at 24491 byte positions
ONLY 11 of 108 command codes are read once per byte position — --the-scan says which
by kind: person 15966 places alone, sign 3015, trigger 2134, on load 1324, on arrival 1167
the two kinds the shared list lost open 2491 places nothing else reaches — 1 in 10 of 24491
0x0A3 is the FAN CLUB on 14.9: eight fans in 0x8004, and the map's on-load asks it eight times
0x63 takes a person and a SQUARE — 26 of 126 hit that person's own square against a floor of 0.45
0x65 takes a person and a MOVEMENT TYPE — 54 of 105 the person's own against a floor of 22.7
neither is NAMED: what they take is read, what they do is still a guess
0x9D's first byte is an INDEX: 9 byte positions in 5 runs, every run 0,1,2 from nought, one in 3^9
0x7F is 0 at all 3 of its places and 0x82 is 1 at all 7 — both UNANSWERABLE, not yes (238)
0x82's word is 7 distinct across 7 places, none a variable; two of them are CUT's and ROCK
  SMASH's own move ids (15, 249) — two of two is not a column, do not build on it
9 routines only the map's own script list asks, 11 only what it runs on arrival — 224's twenty
0x0A7 is one place in the whole game, unbranched, the line before the eight fan questions
0x5C trainerbattle is 794 reads at 729 places and --fights says 729 — two readings agreeing
65 flags are moved ONLY by a map's own scripts: 54 on load, 11 on arrival — the world setting up
0x0070's only two movers in the image are the two arms of one branch on 0x0180, unanswerable
350 arrival conditions at 69 distinct (variable, value, script) on 58 scripts across 61 maps
28 of the 69 want a value NO setvar in the scan writes; 0 name a variable nothing writes at all
0x406F: 20 maps want 1/2/3/5/6/7/8 and the only writer in the scan writes 0, at 3 places
178 routines called 4461 times at 936 byte positions — 936 was RIGHT and NOTHING PRINTED IT
  until 231; 118 of the 178 are called once per byte position and 60 are not
the run's silence decides at 11 byte positions: 0x188 (1) and 0x0A3 (8), 0x0D5, 0x189
--routines: 148 sites have a compare past something, 81 with nothing else — 38 come back,
   40 were somebody else's, 3 not said
callstd 0x05 and 0x00 ANSWER — 153 and 2 sites have nothing in front that could have instead
5660 callstd/gotostd askings at 2791 places, of 9 numbers; the table is NOT found
0x194 is 1066 calls at 34 places; 0x039 is 234 at 234; the worst is 0x0AB at 97 calls at ONE
  place — the ROUTINE inflation runs 1x to 97x, worse than any command code's 67x
0x01C's nineteen sites are ONE address; 219 called them nineteen places
the 57 are TWO blocks, each a yes/no turning on 0x083 or 0x084 and then 0x153
2 of those gates hold NOBODY — 0x084A and 0x084B, the ferry, with no setter anywhere
the floor's 150 -> 153 is milestone 199 alone: 0x026E/0x026F/0x0270 at 10.14, persons 5-10
of 199's three widths, 0xB3 and 0xB4 are in SERIES and 0xC1 opens no flag at any lever setting
the obstacle scripts carry 49 CUT / 97 ROCK SMASH / 54 STRENGTH objects, on 21 / 15 / 15 maps
0x0AB is ONE byte position, 0x081BE07C, reached by those 97 — and all it decides is one 0x27
0x27 is 98 byte positions and 68 of them follow a special, against a floor of 2.35% (2.3 of 98)
those 68 are 36 routines, NOT 41 — 232 wrote 41 and nothing ever computed it (235)
nought of the 98 follow a specialvar; every one of the 68 follows a plain special
22 of the 36 are asked in ONE place; of the other 14, THIRTEEN are waited for at EVERY place
  and 68 of the 82 multi-place routines at NONE — expected under per-site sprinkling: 0.21
0x194 is the only exception BY ROUTINE — and it is not one: 31 of its 34 places set 0x8004
  first, to 18 different values, all on TRAINER TOWER (2.1/2.2/2.10); the one wait is on 0x8004=2
asked of (routine, 0x8004): 269 pairs, 95 in more than one place, and NOUGHT of the 95 are
  waited at some places and not others — chance at 7.3% a place would give 26.6 (236)
25 of the 178 routines take a 0x8004 in the run before a call; 0x194/0x173/0x174 take 18/16/16
0x9C is 7 byte positions and SEVEN distinct words — a column; 3 of them are the obstacle scripts
exactly ONE conditional in the map scan has a 0x27 its target lacks, and it is 0x0AB's
27 numbers are named BOTH as a flag and as a variable in the map scan, floor 1.71 (243)
  0x4001 is 4 flag sites and 326 variable ones; 0x0002 is 23 and 6 and GATES eight objects
  the whole-IMAGE version of the same question says 2117 / 12659 / 1182 — throw it away
0x9C is dofieldeffect, named in ONE place since 233 and privately in EverywhereInTheImage since 191
6 moves pair with 6 numbers: CUT 2, SURF 9, ROCK SMASH 37, STRENGTH 40, WATERFALL 43, DIVE 44
the only repeated move (DIVE, twice) repeats its number — ONE agreement, not six
the 4 numbers no move drives are 62, 64, 68, 69 and ALL SIX move numbers are below all four
  — 6 of 10, which chance would do one time in 210
the same split again, as a different command: the six are followed by an UNNAMED wait (0x27)
  and three of the four by a wait that NAMES the number the effect was started with (0x9E)
0x9E is 3 byte positions in the whole map scan and all three do that — one in 64 conservatively
62 is 1.80 SECTION 49 on arrival, 68 is 2.56 BIRTH ISLAND person 1, 64 and 69 are 10.14 signs
0x0816C994 is ONE byte position reached from NINETEEN sign entries on 10.14
10.14's shared sign block IS a slot machine, READ: "A slot machine! Want to play?" and
  "A COIN CASE is required..." past checkflag 0x0243 — 22 doors saying 0x8004 = 0..21,
  three of them (4, 15, 18) named by nothing; --entries could not see any of it until 237
the raw 0x9C sweep is 11446 sites in BOTH images and the REVERSAL READS ON MORE — throw it away
```

## The next task, precisely

**START HERE — what 239 and 240 opened, and the numbering below is unchanged so item references
still work.**

* ~~Which signs actually ran, and what the seven flags at the floor are.~~ **CLOSED AT 241** —
  `--play --signs`. What is left of it: **why** the seven are what they are (which sign opens
  which of the two people), and the **191 sign scripts that run at no setting** — reach, or a
  square nothing can stand beside, not separated. And `1.114 0x08163F5A`, read 154 times in one
  run, which nobody has asked is a wide sign or a wide walk. And **`10.6 (4,1)`** (242), the one
  sign nothing in the cartridge can stand beside — a mistake, furniture, or a square this
  project's collision reading gets wrong. One `--read-from` and one `--script-map 10.6`.
* **`0x026C` and `0x0807`** — the two that actually make the run go round (240). Set on one map,
  cleared on another, holding nothing. `--read-from` on the four addresses is one command.
* ~~`0x4001` is a flag in the run and a variable in the doors reading.~~ **CLOSED AT 243** —
  both are right, the cartridge holds `29 01 40` at `0x1656AA`, and 27 numbers are used both
  ways. What is left: whether any of the 27 is a MISREAD rather than a real double use (two of
  `0x4001`'s four flag sites have been read), and `0x0002` — 23 flag sites, 6 variable ones,
  gating eight objects, the largest genuine collision and unread on both sides.
* **`9.6`'s puzzle** — fifteen doors, `0x8004` against `0x8008`. Read far enough to say what it
  is; it is NOT why the run cycles, whatever 239 said.
* **`3.57 sign (9,43)`** — the LEMONADE example that has been quoted in this prompt for
  milestones as something the run could not reach. It can now.

1. **`0x0AB` IS READ (232) and the block audit is DONE (231).** What is left of the audit: What is left of it: the three
   numbers nothing prints (`62 gates hold 240 people`, `146 trees and rocks`, `158 objects`) and
   `the ceiling is 45 of 437 byte positions`. Each needs an instrument or deleting; they are
   marked in the block. The next cheap reads are **`0x194`'s nineteen doors** on TRAINER TOWER
   (236), some of which `--entries` may now see since 237 admitted the argument band, and
   **`0x82`'s seven words** — 58, 231, 85, 247, 53 in one run of five, and 15 and 249 in the CUT
   and ROCK SMASH blocks (238). `--read-from` and `--slots` make both one command each. The
   history for reference:
   230 did the floor-row bisect (answer: milestone **199**, one commit, +3 at all six settings,
   announced in its own commit message) and then found the bigger thing: that block, and two
   items of this list, entered the prompt at **`f8d4f15fe`, "the next session's prompt with 190
   folded in"** and **have never been re-run since — thirty-nine milestones**. Eight lines were
   checked at 230; five were right and three were wrong (`258` was 264, `3836` was 3888,
   `3783 / 53` was 3856 / 32 — the last being the pre-199 reading, moved in the same commit
   message as the +3 flags). Items 8 and 9 were sending sessions after commands that already
   have widths. **The other forty lines have not been looked at.** It is one run of each
   instrument and one careful read against the block, and on this evidence it will find more.
   `--the-floor` now makes six of those lines unable to go stale; the honest end of this job is
   an instrument that prints the rest of the block too, rather than a person who maintains it.
2. **The money ceiling is MEASURED and unlevered — decide against the number, not the worry.** 201
   counted it: **8 places** ask the run for money at five of the six lever settings and **1 of
   them hands something over** — `16.0 0x0816F75F` wants 500 and gives `#129` at level 5
   anyway, which is the `#130` at 71 the party ends with. **The floor is clean**: 1 place asks,
   nothing comes of it, so the floor's party of six is entirely earned. Whether that deserves a
   `--pay` lever or a located payout table is a DECISION and it is deliberately not made.
3. **The audit came back mostly clean** (227). `0x5C trainerbattle` is 794 reads at **729**
   places and `--fights` reports 729 — two readings from different code agreeing. `--who-knows`
   answers about the whole image with a floor, so `findmove`'s sixty-six never reached it, and
   the flag work counts flags rather than sites. The two instruments that were wrong were the
   routine tables, fixed at 220 and 223. **What is left is the small codes**, and
   `--the-scan` prints every one of them now rather than the worst two dozen.
   **The 97 command codes whose reads and places differ.** `--the-scan` (224) is the error bar
   for every map-scan number in this project, in one table: 90624 reads at 24491 byte positions,
   and only **11 of 108 codes** are read once per byte. `findmove` is 200 reads at THREE
   addresses. The routine tables have been corrected (220, 223); nothing else has been checked.
   **And check the enumerator before the count.** 224 found the shared script list — created at
   221 to end this very fault — reading three of the five kinds, so 221, 222 and 223 all ran on
   four fifths of the cartridge's scripts. Their findings survived; their numbers did not.
   **`0x0A3` is read** (225): the fan club on `14.9`, eight fans numbered in `0x8004`, asked once
   by each fan and eight more times by the map's own on-load chain at `0x0816F163`. **`0x63` and
   `0x65` are measured** (226, `--two-commands`): `0x63` takes a person and a square in that
   person's own coordinate system (26 of 126 hit their exact square against a chance floor of
   **0.45**), `0x65` takes a person and a movement type (54 of 105 the person's own against a
   floor of **22.7**). **Neither is named** — what they take is READ, what they do is still a
   guess, and naming them would need the game's own code. **`special 0x00A7`**, which opens the
   chain, is the cheap next read.
   Also owed: **the standard-routine table** (222 hunted it — 24 candidates against a floor of 0,
   no way to choose because a pointer to `nop ; end` passes "reads as a script"; untried rules
   are that entries be distinct and longer than two bytes). **`callstd 0x05`'s 251 "not said"
   sites**, where 219's walk back gives up. **`0x0188`'s last three**, behind a block that jumps
   away. **`0x081A77B0`**, where 218's jumping arm goes. **`0x0153`**, half of every one of the
   fifty-seven decisions. **Seven boulder flags with no setter**, **`0x0805`**, and **`0x0053`**
   holding 31 people across the SILPH CO. floors.

4. **Money, for real this time — and the prices are READ now.** Three drinks at 200/300/350 and
   a POKé DOLL at 1000, plus 208's ¥20 a coin and fifteen coin prices, all READ, all at counters
   the run reaches, against a purse of nought. `--money N` is the lever and it is MODELLED; **the
   payout table is still unlocated**, and that is the one number that would make the lever
   unnecessary. 197 filed the POKé DOLL as a reach problem and 198's rule change showed it is a
   money problem after all — the reverse of 197's own correction, and only the fix could tell.
5. **`Attempt.Ran` is fixed (196) and it moved nothing, for a reason worth carrying.** The key
   is `(map, address)` now and five breaks caught it. But the tally 196 added says the only
   consumer in the repository is asked about **one** setter at three lever settings and **zero**
   at the other three. `--flags` never looked at it at all — it takes only the ROM. Before the
   next "X is wrong everywhere", print how many places ask X.
6. **`--entries` reads only the scripts the map scan opens**, which is 0.6% of the file. The
   same sweep asked of the whole image is `--in-the-image`'s question and has never been asked
   of this shape.
7. **The 41 doors never reached** at 381 of 425, and `1.103` MT. EMBER behind `0x0089` — nothing
   in the world sets it, so it is the code boundary with an address on it. The RUBY is behind it
   (`1.102` person 1), and `32.0` person 3 wants the RUBY and the SAPPHIRE both. The SAPPHIRE
   half is closed (190); the RUBY half is not.
8. **The blocks that still stop — RE-READ AT 230, and the old list was two milestones out of
   date.** `0xB3` got a width at 199 and `0x43` got one at 203; `0xE6` stops nothing now. Today
   `--scripts` says **32 reads stop, at 19 codes**, and 15 of the 19 have something behind them
   at every width that reads on:
   `0xCA (3)`, `0xC4 (3)`, `0xC3 (3)`, `0xA4 (2)`, `0x36`, `0xC6`, `0x98`, `0xA6`, `0x57`,
   `0x61`, `0x7A`, `0x59`. `0x73` still stops four and is still worth nothing (ruled out below).
   Two entries have turned out to be symptoms of a wrong width upstream rather than commands, so
   **check alignment before adopting a width**: `--stops 0xNN` prints where each read started.
   **`--derive`'s verdict is advisory — READ THE BYTES.**
9. **The ones no width reads on from are TWO, not four** — `0x9B` (4 stops) and `0x62` (1).
   `[0x92] = 5` and `[0xD3] = 4` are both in `ScriptReader` now. A misread means those blocks are
   wrong earlier; finding where is the job that found `0x1F` and `0x6F`.
10. **The five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — and the ~28
   hand-rolled map walks left in `Program.cs`.

## Fixtures lie in one direction

Guards have come back green because **the fixture was more forgiving than the cartridge**:

1. A zero-filled image is a **NOP SLIDE** — every `0x00` is a valid no-op, so a drifting read
   walks sixty bytes to the target and the test passes at the wrong width.
2. Four sites in dead space all "share their run-up".
3. A yes/no with the reward unconditionally after it never tested the answer.
4. **A stand-in fixture guards the plumbing and not the thing** (189). Four guards on the script
   ordering ran against a lambda that handed its results over ready-made.
5. **A fixture built on the shape where the two readings agree cannot tell them apart** (190).
   `AfterTheRocketsTests` put *a line and an end* after a `trainerbattle` — which `--fights`
   now counts as 17 of the 19 sites of that kind, and which **both** readings treat the same.
   It guarded the wrong answer and passed for nine milestones. The gym shape — a `checkflag`
   and a branch — is the one that discriminates.

6. **A test that reads the instrument instead of the world** (193). The break removed the
   behaviour and left the counter alone, and the test asserted the counter. Rewritten so that
   *one step* and *two steps* are different answers, it caught it. The number a milestone adds
   and the thing that milestone changed are two different claims.

7. **Every fixture in the milestone using one of the thing** (194). 193's tests all used one
   map, so none of them could see that a script attached to nineteen Pokémon Centres is
   nineteen scenes. If the rule has a key, the fixture needs two of whatever the key is made
   of — two maps, two numbers, two addresses.

8. **The ordinary case, unasserted** (195). Every fixture covered the interesting halves and
   none of them said what happens in the common one — the same script on a later pass — so the
   break that conflated it with the rare one came back green.

9. **A break run against one test says nothing about which test caught it** (207). 206's break
   edited `MoveNoiseFloor` while the test watched `NoiseFloor` and came back green; the fix is
   not a second test, it is running **each break against both tests** and writing down the 2×2.
   A guard that goes red for somebody else's break is not a guard on the thing it is named for.
   207's matrix: break the move floor → the flag test stays green, the move tests go red; break
   the flag floor → the reverse. Six break runs, one red each time, and the greens are the
   result.

10. **A fixture where the thing being looked for sits somewhere the scan never reaches** (208).
   `B3 v; B4 g; end` looks like the test for "a read with no compare after it is not a guard".
   It is not: the hand-over lands at index ONE and the fall-through scan starts at three, so the
   fixture answers correctly for a reason that has nothing to do with the compare. A break that
   removed the compare check came back green against it. **Ask where in the fixture the thing
   you are asserting about actually is.**

11. **A fixture that fails the reader before it fails the rule** (208). The replacement for the
   above put filler where the branch was, so the block stopped decoding and failed the "reads as
   a script" filter first. It passed because the block was broken, not because the branch was
   missing. **The thing you blot out has to be replaced by something the same width that the
   reader still understands.**

12. **A fixture that violates two rules at once cannot test either** (213). "Every object behind
   the gate is asked about a move" and "they agree about whether they are removed" both guard
   one function. The mixed fixture broke both, so a break that weakened the first was caught by
   the second and the first stayed untested and green. **A fixture for rule A has to satisfy
   rule B.**

Check for these shapes directly rather than waiting for a break to find them. And the same nop
that makes a slide can make a width **undiscriminable**: the `0x6F` fixture separates four from
one and cannot separate four from three. That limitation is written into the fixture rather
than left to be discovered. **A test named for a discrimination it does not make is worse than
no test.**

## Known flaky

`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother` failed once on a loaded machine
(55s suite instead of 28s) and has passed every run since. Timing-dependent, so it is a guard
that can lie in both directions. If it is red, re-run before believing it.

## A note on guards

A break that comes back green is a claim about the break as much as the guard. One rule went
green **three times** at 189, each for a different reason. At 190 a break went green because
the rule being broken was a `Where` inside `Program.cs`, which no test can reach — it moved to
`Attempt.HandedOverTwice` and was caught on the second attempt. **That is the sixth time the
same structural fault has been fixed by moving a rule about the world out of the printer.** If
a break passes, suspect the fixture — or where the rule lives — before the code.

**A rule fixed in one arm and left standing in the other** (220, and 173, and 207). Two
readings in this repository scanned forward from a `special` for the same compare for the same
reason; one was given a barrier at 214 and the other had none, and they contradicted each other
out loud for six milestones without anybody asking both. **When you fix a reading, grep for who
else reads that shape** — and prefer exposing the one list to copying it, because a copy is how
they came apart.

**A SHARED wrong list is worse than five private ones** (224). Five copies of "every script on a
map" disagree with each other and can be caught by comparing them; one shared copy agrees with
itself everywhere. 221 unified five three-kind copies onto a new three-kind list while a sixth
reading in the same repository had known about five kinds since 179, and nothing compared the two
totals — 2331 against 2915 — for three milestones. **When you unify duplicates, unify onto the
one that knows the most, and print both totals once.**

**A green break FOUR milestones running meant the RULE was in the wrong place, not the guard**
(219, 221, 222, 223). This is no longer a coincidence and it has a cause: this project puts its
rules inside whole-world sweeps — a function that needs a `MapLibrary` and sixteen megabytes —
and a whole-world sweep is exactly what a fixture cannot reach. **Before writing a rule inside a
sweep, ask what a test would have to build to reach it**, and split it out first. At 222 it happened twice in one milestone: both rules the verdict rests on lived
inside a function that needs a whole cartridge, so no fixture could reach either. **When a break
is green, ask where the rule lives BEFORE you suspect the fixture** — on this evidence that is
the likelier of the two, and the note below has had it the other way round since 190.

**The same, first stated as** (219, 221):
At 219 the line being broken was a second copy of a rule nothing could reach; at 221 it was two
lines inside a function that needs a whole cartridge to run, so no fixture could reach it either.
Both times the fix was to move the rule to where a test can ask it directly, and both times the
re-run break failed exactly one test. **When a break is green, ask where the rule lives before
you suspect the fixture.**

**A guard nothing can reach is not a guard** (219). The walk back past a call had a `case Call`
arm of its own sitting immediately above a barrier check that already contained `call` — two
statements of one rule, and breaking the reachable-looking one changed no behaviour because
nothing reached it. The green break was correct: there was nothing there to break. Deleting the
arm and re-breaking the list caught it in one test and nothing else. **When a break is green,
ask whether the line you edited is the line that decides.**

And run the break against **every** test that could plausibly catch it, not just the one it was
written for. 206's break was aimed at one of two near-identical functions while the test watched
the other, and nothing about a single green run says which. 207 writes the 2x2 down instead.

## Things already ruled out — don't re-chase these

* **What is between a shopkeeper and the floor.** `0x80`, read twice with a control each time —
  91.9% against 8.9% by what it stands beside, 22.5% against 0.3% by its own shape. Named on
  `MetatileBehaviour.Counter` with the evidence. The walk talks across exactly one of them.
  Closed; do not re-derive it.
* **The clerks being walled in.** They are not — every one has 2 or 3 walkable squares beside
  them, on the clerk's side of the counter. Walkable is not reachable, and the collision byte
  answers a different question from the distance.
* **`--flags` using the playthrough.** It does not. `case "--flags"` reaches
  `WriteFlagGates(rom)` — one parameter, and it is the ROM. Nothing in it has ever seen an
  `Attempt`. Diffing `--flags` across a playthrough change is diffing a scan that did not look.
* **Anything else in the run keyed on an address alone.** The grep 194 asked for is done and
  `Ran` was the only one. `moved`, `gone`, `spokenTo`, `handovers`, `walkedFrom`, `refused` and
  all five of 195's counted sets already carry the map; `alreadyRun` is a per-map local, the
  same key by scope. Do not re-run this grep.
* **The blockers' own scripts.** All four contain **no conditional of any kind**. What moves
  them is on the object's record, not in the script.
* **`0x4001` is scratch, not a story counter.** 285 scripts write it. The scratch pads stop at
  `0x4010` — a cliff in the write-count distribution, measured, with the cut MODELLED.
* **The playthrough's reader.** It is `HowAScriptRuns` in RomExtract now, with a fixture. Do
  not put script-running logic back into `Program.cs`.
* **SILPH CO. and SAFFRON.** Closed in the reading (176) and now in the playthrough (181). The
  doors are open. Do not reopen it.
* **`--say-yes` "costing" party members.** It never did — the six were four duplicate gifts.
* **Levels.** The party grows now — 3 at level 75 with the sea open.
* **`0x73`.** It stops runs and it is worth nothing — the block ends two bytes later at every
  one of its four sites. Both 4 and 5 parse and adopting either opens nothing.
* **`0x009D` and the nineteen who never arrive.** Closed.
* **SILPH CO., `0x003E`, `0x003F`.** Closed. The `setflag` is not behind a branch at all.
* **`0x1F` and `0x6F`.** Both settled off columns of five sites. Do not re-litigate them; do
  look for a third.
* **The run running triggers whose condition is unmet.** Answered by `--in-order`, a lever
  rather than a decision. `1.57`'s trigger fires at `0x4060 == 0`, so its condition is met.
* **The order scripts run in on a map.** Arrival scripts, then triggers, then people — the
  cartridge's own order. Do not "tidy" those three loops.
* **The trigger north of PALLET TOWN "re-opening the story every pass".** It does that only
  without `--in-order`. Traced in order, `0x4055` is written four times, **all on pass one**,
  and `0x4050`/`0x4052`/`0x4054`/`0x4057`/`0x4060`/`0x4031` are each written once. The lever
  closed it.
* **The run's reach being the last pass rather than the union.** Measured at 190: the union of
  every pass equals the final reach at all five lever settings, even though the boat-and-surf
  runs dip from 376 to 374 between passes one and two. The dip heals. Do not re-chase it.
* **Where a beaten trainer resumes.** Settled at 190 off `--fights`: the bytes after the
  command, and the fight's own script runs once, on the pass that wins it. Kind 1 off eight of
  eight; **kind 2's two sites were read by hand at 191** — `1.114` person 6 (the SAPPHIRE) and
  `14.2` person 5 — and both have a guard in the fall-through. Closed.
* **Which move crosses water.** Move 57, read twice: the move table's own name, and the only
  block in the image that offers to cross water (`0x081A6AD6`, jumped into, on no map, saying
  *"The water is dyed a deep blue… Would you like to SURF?"*). `--surf` is now the override
  only. Do not put the lever back in front of the fact.
* **CERULEAN CAVE.** Reached. The SAPPHIRE comes off `1.114` person 6's fight and `32.0` person
  3 takes it.
* **A cutscene's displacement.** The steps travel now and the walk stops at the first square
  nobody can stand on. **Nobody is off the map at any lever setting**, and the export check says
  every person the cartridge places stands on the map it places them on. Do not put the sum back.
* **What stops a scene running twice.** Nothing does, and nothing needs to: it is not a flag,
  it is that four entry stubs are one scene, so the same `applymovement` command runs four
  times. Each command applies once **per map** — nineteen Centres share one nurse and that is
  nineteen scenes, which 193 got wrong and 194 fixed. 193 closed this and retired both designs 192
  had costed for it — including changing the settle test, which is now **sound**: the state
  stops moving when the loop stops, and the final walk agrees with the last pass exactly.
* **The run's reach being the last pass rather than the union.** Measured at 190, 191 and 192:
  the union equals the final reach at every lever setting, even where a pass dips. And since 193
  the final walk agrees with the last pass's own walk too. Closed.
* **CERULEAN CAVE is not a SAFFRON problem.** `0x005C`, set by `32.0` ONE ISLAND person 3.
* **The fifteen gating flags 0x0011-0x001F.** They hold CUT trees and ROCK SMASH rocks, one per
  map across thirty-odd maps, running two scripts between them (`0x081BDF13`, `0x081BE00C`).
  Their flags are set by the routine that removes the object. Do not file them as the boundary.
* **The twelve STRENGTH boulders** (`0x0040`-`0x0045`, `0x0048`-`0x004B`, `0x0058`, `0x0059`).
  SEAFOAM ISLANDS `1.83`-`1.86` and VICTORY ROAD `1.40`/`1.41`, one script (`0x081BE11D`), which
  removes nothing and sets the shared `0x0805`. **Their flags split three ways** — two set by
  arrival scripts on ROUTE 20 and ROUTE 23, two set out of sight, seven set by nothing. Do not
  treat them as one kind.
* **What sets a flag with no setflag anywhere in the file.** Picking the thing up. The routine
  that hands something over sets the object's own hide flag in compiled code, and only 7 of the
  575 objects carrying a hide flag have a script that sets it — it is written in `Autoplayer`
  beside `what.TakenAway` and was rediscovered the hard way at 211. Do not file those flags as
  the boundary.
* **What the coin commands count and how much fits.** Ten thousand, off five sites and four
  distinct (bound, gift) pairs, with nought chains in the reversal (208). And what a coin costs:
  ¥20, off two sites that ask, give and pay. Do not re-derive either; do look for the payout
  table, which is a different question.
* **A shuffle control on the ceiling sums.** Written at 208, proved unfalsifiable by arithmetic
  and deleted. If every bound plus its own gift is S and no two sites share a pair, a bound
  crossed with somebody else's gift can never be S. Do not write it again.
* **`special 0x0187`.** It heads all three obstacle scripts, its answer is compared against 2
  and only 2 at all 376 of its sites, and `0x081A7AE0` — the arm answer 2 takes — is two bytes,
  `release; end`. Answer 2 means "do nothing". The run answers nought and therefore behaves as
  it would for any answer but one. Closed (214); do not re-derive it.
* **Whether a routine's silence matters is about the BRANCH, not the compared value.**
  `compare 0x800D, 1 ; if LESS` is taken by nought and does not test nought — `0x084` is tested
  against 1 and 2 and nought takes nineteen of its twenty-one branches. `Profile.BranchesTakenByZero`
  evaluates the condition and is the number to use. Settled at 214; do not classify on values.
* **A plain `call` is a barrier in the answer scan.** `special ; call ; compare` reads the
  CALL's answer, not the special's — SEVEN ISLAND's `0x0028` was credited with `0x005D`'s reply
  for as long as the scan existed. Added at 214, 42 of 1097 attributions lost. Do not remove it.
* **Adopting `[0x89] = 2`.** Measured at 237 and declined: width two is the only one that makes
  the argument `0x800D`, which the `specialvar 0x011E` above it just wrote, and the only one that
  gives the arm the same `faceplayer ; end` its two siblings have — but it is ONE site, the
  whole-image column is 1 against a reversal of 0 of 20, and adopting opens exactly one block
  (3856 -> 3857) and moves no run number at any lever setting. A second site would settle it;
  there is not one. Do not adopt it without one.
* **Where the floor row went 150 -> 153.** Milestone 199, `40b589d13`, one commit out of the
  forty-seven between 193's merge and 207, and it is +3 at ALL SIX lever settings — which its own
  commit message said at the time. The three flags are `0x026E`, `0x026F`, `0x0270`, set on
  `10.14`, the GAME CORNER prize counter, by persons 5 to 10. 198's +2 and 200's +1 move the
  `--say-yes` row and never reach the floor. Bisected at 230 with all forty-seven built and run;
  do not re-run it.
* **Which of 199's three widths did it.** `0xB3` and `0xB4` are in series — removing either loses
  all three flags — and **`0xC1` opens nothing at any lever setting**. `0xC1` is the one adopted
  on two sites, below this project's bar of five, and said so out loud; its blast radius on the
  run is nought. Whether it should stay is a DECISION and it is deliberately not made.
* **Calling `10.14` the GAME CORNER.** It is not a name this project read — 199's commit message
  guessed it and 230, 232 and 233 carried it forward. The export says CELADON CITY because bank 10
  is Celadon's interiors and the region-name table gives them all the city's name. What is READ:
  an 18x15 interior, 11 people of whom 5-10 hand something over against a coin count, 20 signs of
  which 19 share one block. Corrected at 234. Describe it; do not name it.
* **The drink, the vending machine, CELADON DEPT, the ferry tickets, the badge-count routine** —
  all dead, see `claude/the-drink-and-the-boat.md`.

## Open, and honestly owed

* Held items are a sixth way a thing changes hands and `Everywhere` does not know.
* ~~Whether the union differs from the final pass.~~ **MEASURED AT 240**: it does, at every one
  of the six settings — by 4 to 10 flags — and 190's "equal everywhere" was a fact about a run
  that could not clear one. It costs nought maps at all six.
* ~~The playthrough never runs signs.~~ **CLOSED AT 239** — and it was never a choice: `MapData`
  carried no sign list at all, so there was nothing for the walk to skip. Still owed off it:
  which signs ran, what the floor's seven new flags are, and `3.57 sign (9,43)`, which asks for
  a LEMONADE and takes it away and can now actually be reached.
* Eleven maps have no way in at all, five of them Sevii isles with no dock in the export.
* A way in reports only the shortest chain, so an upper-bound edge can hide a real one.
* `Bag.PocketCapacity` was counted across the whole bag — fixed at 190 tests ago, but it shipped.
* The purse is modelled and the payout table has never been located. The PRICES are read (208).
* `0x8009` picks which arm of the coin counter runs, 22 scripts write it and NONE on 10.14, so
  the ¥10000 arm is chosen past the code boundary. What the variable is for is not claimed.
* **`MapScripts` — the fifth list — has no test coverage at all.**
* A guard nothing can fail: `SpecialContracts.ComparedAfter`. Decoy or deletion.
* Co-op step 4: a parcel still goes to one person.
* `StoryClosure` deliberately still has no bag, so `--can-it-be-finished` is the no-bag control.
* No milestone docs for `StoryClosure`, `Autoplayer` or `SpecialContracts`.
* Sound is paused: 31 unconfirmed song headers and battle music still open.
* 201 of the floor's 396 "could not answer" places have an answer nothing branches on. They are
  reported and then explained away; they could be taken out of the ceiling line.
* Nothing in this project follows a `call` to attribute an answer. Since 214 the scan stops
  there, so `special 0x005D` inside `0x081A4EAF` is credited with nothing either — the reading
  is now honestly silent where it used to be confidently wrong. Following one level in would be
  a real instrument.
* The 5 flags that look moved and are not — `--flags` prints the count, not the list.
* The raw whole-file sweep is noise: 3762 sites against 3675 in the reversal. Only the
  jumped-into subset is above the floor. Do not quote the raw number as a finding.

Start with `--play`, `--flags` and `--who-knows`. I'll paste the output.

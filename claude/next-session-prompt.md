> **This file is the prompt.** It lives in the repo at `claude/next-session-prompt.md` and in the
> attached Claude Project at the same path, and the two are written together. You do not have to
> paste it — opening a session with *"read `claude/next-session-prompt.md` from the project and
> carry on"* is enough, and it cannot go stale against the repo copy the way a paste can.

I'm building MonMMO, a from-scratch MMO whose data is extracted from my own Pokémon FireRed
cartridge. C# / .NET 8, xUnit, Raylib-cs client, SQLite server. Repo is at
`~/OneDrive/Desktop/pokemmo`, branch `main`, everything merged. Base is the tip of
`claude-314`, 3274 tests green.

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

20. **A break that fails LESS than it should is the same signal as one that passes** (242, and
    again at 245, where one came back fully GREEN). The
    rule "a sign is read from its own square or any of the four around it" was broken to ask
    only its own square, and one test went red where two should have. A sign's own square is
    SOLID — that is what a sign is — so the wrong rule reads every sign in the game as one
    nothing could stand beside, and the only fixture that noticed did so by accident. **Count
    what a break kills against what it should have killed.** A fixture was added, then the same
    break re-run. **245 did it again**: computing "written and never looked at" from every
    number a variable command names, rather than from the ones something writes, passed all ten
    tests — the fixture was built out of flags and a flag is in neither set. **Predict how many
    tests a break should kill before you run it.**

21. **The same trap can be sprung by the milestone that quotes it** (242). 241's own document
    cites 224 — five copies of "every script on a map", counted by the wrong key — one line
    below a count it had made by the wrong key. `215` was addresses-per-map reported as signs;
    the answer is `317`. **When you write a sentence about what a number is keyed on, go and
    look at the key.**

22. **A bare number is not an identity — the COMMAND is** (243). `--trace 0x003F` said "nothing
    the run executed touched it" about a flag a script had cleared on that same run, because
    `--trace` watches a VARIABLE and 0x003F is both. (243 said 27 numbers are named both ways;
    244 says ONE — the rest were a literal counted as a look. The trap stands either way, and
    `--who-reads 0x0001` is still 244 literals out of 283.) Every reading in this repository
    decides by the command and is safe; every ARGUMENT on the command line is a bare number and
    is not. **Before believing an instrument's silence about a number, check which namespace it
    was asking about.**

23. **A number with a denominator and no BREAKDOWN cannot come back mixed** (244). Trap 8 says
    a number with no denominator cannot come back empty. 243 printed "27 numbers used both ways,
    against a floor of 1.71" — both figures right, and what neither could say is that the 27 were
    twenty-six of one thing and one of another. Twenty-six were `0x1A`'s SECOND word, which is a
    plain value unless it is a variable id, so a literal 5 handed to a routine counted as a look
    at variable 5 — and 5 is also a real flag. **Split every sweep by which operand of which
    command produced each hit before believing the total.** It cost nine lines of output, and the
    corrected answer is 1 against the same floor. The test needed no outside knowledge: a
    variable something looks at is a variable something writes, and that operand names 149
    numbers of which 3 ever are.

24. **A READ IS NOT ALWAYS A COMMAND** (246, and again at 247). Trap 1 says the answer is often in a part of the
    file the scan does not open. This is the same fault one level in: the scan opened the right
    bytes and enumerated the wrong KIND of thing. Every sweep in this repository walks a script
    stream and decides what a number is by which operand of which command named it — and a map
    runs a script on arrival *when a variable holds a value*, which is two halfwords in the map's
    own header, names a variable, is a read, and involves no command at all. So 245 reported
    `0x407C` as looked at NOWHERE IN SIXTEEN MEGABYTES while nineteen maps were consulting it, and
    `--arrivals` had been printing all 350 conditions since 229. **Before believing "nothing looks
    at this", ask what kinds of thing can look — not just where the scan looked.** The other
    **247 asked the question of one more record and found the second copy immediately**: a
    trigger fires when a variable holds a value, 228 of them, 42 variables, 52 maps, and no sweep
    counted one. The deaf list went 26 -> 19 -> 5, and all five are loaded by compiled code, so
    the true answer is **nought**. `--who-reads` was still printing "NOTHING IN THE FILE LOOKS AT
    IT" about `0x407C` a milestone after `--namespaces` was fixed — same sentence, different
    sweep. **When you correct a reading, correct every reading of the same shape in the same
    commit, and put the rule in ONE place** (`ReadsThatAreNotCommands`) so a third kind reaches
    every caller at once. **248 asked it of the FLAG side and found the fourth kind**: 183
    buried signs carry an index, the flag that remembers one is a base plus it, and no command
    anywhere names it. Every flag count in this project counts flags something NAMES.

25. **A FILTER THAT LOOKS CONVINCING ON THE FEW AND DIES ON THE DENOMINATOR** (246). The word
    sweep asked only for a four-byte-aligned word equal to a variable's id. On the nine it looked
    excellent — two hits, three hits, reversal nought. Over all ninety variables the map scan
    writes it is **41 against a reversed 27**, which is the same order of number and is exactly
    what 245 threw its own whole-image aggregate away for. Requiring an *instruction that reaches
    the word* takes it to 29 against 4. **Run the weak version's denominator before believing the
    strong-looking few** — and print the without-the-extra-condition number beside the corrected
    one, permanently, so the reader can see which condition is doing the work.

26. **PREDICTING THE BREAK COUNT TURNS A GREEN BREAK INTO A KNOWN HOLE** (246). Trap 20 says to
    count what a break kills against what it should have killed. 246 wrote seven predictions down
    before running any of them and got seven matches — including one predicted **0**, the reversed
    floor, a control nothing could fail. A green break is normally hours of suspecting the fixture
    and then the rule's location; this one was a finding the moment it came back, because the
    prediction said so first. Two fixtures were added and the same break re-run kills exactly one.

27. **A FIELD READING NEEDS A TABLE FROM SOMEWHERE ELSE** (248). "These four bytes are an item,
    an index and a count" is three claims about byte offsets and every one of them parses. What
    makes it a reading is that **171 of the 171 first halfwords that are not NOUGHT resolve to a
    name in the item table's 307 NAMED entries** — a location this project built for a different
    question, which cannot have been tuned to agree. A wrong offset does not score 171 of 171
    against an independent table.
    **When you split a record into fields, find something already in the repository that can
    disagree with the split, and print how often it does not** — and **count the blanks out**.
    248 said "183 of 183 against 308 entries" and twelve of the 183 resolve to entry 0, which the
    table calls `????????`. The same command said so nine lines further down for sixteen
    milestones (264).

28. **TWO BYTES AGREEING EXACTLY IS WORTH MORE THAN EITHER** (248). Twelve of the 183 name no
    item; twelve carry a count above one; they are the same twelve and there are NOUGHT of either
    kind alone. Neither byte alone would have been more than an oddity. Printed as a cross-tab
    with both the overlap and the two singletons, because "12 and 12" without the "and 0" is two
    numbers that might be about different records.

29. **A SET DIFFERENCE NEEDS THE BASE RATE OF THE THING BEING DIFFERENCED** (249). "21 of the 65
    buried items are named by no script" reads as a finding and is not one: a bit under half of
    every item in this game is named by no script at all, so 65 drawn at random would leave about
    30 — the buried kinds are BETTER covered than average and the 21 is BELOW its own floor. This
    is trap 8 in a shape that does not look like a byte scan: **any "N of these are missing from
    that" needs the share of a random N that would also be missing**, computed and printed
    alongside, or it is a number with no denominator wearing a denominator's clothes.

30. **THE NEGATIVE WAS THE FINDING** (249). The interesting result was not the list of what is
    only underground; it was that **nought of the nine items any script asks for has no other
    source**. A reading that can only produce a list produces one whether or not there is
    anything there. Ask the question whose "no" is worth as much as its "yes" — here, does any
    door in this game stand behind a thing you can only dig up — and the answer bounds what the
    walk's inability costs, which the list never could.

31. **AN EMPTY BUCKET IS A FACT ABOUT THE LIST IT WAS ASKED OF** (250). 229 reported "0 arrival
    conditions name a variable NOTHING in the scan writes at all" and it was true. Asked of the
    other list that asks the same question — a trigger's condition, which 247 established is the
    same two halfwords on a different record — the same bucket holds **43**. A bucket that comes
    back empty is the most persuasive output an instrument produces and the easiest to leave
    unexamined, because there is nothing in it to be suspicious of. **When a classification has an
    empty bucket, find the other population the same classification applies to and run it there
    before quoting the nought.**

32. **PREDICTING THE BREAK COUNT TESTS YOUR MODEL OF THE FIXTURES, NOT ONLY THE CODE** (250). A
    break was predicted to kill 2 and killed 1, and the guard was right — the second test names
    the shared reading rather than the caller that reaches it, so it could not have noticed. The
    wrong thing was the prediction. That is still worth the ten seconds: a prediction that misses
    tells you which fixture does not cover what you thought, which is the same information a green
    break gives and cheaper.

33. **TWO INDEPENDENT LISTS WRONG IN THE SAME PLACE CANNOT CATCH EACH OTHER** (251). 224's rule
    is that a shared wrong list is worse than five private ones, because a shared one agrees with
    itself. This is its companion: `copyvar`'s destination was missing from BOTH of this
    repository's write tables, in different files, and having two of them bought nothing. The
    other half of the same copying pair was present in both, one line away in one of them.
    **When you find a table of opcodes, check it against the OTHER operand of the same command
    and against the neighbouring command of the same pair**, and grep for every other table of
    the same shape.

34. **A GUARD CAN ENSHRINE THE FAULT, AND ITS COMMENT WILL SOUND RIGHT** (251). The test on
    `copyvar` asserted the destination was named by NOTHING, on the stated grounds that counting
    it "makes every write a read" — which is true of the READER list and was applied to the
    writer list too. A break aimed at the write table went red against it, correctly, on a rule
    that was wrong. **A red break confirms the guard is wired up; it says nothing about whether
    the guard is right.** What settled it was the instrument's own rule — a variable something
    looks at is a variable something writes — pointed at the change: every reading operand's
    written-ness rose toward 100% and the value-naming one stayed at 2%.

35. **IF THE RULE IS A LIST, THE FIXTURE NEEDS ONE OF EVERYTHING AND HAS TO NAME THEM** (251).
    `EveryWayANumberGetsIntoAVariableIsFound` asserted `4` distinct write commands against a
    fixture built with four of the five. The name promised everything, the count was satisfied by
    whatever the code happened to have, and the fixture supplied the same short list. It asserts
    five BY NAME now, so "five ways" cannot be satisfied by any five commands.

36. **WHEN A TABLE IS WRONG, STOP READING TABLES AND SWEEP** (252). 251 found `copyvar` missing
    from both write tables and 252 asked whether there was a third. Reading the lists again could
    not answer that — so `--operands` scores EVERY halfword-aligned operand of EVERY command by
    244's rule and lets the cartridge sort them. It found two more, and **both were already
    written down elsewhere in this repository**: `specialvar`'s destination is read as the answer
    variable in five files, and `0x42`'s width comment says out loud that it takes two variables.
    **A fact this project already knows in prose is not a fact its tables know.** Grep for the
    knowledge as well as for the list.

37. **NAMING A THING AND KNOWING WHICH WAY IT GOES ARE TWO MEASUREMENTS** (252). Written-ness says
    an operand names a variable; it says nothing about read or write. The direction test is
    whether the NEXT command compares that very number, floor 1.5% over 30766 places — and its
    positive control (`copyvar`'s destination, a write established separately at 251) lands at 65%
    BETWEEN the two unknowns at 91% and 75%. **A test whose known-good case falls in the middle of
    its own findings is one that did not need arranging.** `0x42 arg2` scores 12% and is left out
    of both tables, reported as open rather than guessed.

38. **A MIRROR IS NOT A RE-RUN, AND ITS SEED CAN BE POISONED BY AN EARLIER FINDING** (253).
    Seeding the operand sweep on the writers can only find operands naming variables something
    writes; seeding on the readers is a genuinely different question. But the reader list contains
    the operand 244 identified as naming VALUES, and seeding on its 149 numbers turns "is this a
    variable?" into "is this number small?" — 27 candidates, headed by `giveitem`'s item id at
    100%, against **one** when it is left out. **Before you seed a test on a set, ask what is IN
    the set** — and take the answer from a measurement (`NameValues`, decided by written-ness)
    rather than from a name you recognise. Print both counts: the uncorrected one is the argument
    for the correction.

39. **A COMPLETENESS CLAIM IS ONLY WORTH THE INSTRUMENT'S ABILITY TO DENY IT** (253). "Both write
    tables are complete" is now a real sentence about this cartridge — but only because
    `--operands` found two operands at 252 and would report a third, and because its own
    misleading version is printed beside the good one. A sweep that has only ever come back empty
    has not been shown to be able to come back full.

40. **A NUMBER WITH NO MEANING GETS ONE FROM WHAT IT IS COMPARED AGAINST** (254). `0x42`'s eight
    compares are against 6, 7, 9, 9, 18, 24 and 50 — seven numbers and no finding. Put each one
    beside the WIDTH AND HEIGHT of the map its script is on, both of which this project reads for
    all 425 maps already, and twenty-four on a map twenty-four tall stops being a row. **When a
    value will not say what it is, look for a bound the cartridge already gave you and ask which
    side of it the value falls on.** Only two of the six discriminate and that is enough.

41. **A DISCRIMINATION IS WORTH WHAT ITS NEGATIVE CONTROLS COST IT** (254). The same
    column-or-row test asked of `specialvar`'s answer variable (326 compared places) and
    `copyvar`'s destination (116) comes back naming NEITHER — every value fits both bounds. Those
    two are in the output permanently. **Run a new discrimination against the things you already
    know it should not fire on, and print the result beside the finding**, or "it fits" is a
    sentence about arithmetic rather than about the cartridge.

42. **A DECLARED LIMITATION IS NOT A MEASURED ONE** (255). `--arrivals` said in its own
    documentation, from 229 onwards, that a value written through a `copyvar` or an `addvar`
    would read as written by nothing — "the direction this is allowed to be wrong in". Twenty-five
    milestones of quoting that with no number on it. Measured: **76 of the 364 middle-bucket
    conditions ARE written**, through `setvar 0x8004, N ; copyvar X, 0x8004`, and `addvar`'s step
    is a literal too. **A caveat you can state you can usually measure, and until you do you do
    not know whether it is a footnote or a fifth of the answer.**

43. **ADJACENCY BEATS A BARRIER LIST** (255). Following a copy back to a literal needs to know
    that nothing wrote the source in between. The general answer is a list of commands that count
    as barriers, and this project has had to fix such a list twice (214, 220). The rule here is
    that the literal must come from the command IMMEDIATELY BEFORE, in the very variable being
    copied from — which needs no list, cannot go stale, and is conservative in the safe direction.
    `0x406F`'s third copy has `special 0x014B` in front of it and is correctly left unread.

44. **A DESCRIPTION OF A BEHAVIOUR IS NOT A CAUSE OF IT** (256). 240 said the run goes round
    because two flags' "value at the end of a pass depends on which map the walk reached last".
    That is what oscillation looks like, restated. The cause is a counting argument: a block that
    reads a flag and writes the opposite is a toggle, and a pass running an ODD number of them
    ends the other way round. Three signs share one such block and three is odd. **When an
    explanation could be written without knowing anything the instrument found, it is a
    paraphrase.** And the parity is checkable: the floor is the one setting that settles and the
    one that reports nothing odd.

45. **NECESSARY IS NOT SUFFICIENT, AND THE EXTRA NAME IS THE TELL** (256). 240's criterion was
    "moves both ways within one pass", which is necessary — and it named TWO flags where the
    sufficient criterion names one. `0x0807` moves both ways twice a pass at one address and ends
    every pass as it began. **A criterion that admits more than the thing you are explaining has
    something missing, and the count of what it admits is the size of what.**

46. **A CLASSIFICATION OF A TOTAL THAT MIXES TWO POPULATIONS CANNOT COME BACK DIFFERENT FOR THEM**
    (257). This is trap 31 one level in. 250 exists because `--arrivals` asked one of its two
    condition lists a question, got nought, and quoted the nought; 255 then split the middle
    bucket four ways and printed the split of the two lists ADDED TOGETHER. Asked of each list:
    the one-hop copy idiom is worth **76 of the arrival list's 282 and NOUGHT of the square
    list's 82**, and the counter is worth **nought of the arrival list and all three** of what the
    square list gained. Neither mechanism touches both. 255's "a fifth of it was wrong, and the
    cause is one two-command idiom" is true of the total and is a sentence about ONE list — 21.7%
    is the average of 27.0% and 3.7%, which are about different things. **A mechanism that is a
    rounding error in the total can be the whole of one list's answer.**

47. **A VERDICT IS WORTH WHAT ITS DOES-NOT-KNOW COLUMN IS SMALL** (257). "N conditions can never
    fire" is the sentence this reading exists to produce and NEITHER list can support one: 6
    against an error bar of 192 on the arrival list, 8 against 42 on the square list. The command
    prints the comparison rather than leaving it to be made. **Print the count you cannot read
    beside the count you can, every time, or the second one is a sentence about the reading
    wearing a sentence about the cartridge.**

48. **A LOAD-BEARING ASSUMPTION GETS MARKED OR IT BECOMES A READING** (257). 250 wrote "a variable
    nothing writes holds nought" in prose. Nothing in this repository has read what the save's
    variable block holds before a script writes it — it is MODELLED — and it decides **72 of the
    square list's 228** conditions, the difference between armed at the start and dead. The
    column says MODELLED in the output now, beside three that say READ. **An assumption that
    changes a headline is not a footnote.**

49. **SIX MILESTONES CARRIED A SENTENCE THE COMMIT AFTER IT HAD ALREADY DISPROVED** (257).
    250 said `0x405F` is written by NOTHING and 42 of its 43 squares can never fire. **251 put
    `copyvar`'s destination into both write tables** — and `0x405F` is filled by four
    `copyvar 0x405F, 0x4001` sites on `3.42`, all of which the map scan opens, with `0x4001` set
    to all eight values those squares want. The sentence was copied forward through 251, 252,
    253, 254, 255 and 256. This is trap 14 with the worst possible timing: **the milestone that
    disproves a line is usually the very next one, and it is the one least likely to re-read it.**

50. **A REACHABILITY TEST THAT REACHES EVERYTHING IS NOT A TEST** (258). 255's counter answer
    walks a variable's write set by its steps and asks whether it lands on the value. `0x4001` is
    set to forty-five values and stepped by 1, 2 and 4, so its walk reaches **100 of the 100
    values in 0..99**; `0x4002` and `0x4003` do the same. **Every variable it has ever been given
    saturates**, so the three conditions 255 credited and 257 called the whole of the square
    list's gain were credited by a test that answers yes before it is asked. Corrected, the square
    list's correction is **nought of 82**. This is trap 8 in a shape that does not look like a
    byte scan: the answer was printed with no denominator, and the denominator here is *how much
    of the range the walk covers*. **Saturation is an exact predicate, not a threshold** — the
    walk either covers the range or it does not, which is why it needs no band boundary.

51. **AN OUTLIER IN A COLUMN OF SMALL NUMBERS IS A SENTINEL, NOT A VALUE** (258). Every value
    either condition list names is 0..8, bar a single 17 — and **99, eleven times, all on the
    square list**. Nothing in sixteen megabytes writes 99 to a variable (3 sites against a
    reversed-image 2) and nothing compares one against it. All eleven of those scripts, at
    **eleven distinct addresses**, open `compare <own variable>, 100` and end
    `setvar <own variable>, 100` — where writing your own variable is ordinary (142 of 228) and
    guarding on it is those eleven and nothing else. **The script is doing the record's job**, and
    the eleven are exactly the eleven conditions the reading calls impossible. **Print the whole
    column of values before deciding a bucket is a boundary.**

52. **TRY TO REFUTE THE READING THAT WOULD MAKE YOUR PROBLEM GO AWAY** (258). If the condition
    were `var <= value` rather than `var == value`, all fourteen impossible conditions would
    become satisfiable at a stroke — which is exactly why it is worth attacking rather than
    adopting. `3.42` runs **seven different scripts off `0x405F` at values 1..7**; any inequality
    makes all seven live at once. Equality stands, so the eleven cannot fire, and whether the
    engine special-cases 99 is a question about compiled code — the third milestone in a row to
    end at that wall (248's base, 257's starting nought, this).

53. **A SECOND COPY OF A LOOP IS A GUARD THAT CANNOT BE BROKEN** (258). A break that removed the
    downward arm of the counter walk was predicted to kill nothing and killed nothing. The cause
    was not a missing fixture: `HowManyItReaches` had been written as its own copy of `CanReach`'s
    loop, so the one test covering the downward direction was covering **the other copy**. 224's
    fault, fixed at 220, 224 and 251, walked back into inside the milestone that quotes the rule.
    **When you add a second question about the same thing, share the computation before you write
    the test** — otherwise the suite protects one copy and the break lands on the other.

54. **A FILTER WITH NO COUNT IS A FILTER THAT COULD BE REMOVING ANYTHING** (259). Four event-list
    readers drop a record whose square is off the map, silently, before anything else sees it —
    and 247, 250, 257 and 258 all rest on "228 triggers". Measured: **warps 0, triggers 0, signs
    0**, so 228 is 228 and every reading built on those three lists is complete. The object table
    loses 9 of 1648. **Ask a filter for its count before building a fourth milestone on what
    survived it** — and take the count off the SAME reader, at the drop site, because a second
    pass over the same tables is how 251 lost `copyvar` and 258 lost half a walk.

55. **A BYTE AGAINST AN ARITHMETIC BEATS A BYTE, A POINTER AND A DECODE** (259). Two controls on
    the nine dropped object records disagreed. *0 of 9 carry a pointer into the cartridge against
    1583 of 1584 kept* says they are noise past a table's end, and it is one chance in ten
    thousand billion. *9 of 9 have `localId == index + 1` against 1576 of 1576 kept* says they are
    real. **The second is right**: trap 2 says ask which reading follows fewer edges, and this is
    the fourth time that has decided something. Both stay in the output — a control that misled is
    worth more in the printout than out of it.

56. **A RECORD TABLE CAN HOLD TWO KINDS, AND THE KIND BYTE IS NOT THE COORDINATES** (259). The
    nine are **clones**: `0xFF` in the byte after the graphics id, against nought on all 1639 kept
    records, and every field after the square means something else — the byte the ordinary layout
    calls an ELEVATION is the local id of the object being cloned, and the two halfwords it calls
    a trainer type and a sight range are a map number and a bank. **The record's graphics id is
    that object's graphics id on that map, 9 of 9 against a floor of 0.21**, off tables built for
    another question. Every one hangs off the edge of its own map on the side the map it names
    lies: the person you can see across the join.

57. **THE RIGHT ANSWER FOR THE WRONG REASON IS STILL A FAULT** (259). All nine clones sit outside
    their own map, so the off-map test removed every one and the kind byte was never needed. It is
    decided on the kind byte now and the object list's off-map count goes **9 -> 0** — nothing in
    any of the four lists is off the map at all. **No number moved and 3166 tests stayed green**,
    because the records were already being dropped; what changed is that a clone landing inside
    its own map would have been read as somebody at elevation ten with a trainer type of
    twenty-seven. **A rule the cartridge never exercises is a rule no break can be aimed at** — so
    the fixture carries one, and a fixture's edge case belongs ON the edge: the stray object sat
    at `width + 5`, where `>` and `>=` agree, and a break on that boundary came back green.

58. **DO NOT WRITE DOWN WHICH BYTES YOUR READER READS — WATCH IT** (260). The question "which
    bytes of an event record does nothing in this project look at" needs a list of consumed
    offsets, and a hand-kept one goes stale the first time a field is added: 220, 224, 251, 258.
    `Rom.WatchReads` records what a reader ACTUALLY touched, so the sweep cannot disagree with the
    reader — it is the reader. **When a measurement needs to know what some code does, run the
    code and watch it, rather than restating it.**

59. **A BYTE NOTHING READS IS NOT A FINDING; ONE THAT ALSO VARIES IS** (260). `object +3`, `+11`,
    `+22`, `+23` and `trigger +5`, `+10`, `+11` are nought in every record in the game — that is
    what spare looks like, and a sweep that reported them would have listed every padding byte in
    the cartridge as a discovery. The whole instrument is the difference.

60. **THE SAME ALPHABET IN FOUR INDEPENDENTLY-DERIVED LAYOUTS IS FOUR THINGS SAYING ONE WORD**
    (260). `object +8`, `warp +4`, `trigger +4` and `sign +4` all take values from {0,1,3,4,5},
    dominated by 3 — and two of those four record sizes this project DERIVED rather than knew.
    Named against `MapBlock.Elevation`, a nibble read for drawing: **97.6% / 87.3% / 86.0% / 93.2%
    against floors of ~44%**, and once nought is split out as the wildcard, **three records in
    3863 genuinely disagree**. Every event record carries the elevation of its own square and this
    project reads none of them. The floor is the share of EACH MAP'S OWN squares at the value the
    record carries — a whole-cartridge base rate would have been the wrong denominator, because a
    map that is all one elevation must contribute nothing.

61. **A PREDICTION CAN BE WRONG ABOUT THE CODE RATHER THAN THE FIXTURE** (260). A control break —
    clearing the read-watch between maps — was predicted to kill nothing, on the reasoning that
    byte positions are absolute and no two maps' records overlap. Both true, and not what the code
    does: the record values are checked AFTER every map has been read, so clearing empties the set
    for all but the last. It killed one, correctly. **Trap 32 says a missed prediction tells you
    which fixture does not cover what you thought; this is the other direction, and the first time
    it has been recorded here.**

62. **MEASURE A WORRY BEFORE FIXING IT, AND OVER THE GRID THE RUN ACTUALLY USES** (261). 260's
    "423 of 425 maps are layered and the walk is flat" is a worry. Filled over `map.Collision` it
    came out at **8397 squares across 50 maps** — and that number is about the code: **water in
    this cartridge is COLLISION-ZERO** and is made solid by a metatile behaviour, so the fill was
    walking on the sea. Over `GridFor(false)`, the grid the run steps against, it is **751 squares
    on ONE map**. **Before believing a loss, check which grid you filled.**

63. **THE LEFTOVER WAS NOT A BRIDGE, IT WAS WATER — AND IT NAMES A FAULT IN ANOTHER READING**
    (261). All 751 lost squares are at **elevation 1, which is the sea**: 22250 squares carry it
    and the behaviour pass already makes 21185 solid. Asking what the other 1065 carry gives
    **four behaviours at elevation 1 on 100% of their squares in sixteen megabytes** — `0x1B`
    (751), `0x52` (142), `0x53` (45), `0x50` (42) — against 0-1% for every other behaviour on the
    list, and `0x13` in between at 80%. No band boundary needed. **`0x1B`'s 751 are exactly the
    751 ROUTE 17 squares the layered fill loses**: one value and one number from two directions
    that did not know about each other. **NOT ADOPTED** — each is on one or two maps, below the bar
    237 set when it declined `[0x89] = 2`. `MetatileBehaviour.IsWater` is a READ list; leave it.

64. **NOT EVERY GREEN CONTROL IS A MISSING FIXTURE** (261). 257's and 258's green controls were
    each a rule nothing checked and each earned one. 261's — walking the fill's four directions in
    the other order — cannot be: a flood fill's answer does not depend on visit order, so there is
    nothing there to guard. **Ask whether the thing the break changed can affect the answer at
    all** before writing a test for it; a fixture for a property that holds by construction is a
    test named for a discrimination it does not make.

65. **SAYING A RULE IS MODELLED IS NOT TESTING IT** (262). 261 printed its layer rule, said out
    loud that it was modelled, and reported its output as a cost anyway. The rule is refuted:
    every square 261 called "behind a layer" is on ONE map, and the flat fill crosses to them over
    **336 direct neighbour pairs of `0x1B` beside `0xD0` running the length of ROUTE 17**. A road
    whose two sides touch three hundred times is not two layers. **A difference produced by a
    modelled rule is a number about the RULE until the rule has been tested against the
    cartridge** — and what elevation costs the walk, as far as anything here can show, is NOUGHT.

66. **100% OF X AT Y IS A FACT ABOUT Y, NOT ABOUT WHAT YOU THINK Y IS** (262). 261 found four
    behaviours at elevation 1 on 100% of their squares, believed elevation 1 was the sea, and
    concluded they were water. Asked two ways that cannot see an elevation — what a square BORDERS
    and whether anything STANDS on it — all four look like ordinary ground and nothing like water:
    `0x1B` touches a known water square **0 times in 3004**, less than NORMAL's 0.8%, and `0x52`
    and `0x53` carry people at 3.5% and 4.4% against NORMAL's 1.39%. **And the premise was never
    tested either**: asked the other way round, `0x15` is at elevation 1 on **59.6%** of its
    squares. Elevation 1 is not the sea.

67. **THE BAR IS WHAT MAKES A WRONG READING COST NOTHING** (262). 237 declined `[0x89] = 2` on one
    site; 261 declined its four behaviours on one or two maps each. `MetatileBehaviour.IsWater`
    still holds two values and 262 **had nothing to undo**. A bar that only ever costs you
    headlines has not been tested; this one earned itself.

68. **AN INSTRUMENT WHOSE KNOWN ROWS ARE IN ITS OWN OUTPUT CONTROLS ITSELF** (262). `--sea` prints
    the two behaviours already read as water and three certainly not, beside the candidates. If
    the tallies were wrong, water would not come back at 95.8% and ordinary ground at 0.8%. **Put
    the things you already know into the table rather than into a comment** — it is not a fixture
    and it runs every time.

69. **A NUMBER NOTHING COMPUTES IS EITHER RIGHT OR WITHDRAWN — NEVER CORRECTED** (263). 231
    marked four numbers no instrument printed. Computed at last: **146 and 158 are exact** after
    seventy-three milestones of being uncheckable, and **`62 gates hold 240 people` and `the
    ceiling is 45 of 437 byte positions` reproduce nothing**. The two that fail are WITHDRAWN
    rather than corrected — there is no version of them to fix, because nothing ever produced
    them. **When an audit ends with "it was right all along", that is the honest ending** (231
    said the same of 936), and when it ends with "nothing reproduces this", delete the line.

70. **THE CROSS-CHECK YOU DID NOT ASK FOR IS THE ONE WORTH HAVING** (263). Counting what the gates
    hold gives **605 objects**, and the prompt already says — off the object records, through a
    different structure — that 605 of the cartridge's 1600 objects carry a non-zero hide flag.
    Two directions, one number. **Nothing else in the gate reading has one**, and a reading with
    no independent agreement is a reading nobody has tested.

71. **A PLACEHOLDER IS NOT A NAME, AND COUNTING ONE IS HOW A TEST STOPS BEING ABLE TO FAIL**
    (264). 248's evidence that a buried sign's first halfword is an item id was "all 183 resolve
    to a name in the item table's 308 entries". **Twelve resolve to entry NOUGHT**, which this
    cartridge's table calls `????????` — and `--buried` said so nine lines further down, printing
    "308 entries" and "307 items in the table" in one output for sixteen milestones. Stated
    honestly the reading is STRONGER: **171 of 171 against 307 named entries**. **When a lookup
    table has a blank entry, a hit on it is not a hit.**

72. **ONE MILESTONE MOVED FIVE LINES OF THE BLOCK AND RE-RAN NONE OF THEM** (264). Nine block
    lines had drifted since 231's audit and **five are downstream of 252 alone**, which put
    `specialvar`'s destination and `0x42 arg0` into the operand tables: the variable population
    went 106 -> 115, and every place count and floor computed off it moved with it. **When you
    change what a table contains, grep the block for the population it feeds** — not for the
    number you changed, which appears nowhere.

73. **REACHING IS NOT RETURNING, AND EVERY REACH NUMBER HERE IS THE FIRST ONE** (265). The walk
    is a forward traversal of a graph with one-way edges in it — ledges, doors, the runtime
    sentinel — and "the run reaches 174 maps" has been read as connectedness for two hundred
    milestones. At the floor **24029 of 35142 squares cannot get back**, 137 maps stranded whole,
    through eighteen ledge hops on one route. **A directed graph has two questions and this
    project had been asking one of them.**

74. **HALF A READING IS THE EASIEST KIND TO STOP AT** (265). `Warp.Dynamic` was derived so the
    walker would stop reporting nineteen ordinary exits as holes. It understands the sentinel
    well enough not to call it a hole and not well enough to come back out through one, so the
    walk steps into three lift cabins, counts each as a map reached, and stands there forever.
    **When you teach a reader to stop being wrong in one direction, ask what the other direction
    now does.**

75. **THE LOOSE HALF OF A TEST IS THE HALF THAT CANNOT MOVE** (265). "The far door names this map
    back" scores 237 in the reading and 233 in the control — it is nearly free, because most
    maps' doors all lead to the same place. "It names THIS door back" scores **920 against 219**.
    Two predicates on one question, and only one of them is evidence. **Print both and let the
    control say which.**

76. **A RULE'S JUSTIFICATION IS A NUMBER LIKE ANY OTHER, AND NOBODY RE-RAN IT** (266). Which way
    each ledge is hopped is decided by seven numbers in a doc comment that **no instrument
    printed**. All seven reproduced to the digit — and the criterion the comment NAMES ("leaves
    the geography connected") is not the one it measured (maps reached), and by the named one the
    chosen answer is the worst of the four. **A number in prose beside the rule it justifies is
    231's category even when the rule is right.**

77. **ONE VARIABLE AT A TIME CANNOT DECIDE A VARIABLE THAT IS BEHIND ANOTHER** (266). `0x38` was
    an inference for seventy milestones because "no direction changes the reach" — and with
    everything else a wall the walk stands beside **9 of its 39 squares**. Four identical rows
    mean either "the world does not care" or "the sweep never got there", and only a denominator
    tells them apart. With the other two bytes at their measured values it is decided in one run.

78. **A WIDER POPULATION NEEDS THE KNOWN ROW BEFORE IT NEEDS AN ANSWER** (267). The whole-image
    operand sweep puts four operands above half and none is worth anything, because `compare`'s
    own variable operand — 98% over the map scan — scores **42%** on the same population. The
    ruler is printed beside the reading and the reading is withdrawn. **Before believing a sweep
    on new ground, score something you already know on it.**

79. **A FILTER IS CHOSEN BY THE FLOOR, NEVER BY THE ANSWER** (267). Requiring a pointer to be
    four-byte aligned cuts the reversed-image floor 5.7-fold and the real count 1.3-fold, and both
    settings stay in the output. A tightening whose only evidence is that the result got tidier is
    a tightening the result chose.

80. **THE REVERSED-IMAGE FLOOR IS BLIND TO STRUCTURE, AND THIS PROJECT PUTS IT NEXT TO
    EVERYTHING** (268). Reversing keeps every byte and every byte's frequency and destroys every
    command boundary — and **keeps every table**. A reversed table of text pointers is still a
    table of pointers into the image. So the floor measures the accident rate of a file with these
    frequencies and NO structure, and this file's accidents come from its structure: 456 predicted
    against about 6300 actual, a fourteen-fold gap. `HowClustered`'s own comment has said "a table
    reversed is still a table" since 205 and nobody applied it to the floor itself.

81. **WHEN A CONTROL FAILS, ASK WHAT ELSE HAS THE SAME SHAPE** (268). 267's population was
    condemned by its calibration row and the two explanations — real scripts on the far side of
    the code boundary, or not scripts at all — needed a second axis to separate. The command MIX
    is that axis, and total variation being linear in a mixture makes "how much of this could be
    real" arithmetic on two distances rather than a judgement.

82. **A CONTROL WITH VARIANCE IS NOT A CONTROL, AND ITS OWN VARIANCE IS HOW YOU KNOW** (269).
    Rotation looked like the right null for 268's problem and gave 289 / 2301 / 2449 entries at
    three offsets — an eightfold spread, because rotating by four megabytes moves the pointers out
    of the region scripts live in. The nudge gives 14.9% to 16.4% from four bytes to four
    thousand. **Run a proposed floor at several settings before believing one of them.**

84. **A WINDOW MEASURES THE REGION, NOT THE BLOCK** (270). "A site something jumps into" meant,
    since 175, a jump pointer landing within 192 bytes before the site — 7.5% of unopened flag
    sites against the reversal's 1.3%, the one part of `--flags` called clearly above anything.
    The same pointers aimed 256 to 4096 bytes past the window land in it 5.3% to 7.1% of the time.
    A jump pointer nearby says the site is in script-land; it does not say a script names the
    block. Asked strictly — the jump's own target, read from its boundary, reaches the site as a
    command — it is 16 against a floor of 15..23, and `--who-knows`'s "7 jumped into, 0 in the
    reversal" is NOUGHT: all seven sit just after a neighbouring block that ends before them, and
    what names their own blocks is a literal in code, which the climb had said per site since 191.
    **Before believing a proximity test, ask what it would find if the thing were merely nearby.**

105. **A COUNT OF HOW WRONG SOMETHING COULD BE IS NOT A COUNT OF HOW WRONG IT WAS** (280). 279
    found 97 signs readable from one side that this project read from four, and printed the blast
    radius from the records alone: **68 of the 97 have a walkable neighbour the kind forbids**. Made
    to obey, the walk turns out to have actually stood on the forbidden side of **two** — 0 maps, 0
    flags, 2 signs, at the floor and at the widest, with every one of the six floor rows unchanged.
    Thirty-four-fold between what could have been wrong and what was. This is trap 9 from the other
    end: 196 printed a denominator on the CONSUMER and found the blast radius was nought; here the
    denominator was printed first and the run still used almost none of it.

104. **COUNT THE FIELD INSTEAD OF ASSERTING WHAT IT HOLDS** (279). 248 filtered signs on
    `kind == 7` and this project has said "there are two kinds of sign" ever since. Tallied, the
    byte takes **FIVE** values — 0x00 x422, 0x01 x73, 0x03 x14, 0x04 x10, 0x07 x183 — and **519
    script signs are four kinds read as one**. The tally cost four lines and it is the third time a
    table thought to hold one kind held two (248's buried signs, 259's clones, this). It also makes
    248's reading stronger: every one of the 519 holds a ROM pointer and NONE of the 183 buried
    ones does, so the kind byte separates the two record shapes perfectly.

103. **THE KIND BYTE IS WHICH SIDE YOU READ THE SIGN FROM** (279). 242 reads a sign from its own
    square or any of the four around it, so the walkable NEIGHBOURS of a kind are the test. **0x01
    is read from the SOUTH — 73 of 73, floor 0.0046%. 0x03 from the WEST — 14 of 14, floor 0.0217%.
    0x04 from the EAST — 10 of 10, floor 0.0517%.** The floor is kind 0x00's own rates (87.2%,
    54.7%, 46.9%), which is the kind that names no side. **And the opposite side is open 0 of 14 and
    0 of 10** — which is what turns "one side is always open" into "this side and not that one".
    **97 signs are readable from ONE side and this project reads them from four**; 68 of them have
    another walkable neighbour, which is the blast radius (9). `0x02` never occurs and would be
    north — inferred, not read.

102. **A PERFECT SCORE IS WORTH WHAT ITS FLOOR IS SMALL, AND TWO OF THE ROWS PROVE IT** (279). The
    six buried records that set the spare bit are 6 of 6 on three different properties. *Its item is
    named by NO script* is 68 of 183, so six in a row is one chance in 440. *Its own square is
    walkable* is 142 of 183 — one in five. *The count is one* is 171 of 183 — two in three. All
    three read "6/6" and only the first is anything, and the last two are kept in the table for
    exactly that reason. **And the property is not the bit**: 62 other records hold it without
    setting it, so it is a thing the six have and not what the bit means.

101. **EVERY BLOCK OF SCRIPT THIS PROJECT KNOWS LIES IN 2.5% OF THE FILE** (278). 3888 blocks and
    435 flag sites, all of them, between `0x08160487` and `0x081C5528` — 404 KiB, three of
    sixty-four slices. The populations read against them span 91% (outside ALONE), 92% (outside IN A
    TABLE) and 100% (the reversal). That is not a defect in the null — a sample of real script is
    region-confined because the script is — but it is why the next one bites.

100. **A CHOICE THAT CANNOT BE MEASURED IS MODELLED, AND SAYING SO IS THE ANSWER** (278). The cut
    (277) is a question about how a sample of the REFERENCE should be shaped, so it can only be read
    off members of the population being read that lie inside the reference's span. **Three of the 38
    do**, and at that size a consecutive group of three and an interleaved group of three both touch
    1..2 slices — the two answers are not even different. So the cut is MODELLED and marked as such
    wherever it decides anything. **273 -> 276 -> 277 was three milestones arguing about an
    assumption**, and the reading is printed both ways now because of it.

99. **A FLOOR FOR "HOW OFTEN DOES THIS LAND THERE" IS THE SHARE OF THE THINGS THAT LAND, NOT THE
    SHARE OF THE FILE** (278). Asked whether the 38 sit inside script-land: 3 of 38 is 7.9%, and
    script-land is 2.5% of the cartridge — which reads as a threefold enrichment and is not one.
    **3.9% of the 3674 sites that read as a script and that the map scan does not open are in it**,
    which is the base rate, and 7.9% against 3.9% on a count of three is nothing. The area is
    arithmetic; the share is a fact about the cartridge, and the bytes are not spread evenly.

98. **A NULL'S GROUPING HAS TO MATCH THE SHAPE OF THE THING BEING READ** (277). 273 cut its band
    into CONSECUTIVE groups because neighbours in the cartridge are alike, so a group of them sits
    farther from the whole and the band comes out wider than the truth — every word true, and only
    *conservative* when the population being READ is itself a run. **The 38 boundary sites are
    scattered from `0x028514` to `0xEA7A8F`**, so against a run-shaped null the null carries the
    file's regional structure and the reading carries none of it. Cut to match — every n-th item,
    `Cut.Interleaved`, as reproducible from the file as consecutive is — **real script reaches the
    38's distance in 0 of 102 where in runs it reached it in 6**, the reversal's sites in 19 of 109,
    and the two rate bands do not meet. **273's verdict STANDS and 276's withdrawal of it was the
    null's shape.** Both cuts are printed everywhere now; the difference between them is how much of
    a population's spread is regional structure rather than sampling noise.

97. **THREE WAYS A SCALE FAILS AND THEY ARE DIFFERENT FACTS** (277). The ends can CROSS (no length);
    the thing being read can sit BEYOND the junk end — which is not a strong answer but a broken
    junk model, because nothing can be more junk than junk; or the scale can work and have no
    CALIBRATION. On the 38: 0 of 4 models cross, 3 put the 38 beyond the junk end (all of them the
    nudged site, which is real script read from a wrong boundary), and the one that answers has no
    mixture to calibrate against. **The rate is the reading and the share is not.**

96. **A BAND'S TOP IS A MAXIMUM, AND A MAXIMUM IS A PROPERTY OF HOW MANY TIMES YOU LOOKED**
    (276). 273's verdict on the 38 is "0.601 is OUTSIDE the band a 38-block sample of the maps' own
    lands in", and that band is ELEVEN groups because 435 flag sites is what there is. Asked of the
    maps' own SCRIPTS — same kind of thing, another derivation — a 38-block sample's top climbs
    **0.222, 0.236, 0.278, 0.345, 0.826** as the group count goes 4, 11, 25, 50, 102, and 0.601 is
    inside it. **"Outside the band" is a verdict against a threshold that moves with the sample
    count.** What replaces it is a RATE: real script reaches that far in 5.9% of samples (6/102)
    and the reversal's sites in 33.0% (36/109), so the honest sentence is *5.6 times likelier junk
    than real* and not *these are the reversal's kind*. Trap 8 in the shape of a threshold.

95. **A GROUP SCORED AGAINST A WHOLE THAT CONTAINS IT IS TOO CLOSE TO IT, BY EXACTLY ITS SHARE**
    (276). `SamplingBand` cuts a population and compares each group against the population — which
    holds it. 38 of 435 is 8.7%, and taking the group out moves the maps' own band from
    0.257..0.417 to **0.278..0.451**. Harmless while a band is only being asked whether some other
    population falls inside it; not harmless when the band is the END of a scale. `AgainstTheRest`
    is the version with the group removed, and the rest is BOTH SIDES of the cut — a tail-only rest
    scores the last group against nothing, and **an empty comparand is not an obviously wrong
    answer, it is a plausible one** (it scored exactly what the correct answer scored in one of
    276's own fixtures).

94. **A JUNK MODEL MADE BY MISREADING THE REAL THING IS NOT JUNK** (276). The nudged SITE — a block
    read four bytes past a real `setflag`, in this image, which is the only junk a mixture group can
    be built from here — sits at 0.301..0.496 where pure real script sits at 0.278..0.451. **The
    distance column does not move with the share at all**: 0%, 25%, 50%, 75% and 100% real all read
    0.24..0.56. At thirty-eight blocks, real script read from a boundary that is not one is
    indistinguishable from real script. The reversal, which is not made out of this file's script at
    all, does separate (0.423..0.896). **Ask what your junk is made of before believing it is junk.**

93. **A BOUND THAT PUTS THE REAL THING AT NOUGHT IS READING OFF A SCALE WITH ONE MARK** (275).
    268's mixture bound is `1 - d(mixed, real) / d(junk, real)`, which divides by the distance to
    junk and so places real script at distance NOUGHT from the reference. It is not there: the
    reference is a SAMPLE of real script and so is everything scored against it, and two halves of
    the maps' own scripts sit **0.178** apart. Handed a group that is **half real script by
    construction the bound reads NOUGHT**, and nought again at a quarter; it first moves at 75%.
    A quantity that says "at most nothing" about a population that is half the real thing is not an
    upper bound, and sampling noise pushes it DOWN — the direction that makes it look decisive.
    **Measure both ends before reading anything between them.** A held-out half costs nothing.

92. **A MIXTURE YOU MADE YOURSELF IS THE ONLY ROW WHOSE ANSWER WAS FIXED BEFORE THE ARITHMETIC**
    (275). Two known populations say what a scale's ENDS read; a mixture of known share says what
    everything between them reads, and the worst miss on those rows is the error bar under every
    other row. Read between the measured ends, 25/50/75 come back 29.5/55.1/97.0, so the bar is 22
    points and "outside reads 9.9%" supports "under about a third" and nothing sharper. **An
    instrument shown only its two ends has not been shown it can read the middle.**

91. **THE CONSERVATIVE ARGUMENT FOR A CONTROL CAN BE TRUE OF THE FILE AND FALSE OF THE CALL** (275).
    273's band is taken off CONSECUTIVE groups *because neighbours in the cartridge are alike, so a
    group of them is farther from the whole and the band comes out wider than the truth*. Every
    caller reached it with a list out of a `HashSet` or a dictionary, so consecutive meant
    consecutive in HASH order and the band was NARROWER — the unsafe direction, for forty
    milestones. In file order the maps' own band at groups of 114 goes 0.163..0.425 to
    **0.156..0.703**. **273's verdict survives the fix and was re-run rather than assumed**
    (0.601 outside 0.257..0.417); 274's numbers do not (26.6% -> 35.3%).

90. **A CONTROL THAT ONLY EVER PRODUCED A COUNT CANNOT BE ASKED WHAT IT IS MADE OF** (275). The
    nudge has been this project's floor for anything that follows a pointer since 269 and
    `NudgedFloor` returned an `int`, so the only junk model the mixture bound could use was the
    reversal — 456 blocks, and the one control 268 showed to be BLIND here. As a POPULATION the
    nudge is seven thousand blocks and sits **0.584** from real script where the reversal sits
    **0.711**, and the outside populations sit at 0.680. **The reversal is farther from real script
    than this file's own accidents are**, and every bound that divided by it divided by too much.
    One loop now, two questions: `NudgedFloor` counts what `Nudged` returns.

89. **A COMPARISON AT EACH POPULATION'S OWN NATURAL SPLIT IS CONFOUNDED BY THE SPLIT** (274).
    Each population's own quarters against its own whole looked like a clean homogeneity test:
    the maps' own scripts 0.086..0.167, the reversed image 0.226..0.506. **At ONE common group
    size they overlap** — 0.163..0.425 against 0.226..0.506 — because a quarter of 3888 is 972
    blocks and a quarter of 456 is 114, and a bigger group is tighter for no reason but the count.
    The natural split is the seductive one because every row is doing the same thing to itself.
    **Put every population on the same group size before comparing their spreads**, and print the
    verdict rather than the two columns.

88. **A DISTANCE MEASURED ON TENS AND ONE MEASURED ON THOUSANDS ARE NOT COMPARABLE** (273). The
    38 unnamed boundary sites' command mix sits **0.601** from the maps' own scripts where the
    reversed image sits 0.504 — which reads as "farther from real script than junk is" and made
    268's mixture bound clamp to nought. It is the sample size: **a sample of 38 drawn from the
    maps' OWN scripts scores 0.220 to 0.360 against its own whole** (0.257..0.417 once 275 cuts the
    groups in FILE order rather than in hash order). With that band printed the
    answer LOOKS clean — 0.601 is outside the maps' band, 0.373 is inside the reversal's — **and
    276 shows the band's top is a maximum over eleven groups: a hundred and two groups of the maps'
    own SCRIPTS reach 0.826, and 6 of them reach 0.601.** **277: those six are the CUT** — in runs a
    group is a region of the file; scattered, none of the 102 reaches it. **A population small enough to name individually is small enough to need
    a sampling band**, and `WhatABlockIsMadeOf.SamplingBand` is it: consecutive groups, so it is
    reproducible from the file, and consecutive is the conservative direction because neighbours
    are alike. **The clamp was the sample size, not a finding, and the command says so.**

87. **A UNIFORM ERROR BAR ON A NON-UNIFORM FILE IS WRONG BY THE BYTE FREQUENCIES** (272).
    `--in-the-image` has said since 175 that a three-byte pattern turns up about once by accident.
    Asked of sixteen unused ids with the same high byte, a `0x00xx` flag's floor is a MEDIAN of
    ten to thirteen sites, most 36 — because `29 LL 00` is two of the file's commonest bytes.
    Every whole-image site count printed for the wall flags was at or below that floor, which
    makes "compiled code moves it" stronger, and the nine-against-one that opened 175 was never
    a nine-against-one. **A floor for a pattern has to be drawn from patterns with the same
    bytes** — the nudge for a three-byte sweep is the same sweep asked for a number nothing uses.

86. **ONE AGAINST A FLOOR OF NOUGHT IS NOT A NAME** (271). 270's strict test found one boundary
    site on a block a jump names, against nought at every nudge, and called it a name. The "jump"
    is `04 40 0d 1c 08` at `0x1E2BF7` — `and r4, r0 ; add r5, r1, #0 ; add r5, #8`, THUMB, in a
    routine nothing names — and the block it "names" writes a variable in no band. At n=1 a floor
    of nought has no power: the accident rate of the test on 125 sites is under 1 in 125 at every
    nudge, which is exactly where one accident sits. **A count of one needs the next step up the
    chain, not a floor** — and the climb had already printed it.

85. **THE LIST OF THINGS OWED CAN BE WRONG ABOUT THEIR SHAPE** (270). 269 owed re-runs of three
    readings "about addresses". Rotated, two of them did not move by one unit: the coin chain and
    the field-effect sweep are content-relative and the reversal was always their control. The
    test for whether a reading is address-shaped exists (83) and it is cheaper than the re-run.

83. **A CONTROL HAS A SCOPE AND IT IS WORTH PRINTING** (269). Rotation is a NO-OP by construction
    for content-relative sweeps — the literal-pool test and the written-and-never-read counts come
    back identical at every offset, because a PC-relative load reaches a word a fixed distance from
    itself wherever the file sits. Those readings are not about addresses, so a null that only
    breaks addresses says nothing about them, and the reversal stays their control.

## Where things are

Read `claude/milestone-280-ninety-seven-signs-and-two-of-them-mattered.md` first, then `279`,
then `278`, then `277`, then `276`, then `275`, then `274`, then `273`, `272`, `271`, `270`, `269`, `268`, `267`, `266`, `265`, `264`, `263`, `262`, `261`, `260`, `259`, `258`, `257`,
`256`, `255`, `254`, `253`, `252`, `251`,
`250`, `249`, `248`, `247`, `246`, `245`, `244`, `243`,
`242`, `241`, `240`, `239`,
`238`, `237`,
`236`, `235`, `234`, `233`, `232`, `231`, `230`, `229`, `228`, `227`, `226`, `225`, `224`, `223`, `222`, `221`, `220`, `219`, `218`, `217`, `216`, `215`, `214`, `213`, `212`, `211`, `210`, `209`, `208`, `207`, `206`, `205`, `204`, `203`, `202`, `201`, `200`, `199`, `198`, `197`, `196`, `195`, `194`, `193`, `192`, `191`, `190`, `189`,
`188`, `187`, `186`, `185`, `184`, `183`, `182`, `181`, `180`, `179`, `178`, `177`, `176`.
**Fifty-two faults closed and every one was in this project, not on the cartridge.** A walk that
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
six and stopped the run, three lines below the documentation saying why it must not; and at 246
**every variable sweep in the project enumerating COMMANDS**, so an arrival condition — two
halfwords in a map's header naming a variable — was not a read, and seven variables were reported
as consulted by nothing while one of them was consulted on nineteen maps; and at 247 **the second
copy of that same fault** — a trigger fires when a variable holds a value and that condition is
two halfwords on a map's third list, 228 of them naming 42 variables, and no sweep counted one, so
the deaf list was 26 when the true answer is that NOTHING this cartridge writes goes unconsulted;
and at 248 **183 buried items whose four bytes nothing had ever read**, each remembered by an
index and therefore by a flag nothing in the file names — so every flag count in this project is
a count of flags something NAMES and is short by up to 183; and at 249 **the run standing on 182
of those 183 and collecting none**, which is what 239 left when it put signs into the walk; and at
250 **`--arrivals` asking its question of only one of the two lists that ask it** — its empty
"nothing writes this variable" bucket is 43 on the trigger list; and at 251 **`copyvar` missing
from BOTH of this repository's write tables** — the other half of the same copying pair was in
both, one line away — so sixteen variables read as written by nothing and every reading operand's
written-ness was short; and at 252 **two MORE write operands in neither table** — `specialvar`'s
destination, which five files already read as the answer variable, and `0x42 arg0`, whose own
width comment calls it a command taking two variables; and at 257 **255's four-way split reported
of the two condition lists ADDED TOGETHER** — the one-hop copy correction is 27.0% of one list and
NOUGHT of the other, the counter is the reverse, and 21.7% is the average of two numbers about
different things; and, in the same reading, **250's `0x405F` headline, disproved by 251 and
carried forward through six milestones** — 42 squares filed as "can never fire" are filled by four
`copyvar 0x405F, 0x4001` sites the map scan opens; and at 258 **a reachability walk that reaches
every value in range** — 100 of 100 for every variable it has ever been given — so the three
conditions 255 credited to a counter, and 257 called the whole of the square list's gain, were
credited by a test that could not say no; and at 259 **a second kind of record in the object
table** — nine clones marked by `0xFF`, whose elevation is a local id and whose trainer type is a
map number, removed until now by an off-map test that had no idea what it was catching; and at
260 **an ELEVATION in all four event records that nothing reads** — `object +8`, `warp +4`,
`trigger +4`, `sign +4`, named against the map's own block nibble at 86-98% against floors of
~44%, with three genuine disagreements in 3863 records, on a world where 423 of 425 maps are
layered and the walk is flat; and at 261 **four metatile behaviours that are at sea level on 100%
of their squares and are in no water list** — `0x1B`, `0x52`, `0x53`, `0x50`, 980 squares, found
because the layered fill lost exactly `0x1B`'s 751 on ROUTE 17 — **and at 262 that reading was
refuted by two tests that cannot see an elevation**, along with its premise (`0x15` is at
elevation 1 on 59.6% of its squares) and the layer rule itself; the water list was never changed,
so the fault closed at 262 is 261's own inference and it cost nothing; and at 263 **two numbers
quoted in this file since 190 that nothing had ever computed** — withdrawn rather than corrected,
because no split of this cartridge reproduces either, while the other two came back exact; and at
264 **five block lines moved by one milestone that re-ran none of them**, and 248's "183 of 183"
counting twelve hits on an item table's BLANK entry; and at 265 **a walk that enters three lift
cabins and can never leave one** — `Warp.Dynamic` was derived so the walker would stop calling
nineteen ordinary exits holes, and it stops there: the sentinel is understood on the way in and
not on the way out, so three maps are counted as reached and are rooms with no exit. **And the
larger half of that one is not a bug at all but a reading**: every reach number in this file is
forward, 24029 of the floor's 35142 squares cannot get back, and nothing had ever asked; and at
268 **the reversed-image floor, this project's standard control, being blind to structure** —
6621 blocks reported as scripts the maps do not reach are not scripts, and the floor that let it
through under-counted the accidents fourteen-fold; and at 270 **the jumped-into test itself** —
a 192-byte window that measures whether a site sits in script-land, on its floor once the same
pointers are aimed past the window, so `--flags`' "8 boundary flags jumped into" is 8 against
4..6 and `--who-knows`' "7 against 0" is 7 against 3..8 and nought on the jump's own block; and at 271
**270's one surviving "name"**, a `call` that is two THUMB instructions in a routine nothing names
— one against a floor of nought at n=1 is not evidence, and the sixty-flag bucket `--flags` has
offered as entry points since 175 is the opening plus thirty-eight unnamed accidents; and at 272
**the whole-image error bar** — "about 1.0 by accident" since 175, against a measured ten to
thirteen for any `0x00xx` flag, so every wall flag's site count was on its floor all along; and at
273 **a command-mix distance read without a sampling band** — 38 blocks sit 0.601 from the maps'
own scripts where the reversal sits 0.504, which is not "farther than junk" but the cost of
thirty-eight blocks: the maps' own score 0.220-0.360 at that sample size, and 268's bound clamping
to nought was arithmetic on two numbers measured on different amounts of evidence; and at 274
**268's own bound, which had no error bar either** — "at most 3.1%, about 121 blocks" becomes
"between 121 and about 1266" once a 972-block sample's own 0.086-0.167 is handed back, so the
maps leading to VERY NEARLY ALL the script was a number nobody had put a band under; and at
267 **fifteen milestones of "the whole image" being owed, answered NO with a number** — the
operand sweep's calibration row falls from 98% to 27% on the half the maps do not lead to, so the
population that would have settled 252 cannot; and at
266 **`--ledges` reporting interior counts as totals** — 954 for `0x3B` where the world has 962,
because its loops start at 1 so every square it examines has four neighbours, and eight ledge
squares sit on a map's outer ring where a hop lands off the map and `HopOnto` refuses it; and at
275 **the mixture bound itself, and the order every band in this project was cut in** — the bound
divides by `d(junk, real)` and so puts real script at NOUGHT from the reference, where two halves
of the maps' own scripts sit 0.178 apart, and handed a group that is HALF REAL SCRIPT BY
CONSTRUCTION it reads nought; and `SamplingBand`'s consecutive groups, conservative because
neighbours in the CARTRIDGE are alike, were reaching it out of a `HashSet`, so the band was
narrower than its own documentation claimed — 273's verdict survives the fix and 274's numbers do
not; and at 276 **273's verdict itself, and the band it was read off** — a band's top is a MAXIMUM
and grows with how many groups were taken (the maps' own scripts reach 0.236 over eleven groups and
0.826 over a hundred and two, and 0.601 is inside that), and a group scored against a whole that
CONTAINS it is too close to it by exactly its share. What survives is a rate: 5.9% of real-script
samples and 33.0% of the reversal's reach where the 38 are, which is 5.6-fold evidence and not a
kind; and at 277 **the shape of every null this project cuts** — consecutive groups are conservative
only when the population being READ is a run, and every population these readings handle is spread
over the whole image, so a run-shaped null was being compared against a scattered reading. Cut to
match, real script reaches the 38's distance in NONE of 102 where in runs it reached it in 6, and
**273's verdict stands after all** — 276's withdrawal of it was the null's shape and not the data;
and at 278 **that whole argument, which turns on a choice nothing can measure** — the cut is about
the shape of a sample of the REFERENCE, only 3 of the 38 lie inside the reference's span, and at
that size the two cuts are not even different. It is MODELLED and marked so, and 273 -> 276 -> 277
was three milestones arguing about an assumption; and at 279 **the sign record's KIND byte, read as
two values since 248 and taking FIVE** — 519 script signs are four kinds read as one, and three of
those kinds name the SIDE you have to stand on to read the sign (0x01 south 73/73, 0x03 west 14/14,
0x04 east 10/10, against the commonest kind's 87%, 55% and 47%, with the opposite side open 0 of 14
and 0 of 10). **242's four-square rule is three squares too many for 97 signs** — and at 280 the walk was made to
obey the record, which costs **0 maps, 0 flags and 2 signs** at every lever setting, against the 68
of 97 the records said could be over-read.

`246` is the read that is not a command, and the literal-pool test for whether compiled code holds
a number — both live inside `--namespaces`, which is now the fullest single instrument in the
project. `245` is the twelve. `244` is the operand that names a value.
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
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --buried
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --operands
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --dropped
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --unread
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --layers
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --sea
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --the-way-back
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --which-way
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --operands-everywhere
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --the-control
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --the-ruler
```

`--the-control` is fifteen seconds; sections 4-6 are 270's and the window-at-nought ladder is
271's. `--flags` sorts the boundary's sixty by what names them since 271 and reads the 38 off a
scale whose ends are measured at 38 since 276, cut to match them since 277, with that cut marked
MODELLED since 278. `--the-ruler` is five seconds and opens with a footprint table (278) saying
where each of its populations lives in the file.

`--the-ruler` is 268's mixture bound asked of populations whose answer is known BEFORE the
arithmetic (275), which nothing had ever done. A **held-out half** of the maps' own scripts must
read 100%, a held-out half of the junk model must read 0%, and between them are **mixtures the
command makes itself** out of known-real and known-junk blocks in a share it chose. The reference
holds none of any row.

```
      population                     distance  268's bound  between the ends   off by   groups
        0% real, 0 + 972                0.735        0.0 %             0.0 %    0.0 %   3
       25% real, 243 + 729              0.571        0.0 %            29.5 %   +4.5 %   4
       50% real, 486 + 486              0.428        0.0 %            55.1 %   +5.1 %   4
       75% real, 729 + 243              0.194       45.2 %            97.0 %  +22.0 %   2
      100% real, 972 + 0                0.178       49.9 %           100.0 %    0.0 %   2
      outside, named ALONE              0.680        0.0 %             9.9 %        -   2
      outside, named IN A TABLE         0.684        0.0 %             9.3 %        -   2
```

**268's bound reads NOUGHT on a group that is half real script**, and nought at a quarter. The scale
runs 0.178 to 0.735 and not 0 to 0.735, because 0.178 is how far real script is from ANOTHER SAMPLE
OF ITSELF. **Since 277 every band here is cut as a SCATTER**, which is the shape these populations
actually have: the KNOWN REAL row at 972 goes 14.9%..60.5% in runs to **44.9%..52.6%** scattered and
the worst mixture miss goes 22.0% to **10.5%**. Under the best-calibrated junk model (+64, worst
miss **4.5%**) the outside populations read **16.1% and 15.5%, so under about 20%** — against 268's
3.1%, 274's "121 to 1266" and 275's "under about a third". And **the best-calibrated model is NOT
the one with the smallest answer** (+4 at 9.9%), which the command checks and prints, because a
criterion that always picks the number you liked is not one (79).

It also gives the junk model a second candidate. `EveryScriptInTheImage.Nudged` is the nudge as a
POPULATION rather than a count (269 only ever produced a count), which `NudgedFloor` now counts:
seven thousand blocks against the reversal's 456, sitting **0.584** from real script where the
reversal sits **0.711**. Four offsets are run and the model is chosen by its calibration and not by
its answer (79) — **and the calibration cannot choose**: the worst-miss column spans 3.5% where the
answers span 19.3%, so what the reading supports is the whole span.

And it prints the sampling band THREE ways, because three separate choices move it and none is
visible from the number: hash order, FILE order (275), and SCATTERED rather than in runs (277). The
maps' own at groups of 114 goes 0.163..0.425 (hash) to 0.156..0.703 (file order, runs) to
**0.068..0.185** (file order, scattered).

`--the-control` is the floor, re-asked (269) — **and since 270 it asks the jumped-into readings
against it** (sections 4 and 5): three predicates side by side, WITHIN THE WINDOW as it has stood,
ON A JUMP'S BLOCK, and ON A JUMP'S OR A LITERAL'S BLOCK, each as named, reversed, and nudged up the
269 ladder, with a verdict line that compares the margin to the floor's own spread (82). The strict
test finds ONE boundary site a jump names — `0x0014` at `0x081C0D45` — and 21 of the 22 the
literal column finds are the NEW-GAME script at `0x081A6481`, cross-checked against
`NewGameLocator` in the output. Section 6 rotates the coin-chain and field-effect sweeps and they
do not move: content-relative, the reversal stays theirs. Three controls side by side: BACKWARDS (what this
project has always used, and 268 showed it keeps every table), ROTATED by a multiple of four
(keeps the tables and the alignment, breaks the pointer-to-target correspondence — and is itself a
bad floor, 289/2301/2449 at three offsets), and THE NUDGE (the same pointers aimed a few bytes
off, stable from 4 bytes to 4096). **Use the nudge for anything that follows a pointer and the
reversal for anything content-relative** — rotation is provably a no-op for the second kind and
the command prints both to show it.

`--operands-everywhere` is the operand sweep over the whole file rather than over the 0.6% the
maps point at (267), which 252 left owed. Population: every four bytes anywhere holding a ROM
address whose target decodes to a proper end, plus everything reachable from it — 8860 entries and
**10240 blocks against a reversed-image floor of 456**, where the map scan opens 3888. It prints
its own calibration row on every population it builds, and that row is why the answer is no.

`--which-way` is the ledge assignment, printed instead of quoted (266). Every ledge byte tried
every way and left a wall, twice: **each on its own**, which is the original derivation and
reproduces all seven of its numbers, and **with the others at their measured values**, which is
the run it could not do. Four columns — maps, squares, stranded, and how many of the byte's own
squares the walk stood beside, which is the denominator that tells "the world does not care" from
"the sweep never got there".

`--the-way-back` asks whether the places the walk gets to can get back to where it began (265).
The walker hands out the edges it TAKES — every enqueue goes through one function that records
and then enqueues — and the reverse traversal is of that record and of nothing else, so the two
cannot come to disagree about what a step is. Three settings, then the same question asked of
the map data alone: the warp mirror (920 name this door back against a control's 219) and the
borders (114 of 116 declared from both sides). **Reaching and returning are two facts and this
project printed one of them for two hundred milestones.**

`--sea` asks which behaviours are water WITHOUT looking at an elevation (262): what a square
BORDERS and whether anything STANDS on it, with the two behaviours already read as water and three
certainly not printed in the same table — so the instrument controls itself on every run. Water is
95.8% beside known water and ordinary ground 0.8%. **261's four are not water**: `0x1B` is
**0 of 3004**, `0x52`/`0x53` carry people at 3.5%/4.4% against NORMAL's 1.39%. **And the premise
fails too** — `0x15` is at elevation 1 on only **59.6%** of its squares, so elevation 1 is not the
sea. `MetatileBehaviour.IsWater` still holds two values and was never changed.

`--layers` is what 260's elevation would cost the walk (261, REFUTED at 262), and it changes
nothing. The flat
fill and the layered fill are ONE fill with one predicate swapped — the flat run passes a rule
that always says yes — so they cannot differ for any reason but the rule, which is MODELLED
(equal elevations, or nought on either side). Over `GridFor(false)`, the grid the run steps
against: **751 squares on ONE map, 3.35 ROUTE 17, 0.94%**, and nought maps. Over `map.Collision`
it says 8397 across 50 maps and that number is about the code — **water here is collision-ZERO**
and is made solid by a behaviour. All 751 are at **elevation 1, which is the sea**: 22250 squares
carry it, the behaviour pass makes 21185 solid, and the 1065 left over give **four behaviours at
elevation 1 on 100% of their squares in the image** — `0x1B` 751, `0x52` 142, `0x53` 45, `0x50`
42 — against 0-1% for the rest, with `0x13` at 80% in between. `0x1B`'s 751 ARE the ROUTE 17 751.
**Not adopted**: one or two maps each, below 237's bar — and 262 refuted the lot, so the bar is
why nothing had to be undone. **The RULE is refuted too**: the 751 are reached across **336 direct
`0x1B`-beside-`0xD0` pairs running the length of ROUTE 17**, and a road whose sides touch three
hundred times is not two layers. The command says so in its own output. **What elevation costs the
walk is NOUGHT.** It also prints the cross-layer pairs — 675 join two non-nought layers, 269 of
them 3-beside-4 — and the fill HOPS now (`MapData.HopOnto`, through the ledge rather than over it,
because a ledge carries elevation nought), which 261's could not: flat reach 79594 -> 79886.

`--unread` is which bytes of an event record nothing in this project reads (260), and it does not
keep a list of offsets — `Rom.WatchReads` records what the readers actually touched, so it cannot
go stale against them. **A byte nothing reads is not a finding; one that also VARIES is.** Seven
spare bytes on an object, four on a trigger, and one field on each of the four lists: `object +8`,
`warp +4`, `trigger +4`, `sign +4` all hold values from {0,1,3,4,5}. **It is the ELEVATION of the
square the record stands on**, named against `MapBlock.Elevation` — a nibble this project reads
for drawing — at **97.6% / 93.2% / 86.0% / 87.3% against floors of ~44%**, and once nought is
split out as the wildcard, **3 records in 3863 genuinely disagree**. The floor is the share of EACH
MAP'S OWN squares at the value the record carries, because a map that is all one elevation must
contribute nothing. **423 of 425 maps are layered and this project's collision reading is flat.**
Its positive control is in its own table: `sign +8/+10/+11` are the buried item, index and count
248 found by hexdump, surfaced here from cold.

`--dropped` is the count under every count of people, warps, triggers and signs (259). Four
readers throw away a record whose square is off the map, silently, before anything else sees it —
so it prints what each list loses, off the SAME readers, collected at the drop site. **Warps 0,
triggers 0, signs 0**, so 228 is 228 and every reading built on those three lists is complete.
The object table loses **9 of 1648**, and they are not off-map people: they are **clones**, marked
by `0xFF` in the byte after the graphics id, where the byte the ordinary layout calls an elevation
is a local id and the two halfwords it calls a trainer type and a sight range are a map number and
a bank. **Their graphics id matches that object on that map 9 of 9 against a floor of 0.21.** Each
hangs off its own map's edge on the side the named map lies. Decided on the kind byte now, so the
object list's off-map count is **0** — nothing in any of the four lists is off the map. It prints
three controls including the one that was WRONG (0 of 9 carry a script pointer against 1583 of
1584 kept, which reads as proof they are noise; 9 of 9 have `localId == index + 1` against 1576 of
1576, which is right).

`--operands` is the answer to "is there a third table" and it does not read one (252). Every
halfword-aligned operand of every command the map scan reads, scored by how much of what it names
something WRITES — 244's rule, which needs no band boundary. **The spread is bimodal with a
chasm**: 83 operands under 10%, 3 between, 10 above 90%, nothing in the middle, so the half-way
threshold does no work and the histogram says so. **Three of the ten were in neither table** and
all three were already known elsewhere in the repository. It also asks WHICH WAY separately —
whether the next command compares that very number — against a floor of **453 of 30766 places,
1.5%**: `0x26 arg0` 91%, `0x42 arg0` 75%, and `0x19 arg0` (a write 251 established independently)
**65%, the positive control landing between the two unknowns**. `0x42 arg2` is 12% and is the one
candidate still open: it names a variable and which way is not read.

**And the MIRROR at 253**: seeded on the READERS — which is what finds an operand naming a variable
no `setvar` in the scan writes, `0x405F`'s shape (something DOES copy into it, four times — 257) —
it produces **27 candidates run the obvious way**,
headed by `giveitem`'s item id at 100%, because the reader list contains `0x1A arg2` and seeding
on its 149 values turns "is this a variable?" into "is this number small?". **Corrected — and the
correction is DERIVED from `NameValues`, not written down — it produces ONE**, `0x42 arg2`, which
252 already had. Both counts and all three seed widths (111 / 82 / 231) stay in the output.
**So BOTH TABLES ARE COMPLETE on this cartridge**, and the instrument would say so if they were not.

**And 254 named `0x42`**: it prints every candidate's places with the script that opened each, and
puts the value the next command compares against **the map's own width and height** — read for all
425 maps already. Twenty-four on SEAFOAM (38x24) and fifty on PATTERN BUSH (60x32) cannot be rows.
**6 of 6 could be a column, 4 a row, 2 ONLY a column: `0x42 arg0` is an X.** The negative controls
are the point — `specialvar`'s answer (324/324 both, 0 only-a-column) and `copyvar`'s destination
(115/115, 0) come back UNNAMED, and a test that named those would name anything. `0x42 arg2` has
ONE compared place and is reported as unsettled. **Whose square it is, is not read.**

`--buried` reads the four bytes a buried sign keeps where every other sign keeps a script pointer
(248): an item id, an INDEX and a count with one spare bit. **171 of the 171 item ids that are not
NOUGHT resolve to a name in the item table's 307 NAMED entries** — a location made for another
question. 248 said "183 of 183 against 308 entries" and twelve of those resolve to entry 0, which
the table calls `????????`; counting a placeholder as a hit is how a test that could have failed
stops being able to, and the honest count is the stronger one (264). The third byte is
**183 distinct values in 0..190**, eight unused (7, 16, 40, 43, 44, 45, 46, 124). The same item is
buried in up to twelve places, so the memory cannot be the item; it is the index, and the flag is
a base plus it. **The base hunt comes back UNANSWERABLE and says so**: 3 gaps in the flag number
line wide enough, 14883 candidate bases, 889 loaded by an instruction against a reversed 84.
**Twelve records name NO item and carry counts of 10/20/40/100** where the other 171 carry one —
the same twelve both ways, nought either way — and all twelve are on `10.14`, which holds 5 of the
5 coin chains 208 found.
**And what is only underground** (249): 65 distinct items are buried and 21 are named by no script
— which is BELOW the floor of 30.3 the base rate gives, printed beside it. The finding is the
NEGATIVE: 9 items are asked for by a script, 3 of those are buried, and **NOUGHT is asked for AND
has no other source**, so the run's total inability to dig costs it no reach at all. It also runs
the six settings and prints how much the walk stands on: **182 of 183 at the widest, on 78 of 79
maps, and it collects none of them** — the one it never even stands on is `1.62 (35,5)`, an ELIXIR
at index 33.

`--play --signs` is the fourth list with **its own control in the same process**: which sign
scripts ran, at how many addresses, on how many maps, **why each of the rest did not** (three
buckets, and the first is about the FILE and must not move with a lever), then THE SAME RUN WITH
SIGNS SWITCHED OFF and the two subtracted. It reproduces 239's before-numbers off one build — 183/153, 243/231,
381/294 — which is what a control is for. Signs are worth **0 maps at every lever setting** and
7 / 3 / 2 flags. Keyed by (map, address): one block read in two towns is two signs and one
address, which is 224 in the run rather than in the scan.

**`--trace N` watches a VARIABLE, not a flag — and `--moved N` is the flag half.** They share the
number space, so each command now says when the number it was handed is used in the other one.
`--moved` prints every set and clear with its script, its map, its pass and which of the four
lists ran it.

`--namespaces` asks the map scan — 238 flags, 236 variables — and prints **the shape of each
namespace and the spread PER OPERAND**, which is what caught 244: one operand held every
out-of-band number. It gives the raw shared count (27) and the corrected one (**1**, `0x4001`,
against the same floor of 1.71), the written-ness percentages the correction rests on, and the
whole-image version (2117 / 12659 / 1182) as the noise it is. **It also answers 184's other
half where the question HAS an answer** (245, corrected at 246): **19** of the 90 variables the
map scan writes are never looked at, 10 past the code boundary and **9 looked at nowhere in
sixteen megabytes**. It was 26 / 14 / 12 until 246 counted the read that is NOT a command — an
arrival condition is two halfwords in a map's header naming a variable, and seven of the 26 were
being read by one, `0x407C` on NINETEEN maps. The seven that moved are named in the output with
what reads each, and the commands-only list is printed beside the corrected one so the size of
the correction is visible rather than trusted.
The whole-image version of that same question is 650 against a reversed-image 1070 — the same
order of number, so only the map-scan one means anything.

**And it counts the reads that are NOT commands** (246, 247): a map header's arrival condition
(27 variables, 350 places, 61 maps) and a trigger's square condition (42 variables, 228 places, 52
maps), both two halfwords in a map's own record. Every trigger in the game has a script and a
variable in `0x4000+`, printed, so both halves of the trigger rule are inert here and its fixtures
are decoys.

**And it asks whether the game's own CODE holds a number** (246), which is the only handle this
project has on a variable no script reads. A sixteen-bit constant does not fit in a THUMB
instruction, so it goes in a four-byte-aligned literal pool and is loaded PC-relative: an aligned
word equal to the id, at bytes no script owns, with an `ldr rX, [pc, #imm]` whose arithmetic lands
on exactly it. **All 5 of the deaf list are held that way** once 247's triggers are counted — and the
question is asked of the boundary bucket too, since "something decodes as a compare" is weak and a
load is not. The word
ALONE is a weak filter and the denominator is printed to say so: 41 of 90 against a reversed 27,
which the instruction takes to 29 against 4. And the limit is printed too — a routine computing an
id from a base holds the BASE — `0x4000` is loaded **1** time against a reversed 0, so a
base-relative reader is possible and thinly evidenced. **"Held by nothing" is not "read by
nothing".** (246's document said 56 here; 56 is the count WITHOUT the load requirement and no
instrument prints it — corrected at 247.)

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
take, with floors. **`--arrivals` measures its own caveat (255) AND asks it of each list separately
(257).** The middle bucket — "a variable something writes, but nobody writes THAT VALUE" — is 364
conditions / 84 distinct across both lists, and it is answered off `setvar` alone. Split four ways:
**76 / 4 ARE written** through `setvar src, N ; copyvar dest, src` (one hop, the command
IMMEDIATELY before, no barrier list); **3 / 3** a counter reaches (`addvar`'s step is a literal);
**192 / 10** copied from a source this cannot read, which is the caveat's real remainder; **93 /
67** where the bucket means what it says. That is 21.7% — **and it is the average of two numbers
about different things.** Per list:

```
  ON ARRIVAL  282 conditions:  76 written / 0 counted / 0 saturated / 192 unread copy / 14 neither -> 27.0%
  ON A SQUARE  82 conditions:   0 written / 0 counted / 3 saturated /   0 unread copy / 79 neither ->  0.0%
```

**Neither mechanism touches both lists, and the square list gains NOTHING.** The idiom 255 is
named for is worth everything on one and nought on the other; the square list has NO unreadable
copies at all, so on that list this reading has no unknowns of that kind. **The counter answer is
a FIFTH bucket now** (258): a walk that reaches every value in range has said yes before it was
asked, and all three of the square list's were that — 257 reported 3.7% by "a different
mechanism" and the mechanism was an artefact.

`--arrivals` reads the condition on every script a map runs on arrival AND on every SQUARE (250)
— **a variable AND a value** — and asks whether any `setvar` in the scan ever writes that value.
Both lists go through one reading and each condition says which asked, because 250 exists entirely
because one of them had never been asked.
**ON ARRIVAL**: 350 conditions, 69 distinct, 58 scripts — 0 name a variable nothing writes, 28 of
69 want a value nobody writes. **ON A SQUARE**: 228 conditions, 128 distinct on 128 scripts (every
square has its own) — **43 name a variable NOTHING writes**, all of them `0x405F`, and 82 want a
value nobody writes.
**And the VERDICT on every condition, with the column that says how much is not read** (257):

```
                    something     armed at      NOTHING CAN     does not know
                    writes it     the start     produce it      (a copy it cannot read)
                    READ          MODELLED      READ            READ
    on arrival        144             8             6             192   of 350
    on a square       103            72            11              42   of 228
```

**71 of the square list's 82 want NOUGHT** — every variable holds nought before anything writes
it, so most of that bucket is armed at the start and something has to write the variable to turn
it OFF. The name reads as unsatisfiable and means the opposite. **The ARMED column is MODELLED**
and says so in the output: nothing here has read what the save's variable block holds at the
start, 250 asserted it in prose without marking it, and it decides 72 of 228.
**NEITHER LIST CAN SUPPORT A COUNT OF DEAD CONDITIONS**: 6 against an error bar of 192, and 11
against 42. The command prints the comparison.
**AND THE SQUARE LIST'S ELEVEN ARE ONE IDIOM** (258): every one wants **99**, a value that appears
in no `setvar` and no `compare` in sixteen megabytes while every other value either list names is
0..8 (one 17 aside). All eleven scripts, at ELEVEN DISTINCT ADDRESSES, open
`compare <own variable>, 100` and end `setvar <own variable>, 100` — against 142 of 228 that write
their own variable (ordinary) and NOUGHT of the other 217 that guard on it. The script is doing
the record's job. `--arrivals` prints that cross-tab, the control (161 square scripts open with a
compare and 150 name a different variable), and the whole column of values with the reversed-image
floor. **Equality stands** — `3.42` runs SEVEN scripts off `0x405F` at values 1..7, which any
inequality would make simultaneous — so the eleven cannot fire, and whether the engine
special-cases 99 is compiled code. The arrival list's three self-guards are ONE address on `31.0`
counted three times, which is why both columns are printed.
**`0x405F` is NOT written by nothing — 250's headline was disproved by 251 and carried anyway.**
It is filled by **four `copyvar 0x405F, 0x4001` sites on `3.42`**, all opened by the map scan
(`0x1A7958`, `0x1A7967`, `0x1A7AA1`, …), and `0x4001` is set to all eight values these squares
want. No `setvar` writes it and the code loads it as an aligned literal 0 times, both still true.
The command before each copy is not a `setvar`, so 255's one-hop rule correctly declines a value —
which makes the honest verdict **DOES NOT KNOW, not CANNOT**, and moves 42 out of the dead column
into the error bar. They are 43 squares on `3.42` ROUTE 23 and `28.0` ROUTE 22 across 8 scripts,
the run reaches both maps, and `--in-order` costs +0 maps at every setting so nothing is behind
them. It is read twice, both `comparevars 0x405F, 0x4001`.
Only a `setvar` says what value it writes, so a condition satisfiable through a `copyvar` reads as
satisfiable by nothing: that overstates the boundary rather than understating it, which is the
safe direction — and `--arrivals` now NAMES the source of every unresolved copy, because
`copyvar`'s second operand is in the bytes even when the value is not.

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
run: 0x0001 does not move at all in the --say-yes rows and those cycle. It is **0x026C ALONE, and
the cause is PARITY** (256): three signs on 1.59, 1.60 and 1.61 all call one block that does
`checkflag 0x026C` and sets it on one arm and clears it on the other — a TOGGLE — and the walk
reads all three every pass. Three is odd, so it ends every pass the other way round. **240's
second name, 0x0807, is wrong**: it moves both ways but TWICE a pass at one address on 2.38, so it
ends as it began. Moving both ways is necessary; parity decides. `--play` prints the flags moved
an odd number of times on the pass the run stopped on, and the floor table is its own control —
five settings cycle and report one flag, the floor settles and reports NONE, because it never
reaches those three maps.
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
7 places in the file ask who knows a move and are "jumped into"; 0 in the reversal; 5 OFFER
  — WITHIN 192 BYTES, which is on a floor of 3..8 (270); on the jump's own block it is 0, and
  5 are on a block a LITERAL in code names. The five offering blocks stand on their text
7 blocks in the WHOLE IMAGE offer — the other 2 are CUT's and WATERFALL's, jumped into by nothing
322 flags gate something; 264 are moved by a script somewhere; 233 are the code boundary
  of the 233, 60 are moved by script the maps never open, 8 "jumped into" WITHIN 192 BYTES
  against a nudged floor of 4..6 (270). SORTED AT 271 by what names the script: 21 are the
  NEW-GAME script at 0x081A6481 (49 setflags, all in FlagsAtStart), 1 is THUMB code read as a
  call (0x0014 at 0x1C0D45), 0 a literal's block, 38 read as script and NOTHING names them;
  the other 173 are moved by no script anywhere. The "entry point to find" was never there
  259 of the 264 are on an arm a run could take; the other 5 are behind a switch the script decides
9 people on or beside a door behind 5 flags — the wall list
  whole-image sites for the five: 0x0013 8, 0x0012 5, 0x0089 9, 0x0053 1, 0x0017 2 — against a
  same-high-byte unused-id floor of median 10-13 (most 36) for 0x00xx, median 1 (most 11) for
  0x0089's neighbours. AT OR BELOW THE FLOOR, every one (272)
21 people never arrive at all
11 of 425 maps have no way in at all
5 places guard a coin hand-over; every bound plus its own gift is 10000; 0 chains in the reversal
2 places sell coins for money at 20 each — READ; 3 price lists, 15 rows, all READ
the floor is asked for money in ONE place and it is the coin counter; 8 at --say-yes and above
the widest run sets 213 of the 322 gating flags — 109 gates it never opens; 199 at the floor
  (264: it was 212/110)
  the floor's own gating count went 121 -> 123 at 239 and the widest run's 212/110 did not move
BUT THE RUN ALSO TAKES FLAGS BACK: 164 ever on at the floor against the 160 it stops with (240)
  4 / 6 / 4 / 10 / 9 / 6 taken back at the six settings; 3 of the floor's 4 are on at the start
  and NO script in the run sets them — one script each turns them off (1.57, 10.16, 14.3)
  a set flag HIDES somebody, so clearing one is how the cartridge puts people INTO the world
  costs 0 maps at all six settings, and only that direction can be non-empty — the walk is
  monotone in flags, asserted in TheFlagsItTookBackTests rather than believed
702 signs: 519 a script at 360 addresses on 143 maps, 183 a hidden item — `--export-world` (239)
  THE KIND BYTE TAKES FIVE VALUES, NOT TWO (279): 0x00 x422, 0x01 x73, 0x03 x14, 0x04 x10, 0x07 x183.
    All 519 script kinds hold a ROM pointer; 0 of the 183 buried do — the byte separates the two
    record shapes perfectly. And 0x01 is read from the SOUTH (73/73, floor 0.0046%), 0x03 from the
    WEST (14/14, 0.0217%), 0x04 from the EAST (10/10, 0.0517%), floors off kind 0x00's own 87.2% /
    54.7% / 46.9%; the OPPOSITE side is open 0 of 14 and 0 of 10. 0x02 never occurs (north, INFERRED)
    **242's four-square rule is three squares too many for 97 signs** and 68 of them have another
    walkable neighbour. NOT ADOPTED — the exported map record carries no kind, and what it costs in
    flags is not measured
  315 of the 519 RUN at the floor (214 addresses, 79 maps); 463 at the widest (327, 134) — 242,
    and 280, which took two off each end by making the walk obey the side the record names
    241 said 215 and 328: it keyed the read set on (map, ADDRESS) and a sign is a SQUARE. The
    address and map columns were right throughout. 224 for the THIRD time, this one self-inflicted
  the 56 unread at the widest: 36 on maps it never reached, 19 walls on maps it walks, and
    EXACTLY ONE nothing could ever stand beside — 10.6 (4,1), 0x0816C153, same at every lever
  the floor's seven: 0x0031, 0x0032, 0x0233, 0x0234, 0x0235, 0x026D, 0x0834; TWO gate and each
    holds one person — 3.43 p1 and 30.0 p2. Only 2 of the 7 were moved by a sign ITSELF
  every control stops with "nothing more opened", so signs ARE what makes the run cycle (241)
  the RUN could not see ONE of them until 239, because MapData carried no sign list at all
  they move NO map count at any lever setting — not one square of this game is behind a sign
those 109 are 35 no opener, 30 never run, 16 never picked up, 15 obstacles, 8 past the boundary,
  5 TAKEN BACK (240) — the sixth bucket, first in the order, and it took from THREE of the other
  five. RE-RUN AT 264: the last two were 7 and 7 and the total was 110
35 and 15 are the same at every lever setting, which is how a property of the FILE has to behave
3 scripts hold 27 gating flags: CUT and ROCK SMASH (15, 2 scripts), STRENGTH (12, 1) — CHECKED
322 gating flags hold 605 objects; 2 hold none (the boat's). 15 tree-and-rock gates hold 146,
  12 boulder gates hold 12, all 27 obstacle gates hold 158, the other 295 hold 447 — PRINTED
  by --play since 263, and 146 and 158 were exact after 73 milestones of being uncheckable
  605 also agrees with "605 of 1600 objects carry a hide flag", counted from the other side
of the 295: 272 hold one, 10 hold 5-16, 8 hold 2-4, 3 hold more than 16, 2 hold nothing;
  21 hold MORE THAN ONE, 175 objects between them — "62 gates hold 240 people" is WITHDRAWN
the 12 STRENGTH boulders are SEAFOAM and VICTORY ROAD, and their flags split THREE ways
860 places call 75 routines the widest run cannot answer; 276 of the places, across 58 routines,
  have an answer nothing branches on — RE-RUN AT 264, it was 766/63/186
--routines: 1118 branching sites at 437 byte positions in the file; 48 routines are branched on
0x188's one place comes to nothing
0x4059 has one writer and NO readers anywhere; 0x4055 has 21 readers against a floor of 0
7 of the 115 variables the map scan WRITES are never looked at by any command: 5 past the boundary
  (0x4010, 0x8001, 0x8002, 0x800B, 0x8012) and 2 NOWHERE in the image (0x4026, 0x403E) — and
  NOTHING this cartridge writes goes unconsulted (245 -> 246 -> 247, population corrected at 251)
  it was 90 variables until 251 put copyvar's destination in the write table — 16 more, and 106
    until 252 added specialvar's destination and 0x42 arg0 — nine more, unrecorded until 264
  it was 26 by the commands alone: 21 of those are read by a map header or a TRIGGER, neither of
  which is a command — 0x407C on NINETEEN maps, 0x400F on 8, 0x400E on 8, 0x4062 on 4
  the value-naming operand hid NOUGHT of them, measured both ways — 244's fault does not reach here
the load denominator: 34 of 115 against a reversed 5 (57 WITHOUT the instruction, which is why the
  instruction is in the rule) (246, 251, re-run 264); 0x4000 is loaded 1 time against a reversed 0
every READING operand's written-ness: 0x19 arg2 93%, 0x21 arg0 98%, 0x22 arg0/arg2 100%, and the
  value-naming 0x1A arg2 at 2% — the whole shortfall is TWO numbers, 0x8013 and 0x4025 (251)
0x4010, 0x4026 and 0x403E are BIRTH ISLAND 2.56's on-load, three setvars in a row, and all three
  are loaded by compiled code and read by no script — 0x4010 x4 across three regions (246)
61 variables are read without a command: 27 by an arrival condition (350 places, 61 maps) and 42
  by a trigger (228 places, 52 maps); 21 of the 61 were on the deaf list (246, 247)
ALL 228 triggers have a script and a variable in 0x4000+ — so both halves of 247's rule are inert
  on this cartridge and its fixtures are decoys, printed rather than assumed
0x083 and 0x084 are asked THREE times between them (1 and 2) and carry 39 of the 64 branches
  nought takes in the widest run's mixed bucket — 3 of its 19 byte positions of 44
336 places read an answer through a call: 225 belong to 6 routines, 57 turn on an arm
40 leave the answer alone and 9 jump somewhere the reading does not follow — those are different
of the 40, 38 read 0x01C's or 0x01D's answer across a call that is `copyvar 0x8012, 0x8013`
11 of the 336 have NO owner: 2 behind a jump here and 9 from 218
the four event lists lose NOTHING to the off-map filter: warps 0/1294, triggers 0/228, signs
  0/702, objects 0/1639 once the 9 clones are taken out on the kind byte instead (259)
9 clone records, all on bank 3, marked 0xFF after the graphics id — elevation is a local id and
  the trainer type is a map number; graphics id matches that object on that map 9 of 9, floor 0.21
every event record carries the ELEVATION of its own square and nothing reads it (260): object +8,
  warp +4, trigger +4, sign +4 — 97.6/93.2/86.0/87.3% against ~44%, 3 of 3863 genuinely disagree
423 of 425 maps carry more than one elevation among their own squares; the walk is two-dimensional
  and the modelled layer rule is REFUTED (262), so what it costs is NOUGHT
elevation 1 is NOT the sea: 0x15 is at elevation 1 on 59.6% of its squares, 0x10 on 99.2% (262)
0x1B/0x52/0x53/0x50 are at elevation 1 on 100% of their squares AND ARE NOT WATER — 0x1B touches
  known water 0 times in 3004 and 0x52/0x53 carry people at 3.5%/4.4% against NORMAL's 1.39% (262)
0x1B is 751 squares on ROUTE 17 bordered only by itself and 0xD0 (336 pairs); neither is named
675 walkable pairs join two different non-nought layers, 269 of them 3-beside-4 — the bridges
EVERY BLOCK OF SCRIPT THIS PROJECT KNOWS lies between 0x08160487 and 0x081C5528 — 404 KiB, 3 of 64
  slices, 2.5% of the cartridge (278). Outside-ALONE spans 91.4%, outside-IN-A-TABLE 91.7%, the
  reversal 100%. 3 of the 38 boundary sites are in it (7.9%) against a MEASURED 3.9% of the 3674
  unopened sites that read as a script — the area, 2.5%, is the wrong floor and would have read as
  an enrichment
the file holds 10240 blocks reachable from an ALIGNED pointer against the map scan's 3888 — and
  **the 6621 outside ones ARE NOT SCRIPTS** (268). Command-mix distance from the maps' own
  scripts: outside-alone 0.690, outside-in-a-table 0.698, THE REVERSED IMAGE 0.711; the outside
  populations sit 0.24-0.32 from the reversal. The mixture bound (total variation is linear, so
  this is arithmetic) puts at most 3.1% and 1.8% of them on the real side — about 121 of 4825.
  **274: THAT BOUND HAS NO ERROR BAR AND IS NOT SUPPORTED.** A 972-block sample of the maps' own
  scripts sits 0.097..0.229 from its own whole; hand that back and it is 35.3% and 34.0%. The
  honest sentence was "between 121 and about 1675" — 268's direction survives, its sharpness does
  not. (0.086..0.167 and 26.6%/25.3% were 274's own figures, taken off groups cut in HASH order;
  275 cuts them in file order and they move. The command prints the current ones.)
  **275: BOTH ARE WITHDRAWN AS NUMBERS.** The bound divides by d(junk, real) and so puts real
  script at NOUGHT from the reference, where two halves of the maps' own sit 0.178 apart — and it
  reads NOUGHT on a group that is half real script by construction. `--the-ruler` measures both
  ends and checks the scale against mixtures of known share: outside reads 9.9% and 9.3% under the
  best-calibrated junk model, 9.3%-28.5% across four of them, each with a 22-25 point bar. What
  survives is the SIGN — outside reads at the junk end of a scale whose real end is 40-80% — and
  269's region-preserving floor, which shares no code with any of this
  homogeneity does NOT tell them apart either (274): at a common group size of 114 the maps' own
  score 0.156..0.703 and the reversal 0.231..0.502, overlapping. The own-quarters column that
  separates them cleanly is the GROUP SIZE, not the populations
so the reversed-image floor said 456 where the truth is about 6300, a FOURTEEN-FOLD gap, because
  reversing keeps every table (268). The region-preserving floor (269) puts it at 15%: of 46143
  aligned targets, 19.2% decode AS NAMED and 14.9-16.4% decode NUDGED by 4 to 4096 bytes — and
  split, the maps' own go 99.6% -> 51-70% and everything else 14.9% -> 12.0-13.8%. Two routes
  sharing no code agree that the maps lead to MOST of the script this cartridge has — "very nearly
  all" was the bound quoted with no band under it, corrected at 274
a pointer aimed FOUR BYTES into the middle of a real script still decodes to a proper end two
  thirds of the time — the reader resynchronises (269). That is why "reads as a script" was never
  the filter its name suggests, and 0x00 being a no-op with no arguments is why a run of zeros
  reaches whatever end follows it. 267's "6621 blocks no map leads to" is WITHDRAWN as a count
  of scripts: the maps lead to most of the script this cartridge has
the whole-image operand sweep CANNOT be run and now has no reason to be: compare's variable
  operand is 98% over the map scan and 27% over the outside half (267) because the outside half is
  noise (268). There is no body of scripts outside the maps for a third operand to hide in
2296 of the 2337 entries the maps lead to are named ALONE — a run of one aligned ROM-address word.
  2138 of the outside entries sit in runs of five or more, which is a pointer table (268)
1042 ledge squares — 0x38 on 39, 0x39 on 41, 0x3B on 962, 0x3A a name on nought. `--ledges` says
  954 for 0x3B and that is its INTERIOR: 8 sit on a map's outer ring, where a hop lands off the
  map and HopOnto refuses it, so all 8 are walls here (266)
the hop assignment is MEASURED and `--which-way` prints it (266). Each byte alone, everything else
  a wall: 0x3B down 211 maps / up 38 / east 34 / west 34; 0x39 east 36 / west 34; 0x38 any 34 —
  all seven reproduce to the digit. **0x38 is no longer an inference**: alone it stands beside 9
  of its 39 squares so no direction could differ, and with the other two at their measured values
  it stands beside 24 and WEST is the only direction that changes anything — 46790 squares against
  46568, at 212 maps either way. 0x39 east firms up the same way (212/46790 against 211/46655)
  and the criterion is REACH — by connectedness 0x3B down is the WORST of the four, stranding
  35328 of 46433. The comment said connected and measured reached; it says reached now
REACHING IS NOT RETURNING and until 265 only one of them was ever printed. `--the-way-back`:
  the floor stands on 35142 squares over 174 maps and 24029 of them CANNOT get back — 140 maps,
    137 stranded WHOLE, and the way into every one is EIGHTEEN LEDGE HOPS on 3.22 ROUTE 4
  with moves and through people it is 48 squares on 4 maps; surfing does not change that number
  three of the four are LIFT CABINS — 10.6, 1.58, 1.46 — which the walk enters and never leaves,
    because their exits are the runtime sentinel and the walker `continue`s past it
  the fourth is 2 squares behind a ledge on 3.7 FUCHSIA, and it is this milestone's decoy fixture
  1294 warps: 19 the sentinel, and of the other 1275 — 920 name THIS door back, 237 come back to
    the map by another door, 118 are ONE WAY. Control (the next door along): 219 / 233 / 823
  116 borders, 114 declared from both sides; the 2 are 3.50 and 3.51 both naming 3.14 THREE
    ISLAND upward, which names 3.49 downward — three maps claim to be south of it, it claims one
  9 maps have NO exit but the sentinel: 0.0-0.4 (the rooms above a centre; 0.1 and 0.4 each have
    19 doors in from 19 maps, one per town), 1.46, 1.58, 2.11 TRAINER TOWER, 10.6. The walk gets
    into 3; the other 6 are entered by a script rather than by standing on a square
object +3/+11/+22/+23 and trigger +5/+10/+11 are nought in EVERY record in the game — spare (260)
object +14 is unread on the 1199 non-trainers and is nought on 1197 of them; two are not (260)
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
0x406F: 20 maps want 1/2/3/5/6/7/8; the writers write 0, 3 AND 6 — 3 and 6 through
  `setvar 0x8004, N ; copyvar 0x406F, 0x8004`, which 229 never followed (corrected at 255)
the middle bucket per LIST (257, corrected 258): arrival 282 = 76 written/0 counted/0 saturated/
  192 copy/14 neither -> 27.0%; square 82 = 0/0/3/0/79 -> 0.0%. The square list gains NOTHING
the counter walk saturates: 0x4001/0x4002/0x4003 each reach 100 of the 100 values in 0..99, so
  255's three and 257's "all three of what the square list gained" were an artefact (258)
the verdict per list: arrival 144 written / 8 armed / 6 dead / 192 unread, of 350;
  square 103 / 72 / 11 / 42, of 228. The ARMED column is MODELLED and the output says so
71 of the square list's 82 want NOUGHT and are armed at the start — the bucket's name means the
  opposite of what it says for most of that list; 11 of 228 are ones nothing can produce
all 11 want 99 — a value in no setvar and no compare in the image, where every other value either
  list names is 0..8 (one 17); their 11 scripts at 11 addresses guard on 100 and set 100 (258)
142 of 228 square scripts WRITE their own variable at 85 addresses (ordinary); 11 GUARD on it at
  11; the arrival list's 3 guards are ONE address on 31.0 counted three times
equality stands: 3.42 runs SEVEN scripts off 0x405F at values 1..7, which any <= makes simultaneous
NEITHER list can support a count of dead conditions: 6 against 192, and 11 against 42
0x405F is copied from 0x4001 at 4 sites on 3.42, all opened by the map scan — 250's "42 can never
  fire" was disproved by 251's write-table fix and carried for six milestones (257)
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
ONE number is named both as a flag and as a variable in the map scan — 0x4001, floor 1.73 (244,
  re-run 264; the floor was 1.71 before 252 changed the population)
  243 said 27; 26 of them were 0x1A's SECOND WORD, which is a value unless it is a variable id
  0x1A arg2 names 149 numbers and 3 are ever written; every other reading operand is 75-100%
  the bands, READ not asserted: flags 0x0000+ 237n/347p and 0x4000+ 1n/4p; variables
    0x4000+ 77n/856p and 0x8000+ 16n/3428p, with 0x1A arg2's 145n/501p outside both (264)
  the whole-IMAGE version of the same question says 2117 / 14308 / 1333 — throw it away (264)
the 38 unnamed boundary sites ARE THE REVERSAL'S KIND ON A MODELLED CUT (273, withdrawn at 276,
  restored at 277, and 278 says the cut nothing can measure is what all three turned on — only 3 of
  the 38 lie inside the reference's span and at that size the two cuts are not even different). Their mix is 0.601 from the maps' own, and a 38-block group of
  real script reaches that far in **0 of 102** scattered samples against the reversal's sites'
  **19 of 109 (17.4%)** — rate bands 0.0%..0.0% against 8.0%..12.0%, which do not meet. In RUNS it
  is 6 of 102 against 36 of 109, and that is the file's regional structure, not the reading
  the ends at 38, SCATTERED: the maps' own SITES 0.132..0.225 against the REST (11 groups, 0/11) and
  0.119..0.208 against a whole that contains the group; the maps' own SCRIPTS 0.111..0.285 (102,
  0/102); the reversal's sites 0.441..0.743 (109, 19/109); the sites NUDGED +4/+16/+64
  0.308..0.380 / 0.283..0.392 / 0.338..0.435 (7 each, 0/7). In runs those are 0.278..0.451,
  0.257..0.417, 0.213..0.826, 0.423..0.896 and 0.301..0.496 — the command prints both
  0 of the 4 junk models have ends that CROSS; 3 put the 38 BEYOND the junk end, which is a broken
  model and not a strong answer — all three are the nudged site, which is real script read from a
  boundary that is not one. Only the reversal answers, at 0%..27.5%, AND THAT SHARE HAS NO
  CALIBRATION because the mixtures it would be calibrated against are built out of the nudged site
  a block of 11 real-SCRIPT groups holds none at or beyond 0.601 in 9 of 9, so the SITES' 0/11 and
  the SCRIPTS' rate never disagreed (276 thought they did, in runs)
  22 of the 38 are two or three commands long and three are runs of nop — 269's zero slide
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
* ~~What the twelve are for.~~ **CLOSED AT 247, and the answer is that there are none.** Twenty-one
  of the twenty-six the commands could not account for are read by a map header or a trigger; the
  five left are all loaded by compiled code. Every variable this cartridge writes is consulted by
  something. Both corrections were about what counts as a READER, not about where to look.
* ~~Does any OTHER reading miss the header read?~~ **ASKED AT 247** and the answer was yes twice
  over: the trigger record, and `--who-reads`, which was still printing "NOTHING IN THE FILE LOOKS
  AT IT" about `0x407C`. Both fixed. **And the FLAG side at 248**: 183 buried signs carry an
  INDEX, so the flag is a base plus it and nothing names it. What is left of that one:
  * **The base.** Unanswerable from the number line and the load count (3 gaps, 14883 candidates,
    889 loaded against a reversed 84). Settling it means reading the routine that handles a buried
    item, which means reading COMPILED CODE — a thing this project has never done and which is a
    decision rather than a milestone. 246's literal-pool test is as far as the data goes.
  * ~~The run never picks any of them up.~~ **MEASURED AT 249**: the widest walk stands on 182
    of the 183, on 78 of the 79 maps, and collects none — 101/122/122/182/182/182 across the six
    settings, printed by `--buried`. The one it never stands on is `1.62 (35,5)`, an ELIXIR.
    Making it collect them is a change to the RUN and a decision: 249 showed it moves no reach,
    so it would move only what the party ends with.
  * ~~Is anything only underground?~~ **ASKED AT 249.** 21 kinds are, which is below the base
    rate's 30.3, and **nothing that is asked for has no other source** — no wall has a shovel in
    front of it.
  * ~~The eight unused indices and the spare bit.~~ **BOTH READ AT 279, and both end in a wall.**
    The holes are only readable against the order the slots are handed out in, and it is NOT map
    order (12 of 79 maps hold their slots in more than one run — `3.42` in three) and NOT address
    order (40 of the 182 steps up the index go DOWN the file); no other sign kind claims them,
    because every other kind holds a pointer. The spare bit's six all name an item NO script names
    — 68 of 183, one chance in 440 — and 62 other records hold that property without setting the
    bit, so it is a thing the six have and not what the bit means. **What the bit DOES is engine
    behaviour.** The chase is what found the kind byte.
* ~~The trigger's other half.~~ **ASKED AT 250 and FINISHED AT 257.** 43 of the 228 name a
  variable NOTHING writes, all of them `0x405F` — and 250's "42 of those can never fire" is wrong:
  251 put `copyvar`'s destination in the write tables and four `copyvar 0x405F, 0x4001` sites on
  `3.42` fill it, so the verdict is DOES NOT KNOW. ~~The 82 waiting on a value nobody writes.~~
  **OPENED AT 257**: 255's one-hop correction is worth NOUGHT of them, a counter reaches 3, and
  **71 of the 82 want NOUGHT and are armed from the start** — the bucket's name means the opposite
  of what it says for most of that list. What is left of it:
  * ~~The eight the square list cannot produce and the six on the arrival list.~~ **READ AT 258.**
    The square list's are ELEVEN (the counter correction moved three into them) and all eleven are
    ONE IDIOM wanting 99. What is left of it:
    * **Whether the engine special-cases 99.** Compiled code, and the third milestone running to
      end at that wall. A decision, not a milestone.
    * **The arrival list's other four** — `0x400D == 17` on `2.10`, `0x4085 == 1` on `3.9`,
      `0x406E == 1` and `== 3` on `11.0`. No self-guard, no shared shape, four maps, unread.
    * **What the eleven scenes are.** Seven consecutive variables on `2.35` and four across
      `1.39`-`1.41`, in one idiom. Not read.
  * **The 42 in the error bar.** `--trace 0x4001` is the instrument: what `0x405F` gets is
    whatever `0x4001` held at the moment of the copy, which is a fact about a run.
  * **The MODELLED nought.** That a variable holds nought before anything writes it decides 72 of
    the square list's 228 and has never been read. Reading it means reading compiled code — the
    same wall `--buried`'s base hunt hit at 248.
* **The operand sweep's three unfinished halves** (252). `--operands` found two write operands in
  neither table; what it has NOT done:
  * ~~the mirror~~ **RUN AT 253**: corrected, it finds nothing new, so **both tables are
    complete**. Uncorrected it finds 27, which is why the correction is printed beside it.
  * **the whole image** — `--operands` asks the map scan, which is 0.6% of the file.
  * ~~`0x42` still has no name.~~ **READ AT 254: it leaves a SQUARE**, and its first operand is
    the column — off the maps' own widths and heights, with two negative controls that come back
    unnamed. What is left: **whose** square (226's shape, and eight places is thin for it),
    `0x42 arg2` (one compared place, names nothing), and a name in `ScriptCommands`.
* ~~Whether a dropped trigger hides a reader~~ **ANSWERED AT 259: NOUGHT.** `--dropped` prints
  what all four readers throw away — warps 0, triggers 0, signs 0, objects 9 — so 228 is 228 and
  the trigger list 247, 250, 257 and 258 rest on is complete. The nine objects are a SECOND KIND
  of record (clones), and after deciding on the kind byte instead of the square, nothing in any of
  the four lists is off the map at all. What is left of it:
  * **Whether warps and triggers have a kind byte too.** Signs do — the buried ones, 248. Nobody
    has asked of the other two.
  * **Whether `LoadedMap` should carry the clones.** They are readable rather than discarded now;
    drawing a person across a map join is a rendering question this project has not asked, and it
    is a DECISION.
* ~~`0x026C` and `0x0807`~~ **READ AT 256**: `0x026C` alone, toggled by three signs sharing one
  block, and three is odd. `0x0807` is not a cause. What is left: **what `0x026C` is FOR** (the
  block asks a yes-or-no then branches on `0x8004`, which each sign sets differently — a shared
  scene with three doors), and **whether the walk should read a sign twice at all**, since a
  player reads one and the walk reads all three, which is what makes the count odd.
* ~~`0x4001` is a flag in the run and a variable in the doors reading.~~ **CLOSED AT 243, and
  the follow-up CLOSED AT 244**: both readings are right, the cartridge holds `29 01 40` at
  `0x1656AA`, and it is the ONLY number used both ways — 26 of 243's 27 were a literal counted
  as a look. What is left: `0x4001`'s other two flag sites, and whether
  `EverywhereInTheImage.Reads` should stop counting `0x1A arg2` at all (244 marked the output
  rather than moving quoted numbers, and that decision is owed a re-run).
* **What 280 left.**
  * **The client does not know about the side.** `MapSign.MustBeReadFrom` is on the shared record so
    both halves have it, and nothing on the client asks. A rule enforced on one side of the split
    needs its counterpart on the other — unmeasured and unasked.
  * **Whether a BURIED sign has a side.** Kind `0x07`'s neighbours are open 120/147/142/127 of 183,
    which is no side on the face of it, and nobody has asked it properly.
* **What 279 left.**
  * ~~Whether the walk should obey the side.~~ **DONE AT 280** — the exported record carried the
    kind already (239), so it was the rule and not the plumbing. 0 maps, 0 flags, 2 signs.
  * **`0x02` is absent and would be north.** Inferred, and no cartridge evidence can settle it; it
    is deliberately NOT in `MapSign`'s table.
  * **What the spare bit DOES** is engine behaviour — the sixth wall of that kind.
* **What 278 left.** The cut is measured where it can be and marked MODELLED where it cannot. Owed:
  * **There is no route to measuring it for the 38** — it would need known real script OUTSIDE
    script-land, and there is none. The fifth wall of that kind (248's base, 257's starting nought,
    258's 99, 262's compiled code, this).
  * **2.5% is a fact about the MAP SCAN, not necessarily about the cartridge.** It says where the
    script this project finds lives. If any of 268's outside blocks were real script they would be
    script outside that stretch, which is the question itself — sharpened, not settled.
  * **`--the-ruler`'s cut is MODELLED too** and its footprint table says so on every run.
* **What 277 left.** Every band is cut both ways now and the reading takes the matching one. Owed:
  * ~~Which cut is right is a judgement per reading and NOTHING ENFORCES IT.~~ **MEASURED AT 278**
    where it can be, and reported as MODELLED where it cannot — which is the state the 38 are in.
  * ~~The rate has no error bar.~~ **DONE AT 277** — `RateBand`, and the two bands do not meet.
  * ~~Two REAL populations disagree.~~ **THEY NEVER DID** — 9 of 9 blocks of eleven real-script
    groups hold none at or beyond 0.601. 276 saw it in runs.
  * ~~The same critique lands on `--the-ruler`'s verdict.~~ **APPLIED AT 277** — its ladder is
    scattered and its mixture calibration went 22.0% to 10.5%. Its OVERLAPS/BELOW sentences still
    read off band extremes with the rate printed beside them.
  * **A junk model for the boundary reading that is actually junk.** The nudged site is not — it
    puts the 38 BEYOND its own junk end, which is a broken model. The reversal is, and lives in
    another image, so no mixture can be built with it, so the 0%..27.5% share has no calibration.
* **What 276 left.** `--flags` measures the 38 against ends taken at 38 and reports a rate.
* **What 275 left.** `--the-ruler` exists and 268's bound is withdrawn as a number. Owed:
  * ~~A band at the size actually being bounded (274).~~ **ANSWERED SIDEWAYS AT 275** — the size was
    never the problem. The bound puts real script at NOUGHT from the reference and reads nought on
    a half-real group at every size it can be run at.
  * **A junk model the calibration can choose.** Four nudge offsets calibrate within 3.5 points of
    each other and disagree about the answer by 19.3. More mixture shares, or a mixture ladder per
    model rather than one worst-miss number, might separate them. Might not — and if it does not,
    that is the finding.
  * **No band on the mixture rows themselves.** They are cut from ONE held-out half against ONE
    junk half: 4 groups at 25% and 2 at 75% is what the populations supply.
  * **`0.178` has no band either** — one number from one split, and the split is halves in file
    order. It is the thing every reading in that command is measured against.
  * **Whether `--operands-everywhere` should still print the old bound at all.** It prints it with a
    line saying it is superseded and naming what supersedes it. A DECISION, deliberately not made.
  * **Everything else that took a sampling band should be re-run in file order.** `--flags`' 38 were
    (verdict unmoved, numbers moved) and `--operands-everywhere` was. Grep for `SamplingBand`.
* **What 271 left.** The boundary's sixty are sorted (`--flags`, `WhatTheBoundaryIs`): 21 the
  opening, 1 code read as a `call`, 0 a literal's block, **38 that read as script and that
  NOTHING names**. Owed:
  * **The 38 are 5.6-fold EVIDENCE of being the reversal's kind, not a settled one** (273, read
    again at 276). Their command mix is 0.601 from
    the maps' own against a 38-block sampling band of 0.257..0.417, and 0.373 from the
    reversal's against its band of 0.362..0.754 (275, in file order). 276 withdrew the verdict as
    too strong — that band's top is a maximum over eleven groups — and **277 restored it**: cut to
    match the 38's own scatter, real script reaches 0.601 in 0 of 102 against the reversal's 19 of
    109, with rate bands that do not meet. **The sixty are fully accounted for: 21 + 1 + 38 +
    nothing left over.**
  * ~~Slack itself~~ **DONE AT 271**: window at nought, 4 of 3674 sites and 0 on the boundary.
  * ~~`0x0014` at `0x081C0D45`~~ **DONE AT 271, and it is THUMB code** — see trap 86.
  * ~~The 60 minus 21~~ **DONE AT 271** — 21 / 1 / 0 / 38, printed by `--flags`.
* **What 269 left.** The control exists now; applying it does not:
  * ~~Re-run every reading whose floor was the reversal AND which is about ADDRESSES.~~ **DONE AT
    270, and two of the three were not address-shaped**: the coin chain and the field-effect sweep
    do not move under rotation, so the reversal was always their control. The jumped-into test is
    the one that was, and it was on its floor.
  * ~~A nudge for the three-byte sweeps.~~ **DONE AT 272 for `Moves` and `Writes`**
    (`AnUnusedNumber`, printed under every count in `--in-the-image` and `--who-writes`): the
    sixteen nearest unused ids with the same high byte. `AsksWhoKnows` takes a bound rather than
    an id and has no nudge yet.
  * **The seam.** A rotation joins the end of the file to the beginning; four bytes of the control
    were never adjacent. Unmeasured.
* **What 268 left:**
  * **The 121.** The bound says at most that many outside blocks are real script; WHICH ones is a
    per-block question the mix cannot answer.
  * **`0x09`, `0x0F` and `0x21` are 30% of the maps' scripts and under 4% of everything else.** A
    per-block score against that profile is how the 121 would be found.
  * **`0xAD arg0`** is above half on both populations, named by neither table, and unsettled since
    253. 267's population cannot help.
* **What 266 left.** `--which-way` settled the ledge table; what is owed:
  * **Whether a ledge on a map's outer ring hops across the join.** Eight squares, all `0x3B`,
    all walls here. `WorldWalker` crosses borders; `MapData.HopOnto` does not know borders exist.
  * **The sweep starts at PALLET TOWN with no moves.** A byte whose ground is behind CUT rather
    than behind another ledge is exactly where `0x38` was, and that lever was not varied.
  * **`--ledges`' axis columns are interior counts too** — 950 of 954 in an east–west run is a
    share of the interior, and what the eight ring squares do has not been asked.
* **What 265 left.** `--the-way-back` printed the second column for the first time; what is owed:
  * **What the lifts are worth.** The walk gets into `10.6`, `1.58` and `1.46` and models each as
    a room with no exit. Joining every floor with a door into a lift is the same upper bound the
    boat already takes; the difference it makes to reach has never been measured. A lever, and
    MODELLED.
  * **The other six sentinel rooms** — `0.0`-`0.4` and `2.11` TRAINER TOWER — are entered by a
    script, so `--through-scripted-doors` is the setting that would walk into them. Not run.
  * **The two one-way borders.** `3.50` and `3.51` both name `3.14` THREE ISLAND upward and it
    names `3.49` downward. Neither is reached without the boat, so what a walker crossing north
    from either actually comes back onto is unmeasured.
  * **The floor table has six rows and one column.** 265 asked three settings; `--the-floor`'s own
    six have not been asked whether they can get back.
* **`9.6`'s puzzle** — fifteen doors, `0x8004` against `0x8008`. Read far enough to say what it
  is; it is NOT why the run cycles, whatever 239 said.
* **`3.57 sign (9,43)`** — the LEMONADE example that has been quoted in this prompt for
  milestones as something the run could not reach. It can now.

1. **`0x0AB` IS READ (232) and the block audit is DONE (231).** What is left of the audit: What is left of it: the three
   ~~numbers nothing prints~~ **PAID AT 263**: `146 trees and rocks` and `158 objects` are EXACT
   and printed by `--play` now; `62 gates hold 240 people` and `the ceiling is 45 of 437 byte
   positions` reproduce nothing and are WITHDRAWN. The ceiling in byte positions IS printed, per
   bucket — 17 of 359 at the widest, 10 of 344 at the floor — and 437 is `--routines`' count of
   every branching byte position in the file, a different denominator. The next cheap reads are **`0x194`'s nineteen doors** on TRAINER TOWER
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

# Milestone 247: the second copy, and the list goes to nought

246's last owed item was one sentence: *`--who-reads`, the flag work and `--in-the-image` all
enumerate commands, exactly as `--namespaces` did, and nothing has asked.*

Asking took one look at a record.

---

## A square fires when a variable holds a value, too

A map's third event list is the triggers — the squares that run a script when somebody walks onto
them. A trigger record carries `{x, y, variable, value, script}`. **It names a variable. It is a
read. There is no command anywhere in it.** The same shape as 246's arrival condition, on a
different list, missed by the same reasoning.

```
  and 61 number(s) are looked at without a command being involved at all:
    a map header, on arrival: 27 variable(s) at 350 place(s) on 61 map(s)
    a trigger, on the square: 42 variable(s) at 228 place(s) on 52 map(s)
```

Forty-two variables, and it is the bigger of the two.

## The deaf list: 26 → 19 → 5 → and the answer is nought

```
  5 of the 90 variable(s) the map scan WRITES are never looked at by anything this project can find
    26 by the commands alone, which is what 245 printed — 21 of those are read by something that
    is not a command, and by nothing else:
      0x400E — 8 x a trigger, on the square on 8 map(s) (2.1, 2.2, 2.3, 2.4, 2.5, ...)
      0x400F — 16 x a trigger, on the square on 8 map(s)
      0x4050 — 1 x a map header, on arrival on 1 map(s) and 2 x a trigger on 1 map(s)
      …
      0x407C — 19 x a map header, on arrival on 19 map(s)
      0x4088 — 3 x a trigger, on the square on 1 map(s) (1.114)
    3 are looked at somewhere the map scan never opened: 0x4010, 0x8001, 0x8002
    and 2 are looked at NOWHERE IN SIXTEEN MEGABYTES: 0x4026, 0x403E
```

And the five that are left were then put to 246's load test — **including the three in the
boundary bucket, which 246 did not do**, because "something in sixteen megabytes decodes as a
compare" is a weak filter and an instruction that loads a literal is not:

```
      0x4010 — 4 word(s) an instruction loads (15 that nothing loads or a script owns)  REVERSED: 1
      0x4026 — 2   REVERSED: 0        0x403E — 3   REVERSED: 0
      0x8001 — 8   REVERSED: 0        0x8002 — 1   REVERSED: 0
    so 5 of the 5 are held by compiled code and 0 are held by nothing in the file at all
    so NOTHING this cartridge writes goes unconsulted: every one of the 90 is read by a command,
    a map header, a trigger, or an instruction
```

**245's twelve is nought.** Every variable this cartridge writes is consulted by something, and
the two milestones it took to get there were both about what counts as a reader rather than about
where to look.

## `--who-reads` was still saying it

246 fixed `--namespaces` and left the other sweeps. This is what `--who-reads 0x407C` printed
afterwards, unchanged, about a variable nineteen maps consult:

> `NOTHING IN THE FILE LOOKS AT IT. Whatever is put in it is put there and never asked about — by
> any script, anywhere in sixteen megabytes.`

Two instruments, one sentence, one of them corrected and the other not. It now prints the header
and trigger counts, the compiled-code loads with their reversed floor, and the strong sentence
only when all four come back empty — with the base-relative case named as the thing that would
still not be seen. Its whole-image aggregate carries a line saying which KIND of reader the
population is blind to, beside the line saying how noisy it is.

## Both halves of the new rule are inert, and the output says so

```
    what a trigger's variable field holds, over all 228: 0x4000+ 228
    what a trigger's variable field holds, over the 228 with a script: 0x4000+ 228
```

**Every trigger in the game has a script, and every one names a variable in the story's own
band.** So `ScriptAddress != 0 && Variable != 0` fires on none of them, and both fixtures for it
are decoys — which is this project's stated alternative to deleting a guard nothing can fail. They
are kept because the rule is what makes the population defensible: without it a record that runs
nothing puts its variable field into a reader's list, and nothing about the count would look
wrong.

The distribution is computed over **every** trigger, including the ones the rule throws away. A
version reporting only what the rule kept would say `0x4000+ at all of them` by construction and
mean nothing, and there is a fixture and a break for exactly that.

## One name for the rule

Both kinds live in `ReadsThatAreNotCommands`, which takes records rather than a `MapLibrary` so a
fixture can reach it. 224's rule is that a shared wrong list is worse than five private ones, and
that is about a list nobody compares; this is the other half of it. **When the fault is that a
KIND of thing was never enumerated, five private enumerations means finding it five times** — and
the one that gets missed is the one nobody thinks of. A third kind added here reaches every caller
at once.

`WhenAMapRunsSomething.IsARead` was deleted in the same pass: it existed only to delegate, and a
delegation is a guard nothing can fail. Breaking it would have killed nought tests because every
caller and every fixture already names the real one.

## 246's own number, corrected

246 wrote *"0x4000 is loaded 56 times against a reversed 0, so that is not an empty worry"*.
**Fifty-six is the count without the load requirement** — the very filter that milestone spent its
length arguing for. The line was edited to use the load and the run was not repeated. The command
prints **1**.

Trap 16 in the milestone that quotes trap 16, and trap 21 with it: a sentence about what a number
is keyed on, written without going and looking at the key. The correction makes the finding
stronger and the hedge weaker, which is the direction that would have flattered the hedge — so it
is the direction nobody checks.

## The breaks, predicted before running

| break | predicted | went red |
|---|---|---|
| a trigger with no script counts as a read | 1 | `ATriggerThatRunsNothingIsNotARead` |
| a trigger with no variable counts as a read | 1 | `ATriggerWithNoVariableIsNotARead` |
| the trigger reads are not gathered at all | 2 | `ATriggerConditionLooksAtTheVariableItNames`, `BothKindsAreGatheredUnderTheirOwnNames` |
| the field distribution reports only what the rule keeps | 1 | `TheFieldDistributionCountsTheTriggersTheRuleThrowsAway` |

Four for four. 3082 → 3087 tests. **The six rows of the floor table did not move, and `--arrivals`
reports the same 350 / 69 / 27 it did before.**

---

## What is still owed

* **The hidden items.** 183 of this cartridge's 702 signs are the buried kind, and their record
  carries an item and a small id rather than a script. In this family of games that id is a flag
  taken from a BASE — which is the exact blind spot 246 printed and 247 has not closed. If it is
  true here, it is a fourth kind of non-command read, on the flag side, and it would bear on the
  110 gates the widest run never opens. Nothing in this project reads that field yet.
* **The trigger's other half.** `--arrivals` asks whether anything ever writes the VALUE a header
  condition wants — 28 of 69 want a value nobody writes. The same question has never been asked of
  a trigger's 228 conditions, and it is the same instrument pointed at a different list.
* **Whether a dropped trigger hides a reader.** `MapLinkExtractor` drops trigger records whose
  square is off the map before anything sees them, so their variable fields are never counted.
  That understates the readers, which is the safe direction, and nobody has printed how many.
* `0x4001`'s other two flag sites (244); whether `EverywhereInTheImage.Reads` should stop counting
  `0x1A arg2` (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven flags (241);
  `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words (238); the
  three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle; `3.57 sign (9,43)`.

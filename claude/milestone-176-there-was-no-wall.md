# Milestone 176 — There was no wall

Delivered as `claude-212.bundle` on `27423a2c4`. 2673 tests green from a clean clone at the
base. **Measured against the cartridge**, which milestone 175 could not be.

## The answer

`0x003E` is set unconditionally. Nothing gates it. The wall was in this project.

```
08161EB8  21 01 40 00 00                   compare 0x4001, 0
08161EBD  07 01 00 1F 16 08                CALLif eq -> 0x08161F00      <- returns
08161EC3  21 01 40 01 00                   compare 0x4001, 1
08161EC8  07 01 12 1F 16 08                CALLif eq -> 0x08161F12      <- returns
08161ECE  16 0F 80 03 00                   setvar 0x800F, 3
08161ED3  5C 03 5D 01 00 00 AB 71 17 08    trainerbattle kind 3, trainer 349
08161EDD  0F 00 C2 71 17 08                loadpointer
08161EE3  09 04                            callstd 4
08161EE5  68                               closemsg
08161EE6  97 01                            0x97
08161EE8  53 03 00                         hideobject 3
08161EEB  53 04 00                         hideobject 4
08161EEE  53 06 00                         hideobject 6
08161EF1  97 00                            0x97
08161EF3  16 60 40 01 00                   setvar 0x4060, 1
08161EF8  29 3E 00                         setflag 0x003E
08161EFB  2A 3F 00                         clearflag 0x003F
08161EFE  6B 02                            release, end

08161F00  applymovement(3, …), applymovement(0xFF, …), wait, RETURN
08161F12  applymovement(3, …), wait, RETURN
```

Both `0x4001` blocks are **conditional calls**, and both callees `return`. They are two little
walking animations picked by which square you stepped on. Everything after them — the fight,
the three `hideobject`s, and both flags, three bytes apart — runs with nothing in the way.

So: `0x003E` and `0x003F` are one scene, as predicted, and that scene is beating GIOVANNI in
SILPH CO. Which is what the game does.

## The fault

`WalkItCouldTake` broke out of the block at the first conditional branch the script had
already decided to take:

```csharp
if (certain && takesIt) break;
```

For a `goto` that is right — it never comes back. For a **`call`** it throws away everything
after the call, which here was the entire scene. Milestone 173 corrected `Asks` for exactly
this (*"a `call` comes back… only the called block is the difference"*) and did not correct
this walk, so the two halves of one tool disagreed a second time — and a second time the
stricter one was believed, because strictness sounds like rigour.

**That is trap #2 from the roadmap, hit again by the session that wrote trap #2 down.** Knowing
about it in advance did not help, for the fourth milestone running.

## What moved

| | 174 | now |
|---|---|---|
| flags on an arm a run could take | 231 | **236** |
| of those, gating something | 74 | **77** |
| the code boundary | 248 | **245** |
| people who stand somewhere for ever | 397 | **388** |
| **of them in a doorway — the wall list** | **13** | **9** |
| people who never arrive | 53 | **46** |
| flags that look moved and are not | 10 | **5** |

Every one of those is milestone 174's "shape of a stricter reading, in the direction it should
move" — moving back. **The stricter reading was the bug, and its numbers were presented as
evidence that it was working.** The wall list is nine people behind five flags and SAFFRON is
not on it.

## What the new instrument actually did

`--in-the-image 0x003E,0x003F`, run against the cartridge:

```
0x003E — 1 site(s) in the file, 1 of which read as script, 1 of which the map scan opened
  0x161EF8  setflag  reads as script  the map scan opened this
```

**One site in sixteen megabytes, and the map scan already had it.** The hypothesis milestone
175 was built on — that the scene was hiding in script no map points at — is dead, and it took
one run to kill it. That is the instrument working: it was built to be able to come back empty
and it came back empty.

And then it did not answer the question, because of this:

> *"the climb runs only on sites the map scan never opened, because a site it opened is already
> answered by `--flags`"*

The one site that mattered was opened. **"Already answered" was doing the work in that sentence,
and it was not answered.** A filter whose job is to keep the output readable must never be the
thing that decides which question gets asked. The climb now runs on every site, says which
script opened each one, and prints the aligned words around a literal so a table of script
pointers reads as a table rather than as one address.

## The control, and the number it killed

The reversal control earned itself in one run. The raw sweep finds **3762** sites; the same
sweep on the image reversed finds **3675**. The "reads as script" filter is, at this scale,
almost pure noise, and no amount of confidence would have shown that.

The promoted filter survives it: a site something jumps into is **15.9%** of the unopened sites
here against **1.3%** in the reversal, twelve times the floor. Both figures are now printed in
the same unit — the first run put a count of *flags* beside a count of *sites* and invited them
to be compared, which is a category error dressed as an error bar.

## And the sentence that cost two sessions

```
set by 1.57 SILPH CO. trigger (5,15) — IT RAN THIS SCRIPT AND THE FLAG IS STILL UNSET
  — it ran to the end, so the setflag is on an ordinary branch it had no reason to take
```

There is no such branch. The run had stopped at GIOVANNI and lost. `WhyItStopped` had three
cases — a yes-or-no, a routine, and a fallback — and the fallback *names a cause*. A fallback
that names a cause is worse than one that says nothing, because it is actionable and wrong. It
now reads:

```
IT STOPPED AT A FIGHT it did not win (trainer(s) 349) — everything after the fight is
unreached, and the setflag may be sitting there unconditionally
```

## The guards

**Eight breaks, eight caught**, and two of them were guards that had come back green earlier in
the same session and needed decoys before they could bite:

* `Opened` letting a later script take ownership of a byte — needed a second map script
  starting *inside* the first one's block, because with one script first-writer-wins and
  last-writer-wins give the same answer.
* the control indexing the real image instead of the reversal — needed an image that jumps to
  an address the reversal has a scene at, *with the opcode planted where a mismatched index
  would read it*. Without that the mismatch produces garbage that happens to read as "not a
  jump", so the break under-reports the noise floor silently, which is the failure that
  matters.

The new ones: a conditional call stopping the walk again; a taken conditional goto no longer
ending it (the decoy — the fixture holds both shapes, differing only in whether the branch
comes back); the called block credited with nothing; a stopped fight not remembered.

## The cartridge was read directly

For the first time this thread, the image was staged into the session rather than measured by
posting bundles back and forth. It stayed out of the repository, out of every commit and out of
the bundle — the rule that matters is intact — and the loop went from one measurement per round
trip to eleven in one turn. **The `break` was found by disassembling forty bytes by hand.** No
instrument in this project would have found it, because every instrument in this project was
built on the walk that had it.

## What is next

* **SAFFRON is a strength problem now, not a code-boundary problem.** The four doors open when
  GIOVANNI is beaten; the playthrough reaches him with six at level 25 and loses. Whether that
  is the floor being honest or the party model being weak is its own measurement.
* **The other five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — nine people.
  Every one should be re-read now that the walk follows conditional calls.
* **The 46 who never arrive**, `0x009D`'s nineteen first. `--in-the-image 0x009D` has never run.
* **The 5 that look moved and are not**, down from 10. A short list and now a believable one.
* **The 20 flags with an entry point nothing opens.** Twelve times the noise floor, so the list
  is real; not one of them has been climbed.

## Still open, unchanged

Held items; signs never run; `--say-yes` costing party members; the nine `ARRIVED ON AN
ISLAND`s; eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped
saves; money modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the
no-bag control; `MapScripts` with no coverage at all; milestone docs for `StoryClosure`,
`Autoplayer` and `SpecialContracts`; sound; and whether `Reachable` should honour a trigger's
own condition.

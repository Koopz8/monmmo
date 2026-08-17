# Milestone 184 — The other half of the story's memory

Delivered as `claude-220.bundle` on the tip of `219`. 2708 tests green from a clean clone at
the base. Measured against the cartridge.

## The instrument

`--who-writes 0xNNNN` — every place in the whole file that puts a number into one of the
story's own variables, whether or not any map leads there, with the same climb `--in-the-image`
uses.

**A gate is a flag or it is a variable, and only one of them could be hunted through the image.**
`--in-the-image` has existed since milestone 175 and its mirror never did, so "who puts a two in
`0x4055`" — the question standing between this project and the only creature in the game a
player chooses — could only be answered by reading bytes by eye.

Pointed at it, one run:

```
0x4055 — 12 site(s) in the file, 10 of which read as script, 9 of which the map scan opened
  = 1: 0x16569A (3.0 trigger (12,1))
  = 2: 0x1692A9 (4.3 on arrival (0x4055 == 1))
  = 3: 0x169D4C (4.3 person 5)
  = 4: 0x1694FA (4.3 trigger (5,8))
  ... 5, 6, 7, 8, 9
```

The opening of the game, in order: the trigger north of PALLET TOWN puts **1** in it, the lab's
arrival script reads that 1 and puts in **2**, and **2** is what makes the three balls hand
something over.

## And why no run has ever held a starter

The run rebuilt **every variable from nothing at every script.** Flags crossed from one script
to the next. The bag crossed. The trainers it had beaten started crossing yesterday. Numbers
never did — so the first step of PALLET TOWN was undone before the second one ran.

That is fixed. The reader remembers what a scene leaves in the story's variables, and hands it
to the next.

## Where the scratch pads stop, read rather than assumed

Persisting *every* variable would be wrong: milestone 173 established that `0x4001` is a scratch
pad by counting — 285 scripts write it — so a comparison on one is a switch a script computes
and reads back. The question is where the pads stop, and the answer is in the distribution:

```
0x4000s: 12 variable(s), busiest x168, quietest x1
0x4010s:  5 variable(s), busiest x7
0x4020s:  6 variable(s), busiest x2
0x4030s:  7 variable(s), busiest x21
0x4040s:  8 variable(s), busiest x10
0x4050s: 15 variable(s), busiest x10
```

**There is a cliff, and it is at `0x4010`.** The twelve below it are written up to a hundred and
sixty-eight times each; every band above tops out at twenty-one and mostly under ten.

That there is somewhere to cut is a measurement. Where exactly to cut is a decision — MODELLED —
and the instrument that found it prints the bands, so the next reader can disagree with the
number rather than with a claim.

## What moved, and what did not

283 → **286 flags**. The starter still does not arrive, and now for a reason worth having:

**the run runs every script on a map regardless of that script's own condition**, so `0x4055`
ratchets from 1 to 9 in a single pass through the lab, and by the time the three balls read it
they see `>= 3` — *you already have one*. The counter is remembered correctly and consumed in
the wrong order.

That is the open question the roadmap has carried for four milestones as *whether `Reachable`
should honour a trigger's own condition*, and it now has a concrete case attached instead of
being a general worry.

## The guards

**Nine breaks, seven caught, and two green that were mine.**

Caught: nothing remembered between scripts; the scratch pads remembered too; the boundary moved
above the counter; the remembered values written as nought; `Writes` looking only for `setvar`;
and the two from the shore work.

Green, both because **I built an instrument and did not guard it**: `Writes` and
`EveryVariableWritten` had no tests at all when they shipped their first finding. Four tests
and a decoy later they bite — the decoy being a writer opcode sitting in the middle of
something, so that "drops what is not script" has a case to fail on.

Writing an instrument, using it to find something real, and only then noticing it is unguarded
is the same order of operations this session has criticised five times in other people's code.

## And one flaky test, recorded rather than buried

`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother` failed once, on a run that took
fifty-five seconds instead of the usual twenty-eight, and passed on every re-run since —
including two clean full suites. It is timing-dependent, which makes it a guard that can lie in
both directions. Not chased here; written down so the next person who sees it red knows it has
been seen.

## What is next

* **Whether the run should honour a script's own condition.** It now has a case: `0x4055`
  ratcheting to 9 before the balls read it. That is the starter, and it is the last thing
  between this run and the opening of the game.
* **Which move crosses water, READ rather than assumed** — still the largest number on the list.
  `--surf` is a lever standing in for it.
* **The 42 doors never reached** at 390 of 425.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`**, and the four stops no width reads on from.
* **The five wall flags**, and the ~28 hand-rolled map walks left in `Program.cs`.

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound.

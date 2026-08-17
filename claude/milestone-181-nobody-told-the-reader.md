# Milestone 181 — Nobody ever told the reader a trainer had been beaten

Delivered as `claude-217.bundle` on the tip of `216`. 2692 tests green from a clean clone at
the base. Measured against the cartridge.

## SAFFRON is open

Not in the static reading this time — in the playthrough. The three SAFFRON doors are off the
blocked list, `0x003E` is set, and the run walks through.

```
--play --say-yes      211 -> 215 maps
                      176 -> 195 flags
                       25 -> 31  field moves
                        2 at 49  ->  3 at 59 in the party
```

## Three faults, all the same shape

### 1. The reader was never told who had been beaten

A `trainerbattle` is its own conditional. Beaten, the fight does nothing and the script carries
straight on into whatever the victory was for — milestone 73 established that, and
`ScriptRunner` implements it exactly.

The playthrough built a `ScriptState` from the flags, the bag and the injected variables, and
**never once called `MarkBeaten`.** So `HasBeaten` was false at every site on every pass, and
every script containing a fight stopped at the fight *forever*, however many the run won.

That is SILPH CO.'s `setflag 0x003E`, eleven commands past GIOVANNI. Two sessions were spent on
that flag, and milestone 176 proved it was set unconditionally — while the run that could have
demonstrated it was structurally unable to get past the fight in front of it.

The run knew. The reader did not. Nothing connected them.

### 2. A trainer was marked fought before the fight

```csharp
if (did.Fights is not { } trainerId || !fought.Add(trainerId)) continue;
```

`Add` returns false the second time, so a trainer met once was never met again — **including
one it lost to.** The run met GIOVANNI on its first pass with whatever it had, lost, and never
went back, while every later pass doubled the party's level. A player who loses wakes up in a
centre and walks in again; the healing two lines below was already modelling that, one step too
late.

**A trainer beaten stays beaten. A trainer lost to does not.**

And losses are counted by trainer now, with the attempts beside them:
`52 lost to (103 attempts — it goes back every pass)`. Counting the attempts makes a party
closing the gap look like one falling further behind.

### 3. The continuation carried the flags and not the variables

PALLET TOWN's three balls each write which species they are into `0x4002` and then ask whether
you want it. The `givemon` on the far side of that question reads `0x4002` back. The
`--say-yes` continuation set the flags on the carried-over state and left the variables behind,
so the species came back as nought and `givemon` of nought hands over nothing.

**The starter is the only creature in this game a player chooses**, and no run this project has
ever printed had one. Fixing the carry recovered a different gift immediately — HITMONLEE, the
Fighting Dojo's choice, behind the same shape.

## The lever that had never been used

`--var 0xNNNN=N` was built in milestone 173 and described as *new and untried on anything*.
This is what it is for: the starter is behind `0x4055 == 2`, a variable, not a flag.

```
--play --say-yes --var 0x4055=2    party: #131 at 49, #3 at 46, #9 at 46, #133, #106, #9
```

VENUSAUR and two BLASTOISE — it takes all three balls, which is a ceiling and says so. Left as
an experiment rather than adopted: nothing on the cartridge says a run holds that number, and
finding who writes `0x4055` is the honest version of this.

`--script-run` now takes `--var`, `--answer` and `--say-yes` too. It is the one tool that shows
a script line by line, and it could not be aimed at either question — a lever that exists on
one instrument and not on the one with the detail is a lever nobody can aim.

## The order the fixes came in, and what each was worth

| | maps | won / lost |
|---|---|---|
| `--say-yes`, at the start of this session | 211 | 123 / 97 |
| \+ experience carried across a fight (179) | 211 | 170 / 50 |
| \+ variables carried across a question | 211 | 171 / 49 |
| \+ a lost fight retried | 211 | 275 / 70 |
| \+ **the reader told who was beaten** | **215** | **281 / 52** |

Three fixes in a row moved nothing at all. It would have been easy to stop after any of them
and conclude the frontier was elsewhere. The fourth was the one, and it was invisible until the
first three were out of the way — a party at level 25 loses to GIOVANNI whether or not the
script would carry on afterwards.

## The guards

**Seven breaks, five caught, two green** — and the two are the same structural gap, found for
the fifth time.

Caught: a trainer marked fought before the fight; a beaten trainer fought again; losses counted
by attempt; the reader never told who had been beaten (via the shared set's contract in
`Autoplayer`); a beaten trainer's win not recorded.

Green, both in `Program.cs`: **the wiring that tells the reader**, and **the continuation that
carries the variables.** Two of this milestone's three fixes live in a file with no tests and no
fixture that can hold a map library. Breaking either fails nothing.

That is now the largest single piece of debt in this project and it is written at the top of
the roadmap. It is not attempted here: the right move is a session that moves the playthrough's
script reader into the library where a fixture can reach it, not a hasty refactor at the end of
a long one.

## What is next

* **Move the playthrough's reader into the library.** Five instances. Two live fixes are
  currently unguarded.
* **215 of 425, and CERULEAN CAVE is the frontier now** — `0x005C`, set by ONE ISLAND person 3,
  a map on the ferry thread.
* **Who writes `0x4055`.** The starter is behind it and `--var` is a stand-in.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`** — the unknown commands ranked by what is behind them.
* **The five wall flags**, and **the four stops no width reads on from.**

## Still open, unchanged

Held items; signs never run; the nine `ARRIVED ON AN ISLAND`s; eleven maps with no way in;
shortest-chain ways in; `Bag.PocketCapacity` in shipped saves; money modelled;
`SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the no-bag control;
`MapScripts` with no coverage at all; milestone docs for `StoryClosure`, `Autoplayer` and
`SpecialContracts`; sound; and whether `Reachable` should honour a trigger's own condition.

# Milestone 185 — A run is a fixpoint, and a counter is what a fixpoint cannot hold

Delivered as `claude-221.bundle` on the tip of `220`. 2711 tests green from a clean clone at
the base. Measured against the cartridge.

## The lever

`--in-order` runs a trigger or an arrival script **only when its own condition is met**. Every
one of them carries a variable and a value, and this walk has always run them regardless.

```
--play --say-yes              215 of 425 maps, 195 flags
--play --say-yes --in-order   215 of 425 maps, 193 flags
```

Without it the run is a **ceiling** in that respect — it takes arms of the story no single
playthrough could take in one pass. With it, a floor. A lever rather than a decision, the same
shape as `--boat` and `--surf`, because neither is the truth on its own.

That closes the question the roadmap has carried since milestone 173 as *whether `Reachable`
should honour a trigger's own condition*. It is answered by being able to ask it both ways.

## And the run prints what it remembers

A run has always reported how many flags it set and has never printed a single one of the
numbers. They are the same kind of fact — PALLET TOWN's whole opening is a counter — and
without them there is no way to tell *the run never reached the scene* from *the run reached it
and the counter was on the wrong number*.

```
the story's memory: 0x4050=3, 0x4051=2, 0x4052=1, 0x4053=0, 0x4054=2, 0x4055=5, ... +24 more
```

## Which says why the starter still does not arrive — and it is not the lever

`0x4055` **does** advance. It reaches five. It is never **two at the moment the three balls read
it**, and two is the only number that makes them hand anything over.

`--who-writes 0x4055` says who moves it, and the order is the problem:

* the lab's OAK writes **6** and **8** into it, and he is **person 4**;
* the three balls are **people 5, 6 and 7**;
* a pass talks to everybody on a map in map order.

So the scene that advances the story runs before the scene it is supposed to follow, on every
pass, for ever. Honouring conditions does not help, because the balls are people and people have
no condition to honour.

**This is not a bug in the lever.** A run of this kind is a *fixpoint over passes*: it keeps
talking to everybody until a pass opens nothing new. A story counter is precisely the thing a
fixpoint cannot model, because the ordering *is* the information — and a fixpoint has no order,
only convergence.

Naming that is worth more than another lever. Reaching the starter means an ordered playthrough,
which is a different instrument from this one, and it should be built as one rather than bolted
on.

## A clause that could not change an answer

`Fires` was written as `!inOrder || variable == 0 || remembered[variable] == value`. Broken on
purpose, the `variable == 0` clause came back **green** — and it turned out not to be an
unguarded rule but a dead one: an unconditional entry is `(0, 0)`, an unwritten variable holds
nought, so it passes the comparison already.

Checked against the image rather than reasoned about: `--play` prints the same 215 maps and 193
flags with and without it. Removed. **A clause that cannot change an answer looks like a rule
and is not one**, and this project's own standard for a guard nothing can fail applies just as
well to a condition nothing can fail.

## The guards

Three breaks on the lever, two caught — the lever ignored, and the lever turned into an off
switch, which is the half that matters: a condition that never passes would make every scene in
the game behind a counter unreachable for ever, and it would look exactly like a stricter floor.

The third was the dead clause above.

## What is next

* **An ordered playthrough.** The fixpoint has gone as far as a fixpoint goes: 215 of 425 on the
  floor, 390 with the ferry and the sea. The starter, and everything shaped like it, needs a run
  that walks in sequence rather than converging. That is a new instrument and the roadmap's
  biggest remaining piece.
* **Which move crosses water, READ rather than assumed** — still the largest single number, and
  it turns 390 from a ceiling into a floor.
* **The 42 doors never reached** at 390 of 425.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`**, and the four stops no width reads on from.
* **The five wall flags**, and the ~28 hand-rolled map walks left in `Program.cs`.

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound. And
`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`, which is timing-dependent.

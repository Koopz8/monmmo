# Milestone 183 — The sea was the same silence as a wall

Delivered as `claude-219.bundle` on the tip of `218`. 2701 tests green from a clean clone at
the base. Measured against the cartridge.

## The number

```
--play --say-yes                  215 of 425 maps
--play --say-yes --boat           306
--play --say-yes --boat --surf    390
```

390 of 425, with a party at 75 and 41 field moves. `--boat` and `--surf` are both **modelled**
and both make it a ceiling.

## What was actually wrong

`WorldWalker` has taken a `surfing` flag since it was written, and `GameWorld` — the real
server — has used it for milestones. **The playthrough never passed it.** So every water square
was dropped as unwalkable alongside every wall, and *there is nothing there* and *there is a sea
there and this walk was told not to cross it* have been the same silence in every number this
project has published.

The frontier said `move 249: 20 squares`. It read as the whole of what was in the way. On the
same run the sea is **1245 squares across 35 maps**.

`Reach.Shore` counts what a walk turns back from that is water — whether or not it is swimming,
so the number means the same thing either way: how much of the world is on the far side. On a
run with `--surf` it is empty, which is the count doing its job.

**Not crossed on a guess.** `--surf` is a lever like `--boat`, not a decision that water is
passable. Which move actually crosses water is something to read off the image, and a walk that
started swimming because it seemed obvious would have opened half the Sevii islands and been
unable to say why.

## The correction I nearly shipped

The first version of this said the walker had *no notion of water at all — the word does not
appear in it*. That was true of `Autoplayer.cs`, which is where I had grepped, and false of
`WorldWalker`, which has had the parameter all along. It was caught by reading the parameter
list before writing this file rather than after.

Two milestones ago the same class of overclaim would have gone out as a finding. The difference
is one grep in the right file, and the reason to write it down is that the wrong version was
more interesting.

## And a guard that took three attempts

*The playthrough hands the lever to the walk* was broken on purpose and came back green three
times running:

1. Asserting on `Attempt.Shore` alone. Two different calls into the walker take this lever, and
   `Shore` is filled in by the second — so breaking the first changed nothing.
2. Adding a door across the water and asserting on `Reached`. Still green: **`Reached` also
   comes from the closing walk**, not from the pass loop. Worth knowing on its own — the reach
   a run reports is the final walk's, which can exceed the ground it actually walked and ran
   scripts on.
3. Putting somebody on the far side with a flag to set, and asserting the flag. That is the
   only thing the pass-loop walk produces that nothing else does, and it catches the break.

Three greens on one rule, each for a different reason. The guard is the third one.

## Also in this bundle

`--play` now prints its shut doors **counted by reason** before listing them. The list stops at
twenty and the count is sixty-four, so the shape of the frontier has always been whatever the
first twenty happened to be. They are three different jobs:

```
      42  never reached the door
      17  ARRIVED ON AN ISLAND — it never walked this map at all
```

Zero *somebody is standing in the way*. That whole category — the one this session opened on —
is gone.

## What is next

* **Which move crosses water, read rather than assumed.** That turns `--surf` from a lever into
  a fact, and 390 from a ceiling into a floor.
* **The 42 doors never reached**, now that they are the majority and can be seen as such.
* **Who writes `0x4055`** — the starter is still behind it, with `--var` as a stand-in.
* **`0x3F`, `0xE6`, `0xC0`, `0xA7`**, and the four stops no width reads on from.
* **The five wall flags**, and the ~28 hand-rolled map walks still in `Program.cs`.

## Still open, unchanged

Held items; signs never run; the nine `ARRIVED ON AN ISLAND`s — now seventeen and counted;
eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped saves; money
modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the no-bag control;
`MapScripts` with no coverage at all; milestone docs for `StoryClosure`, `Autoplayer` and
`SpecialContracts`; sound; and whether `Reachable` should honour a trigger's own condition.

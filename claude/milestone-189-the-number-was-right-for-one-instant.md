# Milestone 189 — The number was right for one instant, and nobody was looking

Delivered as `claude-225.bundle` on the tip of `224`. 2728 tests green from a clean clone at
the base. Measured against the cartridge.

## The run has a starter

```
--play --say-yes --in-order   215 of 425 maps, 193 flags, party of FOUR
  the party: #131 at 59, #3 at 59, #133 at 59, #106 at 59
```

`#3` is the fourth, and it is the only creature in this game a player chooses. No run this
project has ever printed has held one. It arrives on the **floor** — the strictest run there
is — which is the strongest place it could have turned up.

## One line of ordering, never chosen

`Reachable` yielded every **person** on a map, then the triggers, then the map's **arrival
script**. That is the order the three `foreach` loops happen to sit in. Nothing chose it and
nothing had ever asked.

It is not a modelling decision and it has no other reading. An arrival script is what runs when
you arrive; **nobody has ever talked to somebody on a map they had not yet arrived on.** The
other way round is not a stricter run or a looser one — it is an order the cartridge cannot
produce.

Moved to the front, it is three lines. The whole opening of the game is behind them.

## And the cause written down for three milestones was the opposite of the real one

The roadmap has said since milestone 184 that PALLET TOWN's counter **ratchets past two before
the balls read it**, so they answer "you already have one". That went into `Autoplayer`'s own
comments, into 184, into 185, and into the next-session prompt.

Traced through an actual run:

```
pass 1  3.0            0x4055 <- 1
pass 1  4.3 person 5   0x4055 ? 3 (held 1)
pass 1  4.3 person 5   0x4055 ? 2 (held 1)
pass 1  4.3 person 6   0x4055 ? 3 (held 1)
...
pass 1  4.3            0x4055 <- 2
pass 1  5.3            0x4055 <- 5
```

**The balls read ONE.** Every pass. For seven passes. The counter was too *low*, and the two
landed immediately after the last of the three had been asked — then the next map moved it to
five, and on every later pass they read five and answered "you already have one", which is the
symptom the wrong cause was inferred from.

So the number was **correct for one instant**, between the lab's own script and the next map,
with nobody looking at it. Every instrument this project has could print the five it ended on
and none of them could say the balls never saw a two.

## Why the wrong cause was so convincing

It came off `--who-writes`, and `--who-writes` is right. It answers *where in the image is this
variable written*, statically, following **every arm of every branch** — which it must, since
choosing an arm needs a save. The lab's OAK is a dispatch table of ten `compare 0x4055, N` and
ten `goto`s, so the static list names him as a writer of six and of eight, truthfully.

**A run takes one arm.** Talked to with one in the counter, OAK falls through all ten compares
to "Now, choose one of the three POKé BALLS" and writes nothing at all.

Two instruments, both correct, answering questions one word apart, and the answer to the wrong
one is a complete and plausible story. That is trap 1 in the next-session prompt — *the answer
is often in a part of the file the scan does not open* — arriving from the other direction: the
scan opened **more** than the run does, and reading a cause off it was reading it off the wrong
instrument.

## The instrument: `--trace 0xNNNN`

Every look at and change to one of the story's variables, in the order the run did them, with
where and when:

```
pass 1  4.3 person 5  0x08169BAB  0x4055 ? 2 (held 2)
pass 1  4.3 person 5  0x08169BAB  0x4055 <- 3 (was 2)
```

That is the ball reading a two and taking it. It is not a lever: it changes nothing the run
does.

**A read is as much a fact as a write, and only writes have ever been recorded.**
`VariablesWritten` is a dictionary of final values — no order, and no reads at all — so "the
counter ended on five" and "the ball was looking at five" have been one sentence in this
project's output for as long as it has had one. They are the two different findings this whole
milestone is about.

Bounded at 4096 touches, and the overflow is **counted and printed**. A silent cap reads as
"that is all that happened", which is the failure this project has spent a session finding in
its own numbers.

## The guards

**Not one of 2721 tests noticed when the order changed.** It was unguarded in both directions.

Seven now, and four breaks all caught: the order put back exactly as it was (two fail — the
starter, and the write-before-read); the runner not recording reads; the runner recording a
write *after* it happened, so "was" is the new value; and the overflow dropped quietly.

Four run against a stand-in, which guards the walk's plumbing; two run bytes through
`ScriptRunner`, which guards the recording. That split was deliberate after the first break
caught the wrong three tests for the right reason.

## What is next

* **The ordered playthrough is still worth building, and it is now a smaller job.** The fixpoint
  reaches the starter with `--in-order`; what it still cannot do is refuse to re-run a scene it
  has already played. The trigger north of PALLET TOWN resets `0x4055` to one at the top of
  every pass without the lever, which is a whole story re-opening every pass.
* **Which move crosses water, READ rather than assumed** — still the largest single number, and
  it turns 390 of 425 from a ceiling into a floor.
* **`--trace` on the other counters.** `0x4050`, `0x4052`, `0x4057` and `0x4060` all gate scenes
  and none has ever been looked at in order.
* **The 53 stops**, the four that no width reads on from, the five wall flags, and the ~28
  hand-rolled map walks in `Program.cs`.

## Where the numbers stand

```
--play --say-yes                     215 of 425 maps, 193 flags, party of 3
--play --say-yes --in-order          215 / 193, party of FOUR — the starter
--play --say-yes --boat --surf       390 / 284, party of 3 at 75
--play --say-yes --boat --surf --in-order   390 / 284, party of FOUR at 75
```

Map counts and flag counts did not move. The fourth creature did.

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound; and
`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`, which is timing-dependent.

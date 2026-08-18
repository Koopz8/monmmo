# Milestone 239: the run walked a world with no signs in it

Eight of the nine milestones before this one end with the same sentence: **Nothing the run does
changed.** All of it was the reading. So the question was which part of the run could not move,
and one answer was sitting in the owed list as a line nobody had checked.

---

## "The playthrough never runs signs" was not a choice

```
  MapData:  Id Name Width Height Collision Behaviours Music Encounters
            Connections Warps Doors Ferry Objects Triggers OnEntry
```

**No signs.** `MapSign` has existed since the map work — it knows the cartridge's own kind tag and
which 183 of the 702 are hidden items rather than scripts — and the map scan has read all 519
scripted ones for as long as it has known five kinds. The record the walk and the server both go
over never carried them, so there was nothing for the playthrough to skip.

That is **224's fault standing in the other half of the project**. 224 found five copies of "every
script on a map" disagreeing, unified them onto the one that knew the most, and wrote down *check
the enumerator before the count*. Nothing then compared that list with the one the RUN walks.

519 sign scripts, at 360 addresses, on 143 maps.

## What it costs

```
                                            BEFORE                    AFTER
  --play                                    183 / 153 in 6, 11/103    183 / 160 in 6, 11/104
  --play --say-yes                          243 / 231 in 5, 10/155    243 / 234 in 6, 10/155
  --play --say-yes --in-order               243 / 233 in 5,  0/152    243 / 236 in 6,  0/152
  --play --say-yes --boat                   381 / 293 in 6, 11/204    381 / 295 in 7, 11/204
  --play --say-yes --boat --in-order        381 / 294 in 6,  0/200    381 / 296 in 7,  0/200
  --play --say-yes --boat --surf --in-order 381 / 292 in 4,  0/200    381 / 295 in 5,  0/200
```

**The map counts do not move at all** — not one square of this game is reached by reading a sign.
The flags do: seven at the floor, two or three everywhere else. One more place hands something
over at the floor. The run's own scale goes from 856 places running 765 blocks to **1071 running
979**.

And **a sentence this table has been quoted for since 207 has changed**: `--surf` now costs
**one** flag, not two. `--the-floor` prints that difference off the same six runs that print the
rows, so it moved in the output rather than in somebody's memory — which is exactly what 230 built
it for.

What the seven do not do: the widest run still sets 212 of the 322 gating flags with 110 it never
opens, and the 35 / 31 / 17 / 15 / 12 breakdown of those 110 is unchanged. At the floor the gating
count goes 121 to 123. **Signs are almost entirely not how this game gates anything**, and now
that is measured rather than assumed.

---

## And a sign is the first thing this run can take BACK

Putting them in broke the fixpoint. Every `--say-yes` row ran to the twenty-four-pass backstop:

```
  pass  4: 243 maps,  234 flags …
  pass  5: 243 maps,  233 flags …
  pass  6: 243 maps,  234 flags …
  pass  7: 243 maps,  233 flags …          … and so on to 24
```

A clean two-cycle: one flag on and off forever. `9.6` is a **fifteen-door puzzle** — each sign
sets `0x8008` to its own number and they share one block that compares it against `0x8004` and
**sets or CLEARS `0x0001`** depending on the answer. A walk that stands in front of all fifteen
every pass gets it right and then wrong, forever.

**Everything else this run does is one-way.** Flags get set, things get picked up, people get
talked to; a pass that changed nothing had nothing left to change. That is why comparing a pass
with the one before it was enough, and it is only enough for a step that never takes anything
back. A fixpoint over a step that is not monotone does not converge — it goes round.

`WhereItHasBeen` keeps every state the run has been in and stops when one repeats. The rows settle
in 5 to 7 passes again, and the loop reports a **third** answer:

```
  --play                                     a pass opened nothing new
  --play --say-yes … and all four others      the state came back to one it had already been
                                              in — a CYCLE, not a fixed point
```

Folding that into `NothingMoreOpened` would have lost the finding. A run that settles and a run
that oscillates are different facts about the world, and five of the six rows are now the second
kind.

**The signature is the contents and not the counts.** A pass that clears one flag and sets another
has the same count and is not the same state; a signature built out of counts would call that a
cycle and stop a run with somewhere left to go, which is the expensive direction. The flags fold
in commutatively because a set has no order.

## The breaks

Seven, seven catches:

| break | what went red |
|---|---|
| signs are not run at all | two tests |
| a hidden item is run as a script | `AHiddenItemIsNotAScriptToRun` |
| a sign is read across a counter like a shopkeeper | `ASignIsNotReadAcross…` |
| the cycle test never fires | `AFlagToggledEveryPassIsACycleAndNotABackstop` |
| the signature is built out of counts | three tests |
| the fold is order-dependent | `OrderIsNotPartOfTheState` |
| a state is only compared with the one before it | that, and the toggle test |

The last one is the old behaviour written as a break, and it fails both the unit test and the run.

The counter one is the discrimination worth naming: **a sign is read from beside it and not from
across a counter.** 198 derived the counter rule for a shopkeeper standing behind one; a sign is
not standing anywhere. The fixture asserts both halves — the sign across a counter is not read and
a person in the same square is talked to — because with only the first half it cannot tell "signs
do not use that rule" from "this fixture reaches nothing at all".

The world file is version 29. Signs travel with their kind, because the kind is what says whether
there is a script behind one at all, and a round trip that dropped it would turn 183 hidden items
into 183 scripts at address nought.

3009 → 3020 tests, all green.

---

## What is still owed

* **Which signs actually ran, and what the seven flags are.** The floor sets seven more and two of
  them gate something; none of that is broken down.
* **`9.6`'s puzzle** — fifteen doors, `0x8004` against `0x8008`, and flags `0x0001` and `0x0002`
  as its own state. Read far enough to explain the cycle and no further.
* **`3.57 sign (9,43)`**, which asks for a LEMONADE and takes it away — the example that has been
  in the prompt for milestones and can now actually run.
* **Whether the union differs from the final pass now**, which 190 measured as equal at every
  lever setting. With a cycle they can differ by one flag, and nothing has re-measured it.
* **`0x194`'s nineteen doors** (236), **`0x82`'s seven words** (238), the three numbers nothing
  computes (231), `0x406F` (229), and everything owed at 215 onwards.

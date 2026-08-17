# Milestone 207: the other floor, and the table nobody re-ran

Two things, and the second one was not on the list. It came out of doing what the prompt says
to do first — run `--play`, `--flags` and `--who-knows` — and then reading the output against
the table at the top of the prompt instead of assuming they agreed.

---

## The other floor

206 made both reversed-image floors clump-aware and guarded one of them. The break aimed at the
other came back green, twice: there are **two floors eleven lines apart with near-identical
returns** — `NoiseFloor` for the flag sweep, `MoveNoiseFloor` for the move sweep — and the first
break edited one while the test watched the other. 206 wrote that down and shipped
`MoveNoiseFloor` unguarded on purpose, because the flag fixture produces **no sites at all** for
the move sweep: it matches `0x7C <u16 move>` and there is no `0x7C` anywhere in a fixture built
out of `setflag`s.

So it needed its own fixture, in its own pattern, written backwards for the same reason:

```
    00 01 7C   in the image
    7C 01 00   once the sweep reverses it   ->  findmove <move 1>
```

Six of those at 48-byte spacing is one clump; three of them at 0x10000/0x60000/0xC0000 is three
places. Unlike the flag sweep, this one does **not** require its sites to read on to an end, so
there is no backwards `end` to write — and if that filter is ever added the fixture stops
producing sites and the `Assert.Equal(6, sites)` says so rather than the place assertion quietly
passing on nothing.

### The discrimination, run in both directions

The fault at 206 was not that a guard was missing. It was that **a break pointed at one function
and a test watched the other, and nothing said so.** A guard that only fails when its own floor
is broken is the whole claim, so both breaks were run against both tests:

| break | flag test | move tests |
|---|---|---|
| `MoveNoiseFloor` loses its clump-awareness | **green** — it cannot see this | **caught** |
| `NoiseFloor` loses its clump-awareness | **caught** | **green** — they cannot see this |

And the ordinary case, which is the half that stops "one place, always" passing (fixtures-lie
#8, again):

| break | the clump half | the spread half |
|---|---|---|
| `MoveNoiseFloor` returns `1` places, always | **green** — `1 < 6` is still true | **caught** |
| `NoiseFloor` returns `1` places, always | **green** | **caught** |

Six break runs, and exactly the intended one red each time. `AndTheFlagFloorSaysAsManyPlacesAsSitesWhenTheyAreSpreadOut`
is new too — the flag floor had the clump half guarded and the ordinary one unasserted, so
"always one place" would have passed there as well.

---

## The table nobody re-ran

`--play` printed **153 flags** on the floor. The table at the top of every session's prompt says
**150**, and has said 150 for as long as the file has existed. So the other five rows were run.

```
                                            THE TABLE SAID              MEASURED AT 206
--play                                      183 / 150 in 6, party 6/52  183 / 153 in 6, party 6/52
--play --say-yes                            243 / 225 in 6, party 3/67  243 / 231 in 5, party 4/67
--play --say-yes --in-order                 243 / 227 in 5, party 4/67  243 / 233 in 5, party 5/67
--play --say-yes --boat                     381 / 287 in 6, party 3/77  381 / 293 in 6, party 4/77
--play --say-yes --boat --in-order          381 / 288 in 6, party 4/77  381 / 294 in 6, party 5/77
--play --say-yes --boat --surf --in-order   381 / 286 in 4              381 / 292 in 4
```

**The map counts are right in all six rows. The flag count is wrong in all six.** The party size
is wrong in four, and one row has the wrong number of passes.

### Why it survived thirteen milestones

Because every **difference** the table is quoted for is still exactly right:

* `--surf` still costs two flags — the table's 288 → 286, measured 294 → 292.
* `--in-order` still adds two on the walking thread — 225 → 227, measured 231 → 233.
* `--in-order` still adds one on the boat thread — 287 → 288, measured 293 → 294.
* `--in-order` still adds one party member — party of 3 → 4 in the table, 4 → 5 measured.

Every sentence this project has written **about** the table is true. Only the absolutes are
stale, and nothing anybody said out loud depended on them. That is trap 9 from the other side:
the fault was real everywhere and the blast radius was nought — except that the number the next
session reads first is wrong, and a stale headline is exactly what "assume the number in front
of you is distorted" was written for.

### Where it moved

`--play --say-yes` re-run at each milestone that could have moved it:

```
  193   225 flags, 6 pass(es), 3 in the party      <- what the table still says
  198   227 flags, 6 pass(es), 3 in the party
  199   230 flags, 6 pass(es), 3 in the party
  200   231 flags, 5 pass(es), 4 in the party      <- and it has not moved since
  201   231 flags, 5 pass(es), 4 in the party
  204   231 flags, 5 pass(es), 4 in the party
  206   231 flags, 5 pass(es), 4 in the party
```

**The row is milestone 193's reading.** It moved at 198, again at 199, and again at 200 — where
the run also lost a pass and gained a party member — and it was copied forward through every one
of those. 200 is "the money commands, and the thing behind them", which is not a milestone
anybody would expect to move the walk; that is the point.

The floor row is `--play` alone and has not been chased back past 200, where it already read 153.

**The table in the prompt is now the measured one.** It is written as a reading rather than as a
copy, and the honest thing to do at the start of a session is still to run the three instruments
and check.

---

2808 → 2811 tests, all green.

## What is still owed

* Only `--play --say-yes` was re-run across history. The other five rows were measured at 206
  and their drift was not located. If it matters where the floor went 150 → 153, that is one
  more bisect of the same shape.
* The clumping threshold is a kilobyte and MODELLED. The entropy cut is 4.5 bits and MODELLED.
  Neither has been varied to see whether the answers move — unchanged from 206.
* Nothing has re-read `--in-the-image`'s per-flag counts in bulk — unchanged from 206.
* Six stops, the money ceiling, the GAME CORNER and `0xE6` — all unchanged.

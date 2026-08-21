# Milestone 283: the unread list was a fact about one lever

This project's prompt has carried, since 241, the line *"the **191 sign scripts that run at no
setting** — reach, or a square nothing can stand beside, not separated"*. Three things were wrong
with it. The separation has existed since 249. The number is not 191. And nothing in the project
could have produced that number anyway, because every instrument that sorts unread signs has only
ever been handed **one run**.

---

## Fifty-five, not a hundred and ninety-one

`--the-floor` already runs all six lever settings. It threw the six `Attempt`s away after reading a
row out of each, so the only thing it could say about the fourth list was nothing. Keeping them:

```
      setting                                    read   of them  never got  nothing can
                                                        unread   to a wall  stand there
      --play                                      315     204          8           1
      --play --say-yes                            394     125         17           1
      --play --say-yes --in-order                 394     125         17           1
      --play --say-yes --boat                     464      55         18           1
      --play --say-yes --boat --in-order          463      56         19           1
      --play --say-yes --boat --surf --in-order   463      56         19           1

    AT NO SETTING: 55 of the 519 scripted sign(s) — read by none of the 6 runs
        36  on a map the run never reached
        18  it reached the map and never got to that wall
         1  NOTHING COULD EVER STAND BESIDE IT — not a reach problem, a fact about the file
```

**A sign the floor cannot reach is not a sign nothing reaches.** The floor leaves 204 unread and the
union of the six leaves 55, and the two numbers answer different questions — which is the whole
reason "at no setting" needed an instrument rather than a paragraph.

And the third column is the check that the buckets are named right: **"nothing can stand there" is 1
at every one of the six settings** while the other two move. 211's rule is that a bucket about the
cartridge must not move when a lever does, and here it is passing in the open rather than being
asserted.

## Where the fifty-five are

The 36 are not spread. **26 of them are on `1.96` MT. EMBER**, and five more are one apiece on the
five maps of the DOTTED HOLE — a puzzle nothing in the walk solves. Three are on `1.62`, one on
`1.102`, one on `3.61` RUIN VALLEY. So the reach bucket is two places and a scatter, not a hundred
rooms slightly out of reach.

The 18 that are on maps every run walks:

```
    1.60 (6,31)
    10.9 (0,12)  10.9 (0,9)  10.9 (4,9)                                  CELADON CITY
    12.0 (15,2)  12.0 (16,2)  12.0 (13,10)  12.0 (14,10)  12.0 (13,17)
    12.0 (14,17)  12.0 (1,18)  12.0 (2,18)  12.0 (1,10)  12.0 (2,10)     CINNABAR ISLAND
    12.0 (3,1)
    14.2 (5,2)  14.2 (7,2)                                               SAFFRON CITY
    35.1 (2,1)                                                           FOUR ISLAND
```

**Ten of the eighteen are on Cinnabar Island, and they come in five adjacent pairs** — `(15,2)` and
`(16,2)`, `(13,10)` and `(14,10)`, and three more — each pair two neighbouring squares with two
separate script blocks twelve bytes apart. That is a shape, not a scatter, and this milestone does
not read it further.

**And none of them needs a swimmer.** The buckets are sorted with the water open, so a sign whose
only standable square is sea would sit in the reach bucket looking like a corner nobody walked into.
Asked again with the water shut: **0 of the 18**. One of the six runs surfs, and it was not the
answer.

That question is asked of the reach bucket **only**. Asking it of "nothing can stand beside it"
would return the whole bucket every time — a sign nothing can stand beside with the water open is
one nothing can stand beside with it shut, necessarily — and the first version of this printed
`of those 1, 1 can only be read from WATER`, which is 219's rule caught in the act.

## 154 was twenty-two times seven

The prompt also carried `1.114 0x08163F5A`, read 154 times in one run, *"which nobody has asked is a
wide sign or a wide walk"*. It is both, and the number is a product:

```
      address     records  squares   times   most
      0x08163F5A      22       22     418      7
      0x0816AC94      11       11     407      7
      0x0816A580       6        6     222      7

    THE WIDEST BLOCK, 0x08163F5A — its 22 record(s) sit on 1.114 x22
      --play                                       0 square(s) x 0 pass(es) =    0 read(s)
      --play --say-yes --boat                     22 square(s) x 7 pass(es) =  154 read(s)
      --play --say-yes --boat --surf --in-order   22 square(s) x 5 pass(es) =  110 read(s)
```

**Twenty-two sign records on one map — `1.114`, ROCKET WAREHOUSE — all pointing at one block, stood
at once per pass over seven passes.** Neither factor alone is the finding: a walk that passed one
square 154 times and a block behind 154 squares would print the same 154. This is 224's "519 records
at 360 addresses" showing up in the run rather than in the scan, and **59 of the 327 blocks any run
reads are shared**.

The first three rows also show the walk reading a block once per square per pass and never more,
which is what makes the multiplication exact.

## The side rule in the sorting: right, and worth nothing

280 made the walk read 97 signs from the one square their kind names. `WhySignsWentUnread` — which
sorts what the walk missed — was still asking 242's five-square question. One rule in two places,
disagreeing.

Fixed, with the old rule kept as an in-process control (241), and the honest answer is:

> **the side rule moves 0 signs** out of a reach bucket and into a fact about the file.

And it can never move any. 279 *read* the side off the named square being walkable — 73 of 73, 14 of
14, 10 of 10 — so a sign whose named side is a wall does not exist in this cartridge, and this
sorting is asked with the water open, which can only add squares. **The reading and the evidence are
the same fact, so the cartridge cannot be evidence against it.** The difference the parameter buys is
real and only a fixture can show it, which is where it now lives.

## And what SOLID says

282 left 41 buried items on squares nothing can stand on. The collision field is two bits, so a
reading of "solid" as one thing is a claim that the other three values do not mean anything separate:

```
    In the whole world: 0 x110028, 1 x123713
    the 41 buried on a wall carry: 1 x41
```

**Two of the four values occur at all**, and every one of the 41 carries the same one. So `IsWalkable`
being `== 0` throws nothing away and there is no third meaning hiding in the field. A negative, and
the reason it is here is that "35 buried items on ordinary ground marked solid" would have been a
reading rather than a fact if it were not.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the sorting ignores the side and asks all five squares | 1 | **1** |
| the named side becomes the sign's own square | 1 | **1** |
| `AtNoSetting` intersects the reached maps instead of unioning | 1 | **1** |
| `AtNoSetting` drops the runs' read lists | 1 | **1** |
| `CanBeStoodBeside` ignores the grid it was handed | 5 | **5** |
| **CONTROL:** `Any(p)` written `Where(p).Any()` | **0** | **0** |

Five predictions, five matches — and **a sixth attempt that was green because the break was wrong,
not because the guard was**. It was meant to be "take the reached list from the first run only" and
was written `if (reached.Count == 0) reached.UnionWith(maps)`, which still lets a later run fill an
empty set — so it never took the first run only, and the fixture it was aimed at was right to ignore
it. 219's rule is about guards nothing *can* fail; this was a break that did nothing. The corrected
one is the third row.

## What is left

* **The five pairs on Cinnabar Island.** Ten of the eighteen, in adjacent twos, on a map every run
  walks. Something about that map's shape, or about what a pair of side-by-side sign records is.
* **26 signs on MT. EMBER and 5 in the DOTTED HOLE** are the reach bucket almost entirely, and they
  are two puzzles rather than a distance.
* **`10.6 (4,1)`** is settled as far as the bytes go (281: an ordinary byte in a walled block, so
  not a collision misreading) and stays the single sign in the cartridge nothing can read.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — eight milestones owed now.

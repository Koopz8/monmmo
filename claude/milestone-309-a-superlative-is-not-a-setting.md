# 309 — a superlative is not a setting

**3555 tests green.** Base: 308, 3554.

307 added a lever. 308 changed the rule that decides what a scene leaves for the next one. Both
re-ran `--the-floor` and neither re-ran the rest of the block — which is 230, 231, 264 and 72's
fault, four times over, and the thing this project keeps paying for.

The concrete evidence that it was owed: the block says *the widest run sets 216 of the 322 gating
flags — 106 gates it never opens.* It reads **219 / 103** now.

---

## One experiment separates three populations

Re-run **the old superlative** — `--play --say-yes --boat --surf --in-order`, which is what "the
widest" meant before 307 — under the **new build**. Anything that reproduces was right and only
the row moved. Anything that does not was actually wrong.

### Held exactly — and one of them is 211's hardest test yet

```
      setting                                    gates set  never set  boundary  reach  obstacle  picked up  past it  took back  ever on  took back  places  routines  signs   at   on
      --play                                           123        199        35     37        15        100        8          4      164          4     472        44    315  214   79
      --play --say-yes                                 163        159        35     36        15         60        7          6      238          6     755        66    394  287  106
      --play --say-yes --in-order                      165        157        35     36        15         60        7          4      238          4     538        53    394  287  106
      --play --say-yes --boat                          214        108        35     29        15         13        8          8      308         10    1223        92    470  333  140
      --play --say-yes --boat --in-order               215        107        35     30        15         13        7          7      308          9     871        79    469  333  140
      --play --say-yes --boat --surf --in-order        216        106        35     30        15         13        8          5      306          6     873        80    469  333  140
      ... --on-load                                    219        103        35     25        15         13        9          6      320          7     895        86    469  333  140
```

**BOUNDARY reads 35 at all seven settings and OBSTACLE reads 15.** Both are properties of the
FILE — *no setflag anywhere names it*, *it is a tree, a rock or a boulder* — so 211 says they
cannot move with a lever. They did not, across a lever added and the memory rule changed. The
block claimed exactly this (*"35 and 15 are the same at every lever setting, which is how a
property of the FILE has to behave"*) and now the command prints the verdict instead of the reader
checking it.

The whole floor row held too: **123 / 199** gating, **164** ever on against the **160** it stops
with, **315** signs at **214** addresses on **79** maps. So did the widest's signs — **469 / 333 /
140** at both widest rows. And the took-back sequence **4 / 6 / 4 / 10 / 9 / 6** reproduced to the
digit, with a seventh (7) added.

### Moved because the row moved, with nothing wrong

`216 / 106` is **still exactly right** at `--play --say-yes --boat --surf --in-order`. It reads
219 / 103 only because 307 put a new row after it.

**The widest is a row, not a setting.** Every line in this block saying *the widest run* was a
claim about whichever row happened to be last, and adding a lever moved all of them at once
without any of them being a claim about that lever. `--the-floor` now says which row is which.

### Actually wrong

* **`869 places call 76 routines the widest run cannot answer`** is **873 / 80** at its own row —
  wrong before either milestone touched anything.
* **The six why-shut buckets never added up to their own total.** *106 gates it never opens* on
  one line; *those 109 are 35 / 30 / 16 / 15 / 8 / 5* on the next. Six numbers summing to **109**
  under a total of **106**, each maintained by hand at a different milestone, every one of them
  individually plausible. The true split at that row is **35 / 30 / 15 / 13 / 8 / 5 = 106**: only
  *never picked up* was wrong, by three, and **the total was right all along**.

---

## The fix is that nobody maintains them

`--the-floor` gained **THE BLOCK'S RUN-DEPENDENT LINES** — one row per setting for every number
the block used to keep by hand, off the seven runs it already makes. Signs are printed as records,
addresses **and** maps, because 224 is the milestone about counting one when you meant another and
241 said 215 and 328 for what are 315 records at 214 addresses.

And **NEVER SET is the six buckets added up**, not a seventh count. A `!` appears beside it if they
ever stop partitioning. The split and the total are one number now, so they cannot be kept by two
hands again.

---

## The guards

Two breaks, both predicted 1:

| break | predicted | killed |
|---|---|---|
| the past-the-boundary bucket collapses into the boundary | 1 | **2** |
| — | | |

The over-shoot is existing coverage being better than I thought (32).

**And one test I wrote was a tautology.** `Enum.GetValues<ShutBecause>().Sum(why => shut.Count(g
=> g.Why == why))` always equals `shut.Count`, because every gate's `Why` is *some* enum value —
a test named for a discrimination it does not make, which is the thing this project's own fixture
notes warn about. Rewritten to **name the six reasons the output prints** and assert nothing falls
outside them, plus that the enum has exactly six members: now a seventh reason added without the
printer learning about it fails a test instead of quietly drifting the total.

---

## What this leaves

* **`279 of the places, across 59 routines, have an answer nothing branches on`** was NOT re-run.
  It needs `SpecialCalls`' profiles joined to the run, which the floor table does not do. It is
  marked in the block as not re-run rather than left looking checked.
* **The block still has lines no instrument prints.** 231 found four; this pass did not sweep for
  new ones. The honest end is that every number in the block is printed by something, and
  `--the-floor` now covers the run-dependent ones — the ROM-only ones are spread across a dozen
  commands with no single place that gathers them.
* **`--the-floor` is slow and getting slower** — seven runs plus a whole-image flag sweep. Nothing
  in it is wasted, but a session that only wants the rows pays for all of it.

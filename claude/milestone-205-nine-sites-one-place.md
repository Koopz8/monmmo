# Milestone 205: nine sites, one place

The 41 unreached doors thread, re-asked. `1.103` MT. EMBER is behind `0x0089`, the RUBY is
behind that, and the standing answer has been *nothing in the world sets it*. The reading now
opens 73 more blocks than when that was last checked, so it was worth asking again.

The answer is still nothing. What changed is that the instrument was reading its own floor
wrong, and has been for as long as it has existed.

---

## Nine against a floor of one

```
  a three-byte pattern turns up by accident about 1.0 time(s) in an image this size
  — which is the error bar on every count below

  0x0089 — 9 site(s) in the file, 0 of which read as script, 0 of which the map scan opened
```

Nine against one. Read straight, that is signal — nine times the noise floor, and every one of
them "probably noise" only because none of them decodes.

Where they are:

```
  0x47CB0B -> 0x47E62B    6944 bytes
  0x47E62B -> 0x47E6AF     132
  0x47E6AF -> 0x47E6DB      44
  0x47E6DB -> 0x47E79F     196
  0x47E79F -> 0x47E7CB      44
  0x47E7CB -> 0x47E8BB     240
  0x47E8BB -> 0x47E93F     132
  0x47E93F -> 0x70D6D3 2682260
```

**Seven of the nine are inside 791 bytes.** And the bytes there are not script:

```
  0x0047E620  54 54 54 54 00 00 00 10 94 52 4A 29 89 00 00 00
  0x0047E630  C7 CF CC C5 CC C9 D1 FF 00 00 00 FF 4E 00 D7 00
  0x0047E640  54 01 35 00 62 00 18 00 00 00 54 54 54 54 54 54
  0x0047E650  00 00 00 10 94 52 4A A9 0C 00 00 00 CC BB CA C3
```

`00 00 00 10 94 52 4A` twice in fifty bytes, with `29 89` at a fixed offset inside the record
and a run terminated by `FF` beside it — a table with names in it. Entropy **4.70 bits per
byte** across the run; this cartridge's script regions run about **5.70** and the file as a
whole **6.38**.

## The floor was a whole-image average, and the image is not uniform

`ByChance` computes "about 1.0 occurrences" by treating every byte as independent. Nothing in a
uniform model predicts seven hits in eight hundred bytes — in a region of that size the uniform
expectation is **0.00005**.

So the count is nine and the finding is one, and the floor could not say so. Nine sites spread
over 16 MiB and nine sites inside one kilobyte are the same number and completely different
findings, and they have printed identically for the whole life of `--in-the-image`.

It now says both:

```
  0x0089 — 9 site(s) in the file, 0 of which read as script, 0 of which the map scan opened
    7 of them sit within a kilobyte of another — that is 1 place(s), not 7. The whole-image
    error bar assumes independent bytes and cannot model a clump; a run of table data makes
    them all by itself.
      0x47E62B..0x47E93F  7 site(s) in 791 byte(s), entropy 4.70 bits/byte   <- table-like, not script
```

And it comes back the other way when it should: `0x003E` has one site and prints nothing;
scattered sites print *no two of them are within a kilobyte of each other — so the count above
is that many separate facts about this file*.

## What this settles, and what it does not

`0x0089` is **closed** as a script question, and more firmly than before: it was nine sites
against a floor of one, and it is one clump of table data plus two isolated hits against a floor
of one. MT. EMBER is behind compiled code, and the RUBY behind that. Reading it means
disassembling ARM, which is a different job from anything in this repository.

What is not settled is how many *other* counts this project has quoted are clumps. Every number
`--in-the-image` and `--who-knows` have printed carries this error, and only the ones that were
looked at by hand have had it caught.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| every set of sites is one clump | two of the four |
| clumps counted as one place however many runs | two of the four |
| entropy stops discriminating | `ARepeatingRunReadsAsATableAndAVariedOneDoesNot` |

None green. The ordinary case is asserted in advance — sites spread across the file are **not**
a clump — because without it "everything is one clump" passes the interesting test and the
instrument says nothing it did not say before.

The rule lives on `HowClustered` and not in `Program.cs`. **That is the seventh time this
project has moved a rule about the world out of the printer**, and the first three breaks would
all have come back green if it had stayed there.

2803 → 2807 tests, all green.

## What is still owed

* **Every count this project has quoted against a uniform floor.** `--who-knows`'s 600 against
  787, the raw whole-file sweep's 3762 against 3675 — none has been checked for clumping. The
  instrument exists now; the numbers have not been re-read.
* The threshold is a kilobyte and it is MODELLED, said out loud on the constant. Chance puts two
  three-byte hits that close about once in sixteen thousand pairs, so anything of the same order
  gives the same answer — but it is a choice.
* `0x0089` needs an ARM disassembler or nothing. It is not a script problem.
* Six stops left, the money ceiling still unlevered, and `0xE6` still load-bearing in eight
  fixtures without ever having been read.

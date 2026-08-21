# Milestone 280: ninety-seven signs, and the walk was standing on the wrong side of two

279 read the sign record's KIND byte and found three of its five values name the side you have to be
standing on. It left the walk still reading all 519 sign scripts from any of the four squares around
them. This makes the walk obey the record and measures what that is worth.

**It costs 0 maps, 0 flags and 2 signs — at the floor and at the widest.**

---

## The plumbing was already there

239 put the fourth list into `MapData` and the exported sign record has carried `Kind` ever since,
because the kind is what says whether there is a script behind the record at all. So nothing needed
adding to the world file: `MapSign.MustBeReadFrom` is the adopted table — `0x01` south, `0x03` west,
`0x04` east, and nought for the other two — with 279's evidence written onto it, and `Autoplayer`
asks it instead of `Beside`.

`0x02` is not in the table. By elimination it would be north and this cartridge has none of them, so
naming it would be an inference with no record behind it. 67's bar: the reading is what the
cartridge exercises.

## And the control is in the same process

241's rule is that a before-and-after across two builds is a measurement with no instrument.
`obeySignSides` is the switch, `--play --signs` runs the walk three times over — with signs, without
them, and with the side ignored — and the difference is subtracted rather than remembered:

```
      AND THE SIDE (279, 280). 97 of the 519 sign script(s) name ONE square to be read from; the
      rest are read from any of the four 242 allows. The same run with the side IGNORED: 183 maps,
      160 flags in 6 pass(es), 317 sign(s) read against this run's 315
      so obeying the side costs 0 map(s), 0 flag(s) and 2 sign(s) — and there is no flag the loose
      run has that this one does not.
```

At the widest it is the same shape: 465 signs against 463, 381 maps and 296 flags either way.

## What that means, and what it does not

279 predicted the blast radius from the records alone: 68 of the 97 have a walkable neighbour the
kind forbids, so 68 signs *could* be over-read. **The walk actually stood on the forbidden side of
two.** A count of how wrong something could be is not a count of how wrong it was (9), and the gap
here is thirty-four-fold.

Every one of the six floor rows is unchanged — `--the-floor` reproduces all of them and every delta.
That is 190's pattern: a fix that moves no headline is not evidence it was not a fix, and the two
signs are real. What moved is the sign counts: **315 of 519 at the floor** (was 317) and **463 at
the widest** (was 465), with the two joining the "reached the map and never got to that wall"
bucket, which goes 17 to 19.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the named square is the sign's OWN square | 4 | **2** |
| south and north swapped | 3 | **3** |
| the walk ignores the side it was handed | 4 | **4** |
| the control switch removed, so it always obeys | 1 | **3** |
| **CONTROL:** the square written with a `with` expression | **0** | **0** |

Two misses, one in each direction, and both are about the theory cases rather than the code. The
own-square break only touched the SOUTH mapping, and the two theory rows are west and east — so
they could not have noticed, and I had counted them. The control-switch break kills three because
those same two rows assert the loose run as well, which I had not counted. **A prediction that
misses tells you which fixture does not cover what you thought** (32), and here it told me twice
about the same two rows in opposite directions.

## What is left

* **`0x02` is still an inference.** Four kinds, three sides, one direction absent.
* **The client does not know about the side.** `LoadedMap` carries the signs and this rule lives on
  `MapSign`, so both halves already have it — but nothing on the client asks, and a rule enforced on
  one side of the split needs its counterpart on the other. Unmeasured and unasked.
* **The buried kind's own square rule.** 249 measured that the walk stands on 182 of the 183 buried
  signs; whether a buried item has a side is a question nobody has asked, and 279's table shows kind
  `0x07`'s neighbours open at 120/147/142/127 of 183 — no side, on the face of it.

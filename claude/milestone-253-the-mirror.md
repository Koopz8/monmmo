# Milestone 253: the mirror, and what it took to make it usable

252 swept every operand of every command and scored each by how much of what it names something
**writes**. That seed has a shape to it: it can only find operands naming variables the scan
writes. A read of a variable only compiled code ever touches scores nought and is
indistinguishable from an operand naming item ids — and this project knows one of those exists,
because `0x405F` has forty-three squares waiting on it and nothing in sixteen megabytes writes it
(250).

So: seed on the readers instead. That is the mirror, and it took one correction to be worth
anything.

---

## Run the obvious way, it produces twenty-seven

```
      0x46 arg0: 35 number(s) — writers   9 %, readers 100 %   <- INVISIBLE TO THE FIRST SEED
      0x44 arg0: 45 number(s) — writers   4 %, readers  98 %   <- INVISIBLE TO THE FIRST SEED
      0x65 arg0:  8 number(s) — writers   0 %, readers  88 %   <- INVISIBLE TO THE FIRST SEED
      0x63 arg0:  8 number(s) — writers   0 %, readers  88 %   <- INVISIBLE TO THE FIRST SEED
      … twenty-three more
```

`0x44` and `0x46` are the two commands that hand an item over. Their first word is an **item id**,
which `ItemMentions` has read as one since it was built. Scoring a hundred per cent against a
seed meant to identify variables.

**The reader list contains `0x1A arg2`**, and 244 established what that operand is: it names 149
numbers of which three are ever written, because `copyvarifnotzero`'s second word is a plain value
unless it happens to be a variable id. Seed on it and the question *is this number a variable?*
quietly becomes *is this number small?* — and every item id, movement type and coordinate in the
game answers yes.

## Corrected, it produces one

```
    1 operand(s) score above half on the widest seed and are in neither table:
      0x42 arg2: 4 number(s) — writers 100 %, readers 100 %, both 100 %
    with the value-naming operand(s) LEFT IN the seed it would be 27 candidate(s)
    the seeds: writers name 111 number(s), the corrected readers 82, the raw readers 231
```

One, and it is `0x42 arg2`, which 252 already reported and left open. **Nothing else outside the
two tables names variables on either seed.**

The correction is derived, not asserted: which operands name values comes from
`BothNamespaces.NameValues`, which decides by written-ness and named exactly one on this
cartridge. Nothing here writes down that `0x1A arg2` is special — it is measured, one milestone
at a time, and then used.

**Both counts stay in the output.** Twenty-seven is the argument for the correction, and a
correction whose size nobody can see is one the reader has to take on trust. So is the seed width:
111 against 82 against 231, printed beside the counts, because a wider seed finds more candidates
by being wider and that is not a finding.

## What it means

```
  so what is left is 1 operand(s) across both seeds: 0x42 arg2
```

**Both tables are complete on this cartridge.** 251 found `copyvar` missing from both write
tables; 252 found `specialvar` and `0x42 arg0` missing from both; 253 asked the question from the
other side and found nothing new. Three milestones of an audit that started with one line of a
table, and it ends with an instrument that would say so if there were a fourth.

That claim is only worth having because the sweep can come back empty, and because its own
uncorrected version — the one that would have produced twenty-seven confident false operands —
is printed next to it.

## The breaks

| break | predicted | went red |
|---|---|---|
| the seed keeps its value-naming operands | 2 | 2 |
| the correction empties the seed instead | 2 | 2 |
| the seed filter's name does not match an operand's | 2 | **5** |

The third was low again, and for the reason the last two have been: the operand's name is
load-bearing in more fixtures than I had modelled — five of them select an operand by it. That is
the third prediction in three milestones to miss in the safe direction, and each one has said
something about the fixtures rather than the code.

3114 → 3117 tests. **The floor table did not move.**

---

## What is still owed

* **`0x42` still has no name.** It writes its first operand, names a variable in its second, and
  has eight places in the whole game — each one a `compare` away from saying what it computed.
  `--read-from` on those eight is one command, and it is the last thing the operand audit leaves.
* **`0x42 arg2`'s direction** — 12% on the compared-next test against a floor of 1.5%, which is
  above the floor and far below its sibling's 75%. Neither table has it and neither should yet.
* **The whole image has never had this treatment.** `--operands` asks the map scan, which is 0.6%
  of the file.
* **The 82 trigger conditions waiting on a value nobody writes** (250), 56 distinct.
* `0x405F` (250); the base (248); the eight unused indices and the spare bit (248); collecting the
  buried items (249, a decision); `0x8013` and `0x4025` (251).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.

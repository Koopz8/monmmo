# Milestone 267: the two thirds nothing points a map at

252 found two write operands in neither of this repository's tables, and said in its own note what
came next:

> the whole image — `--operands` asks the map scan, which is **0.6% of the file**.

Fifteen milestones later, `--operands-everywhere` asks it. The answer is no, and the value of the
milestone is that the no has a number on it.

---

## The population is the cartridge's own index

"Every offset whose bytes read as a script" is sixteen million tries and mostly luck. What this
uses instead is how the file names a script: **four bytes holding a ROM address**. Compiled code
puts one in a literal pool or a table; a script puts one in a `call` or a `goto`. An address
something points at, which decodes to a proper end, is a way in; everything reachable from it is a
block.

```
    pointers            least   named   entries   blocks  |  BACKWARDS        | calibration
    at any offset           1   75925     13183    13270  |    2594 / 2616    |     43 %
    at any offset           8   75925      2094     3396  |     483 / 494     |     60 %
    on a 4-byte boundary    1   46143      8860    10240  |     451 / 456     |     42 %
    on a 4-byte boundary    8   46143      1440     2734  |      77 / 79      |     59 %

    for comparison, the map scan: 3888 block(s), calibration    98 %
```

**Alignment is chosen by the floor and not by the answer.** Turning it on cuts the reversed-image
floor 5.7-fold — 2616 blocks to 456 — and the real count 1.3-fold. Both settings stay in the
output, because a tightening whose only evidence is that the answer got tidier is a tightening
chosen by the answer.

## 6621 blocks no map leads to

```
    10240 block(s) against 3888 the map scan opens, and the floor's 456
    6621 the maps do not lead to, and 269 the maps lead to that NOTHING in the file points at
```

Twenty-two to one against the floor. **Every sweep this project has ever run over "the scripts"
has run over about a third of them** — that is what "0.6% of the file" has been shorthand for, and
this is the number.

The 269 going the other way are blocks named only by an unaligned pointer: a `call` or a `goto`
inside another script, which is exactly the sort the alignment filter is allowed to drop because
the caller's reach brings them back.

## And the answer is no, because of the calibration row

262's rule is that an instrument whose known rows are in its own output controls itself on every
run. The row here is **`compare`'s variable operand**, which the written-ness method scores at
**98%** over the population this project trusts.

Over the whole image it scores **42%**. Tightening the length threshold until the floor is
seventy-nine blocks — a signal-to-floor of thirty-five to one, cleaner than anything else in this
repository — moves it to 59% and stops.

So the split, in the same run of the same instrument:

```
    aligned, least 1: the 2337 entries the maps DO lead to score 92%; the 6523 they do NOT score 27%
    aligned, least 8: the  494 entries the maps DO lead to score 90%; the  946 they do NOT score 38%
```

**The failure is entirely on the outside half.** Noise would have been spread across both, and
would have thinned as the floor fell; this does neither.

The reading is that **the scripts outside the maps compare variables that no script anywhere in
this image writes** — the code boundary, seen from a side this project has not seen it from. Which
means the wider population cannot be used to check the operand tables, because the test's own
calibration fails on the half it was built to look at.

## So nothing is adopted

The sweep puts four operands above half over the whole image, the highest being `0xAD arg0` at
58%. **None is adopted and none is a finding.** An operand scoring 58% on a population where
`compare`'s variable scores 42% has not out-scored anything; it has been measured with a broken
ruler, and the ruler is printed beside it.

252's question stays open. What it has now is a reason rather than a gap, and the reason is a
number in the output of the command that failed to answer it.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| an address that does not decode is still a way in | 2 | 2 |
| alignment is not checked | **1** | **2** |
| `Pointed` counts every address whatever the filter | 1 | 1 |
| the length threshold does nothing | 1 | 1 |
| the blocks are just the entries | 1 | 1 |
| the floor is the image forwards | 1 | 1 |
| **CONTROL:** the entries come out unsorted | **0** | **0** |

The second was under-counted rather than mis-modelled: turning alignment off promotes the
fixture's `goto` target from a block to an entry, and a test asserting it is *not* an entry
noticed. Two guards on one line, and only one of them was in the prediction.

The control's nought is 261's kind: the entry order cannot change a set of reachable blocks, and
the blocks are a set.

## What is left

* **What the 6621 blocks ARE.** They are counted and not read. The obvious first cut is what
  points at them — a table, a literal pool, another script — which `EverywhereInTheImage.NamesIt`
  can already say and nothing has asked of this population.
* **Whether the outside half's variables are one band or many.** 27% written is an average over
  6523 entries and says nothing about whether they share a namespace. `--namespaces` asked of this
  population is one command.
* **A population that could carry the operand test.** Nothing here found one. The two filters
  swept are alignment and block length; what has not been tried is requiring more than one pointer
  at the same address, or excluding blocks that overlap each other.
* **`0xAD arg0`** is above half on both populations and named by neither table. On the map scan it
  is the one 253 could not settle either. It is still not settled.

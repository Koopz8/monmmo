# Milestone 263: the debt 231 marked, paid

231 audited this project's block of standing numbers and found four that **no instrument in the
repository printed**:

> `62 gates hold 240 people`, `146 trees and rocks`, `158 objects`, `the ceiling is 45 of 437 byte
> positions`

They read like measurements, they were quoted like measurements, and nothing could have
contradicted them. 231 marked them and said each needed an instrument or deleting. Thirty-two
milestones later they were still marked.

---

## The half that was never counted

Every line this project prints about flag gates counts **gates**. The four numbers are about what
those gates **hold**, and no code path had ever added it up.

```
    322 gating flag(s) hold 605 object(s) in all — 2 of them hold none, which is the boat's
     15 hold a tree or a rock and take it off the map — 146 object(s)
     12 hold a boulder and never take it off — 12 object(s)
     27 obstacle gate(s) between them — 158 object(s)
    295 hold anything else — 447 object(s)
    of the 295 others: 272 hold one, 10 hold 5-16, 8 hold 2-4, 3 hold more than 16, 2 hold nothing
     21 hold MORE THAN ONE — 175 object(s) between them
```

## 146 and 158 are exact

Seventy-three milestones after they entered the prompt, both come back to the digit. Fifteen gates
hold 146 trees and rocks; all twenty-seven obstacle gates hold 158 objects.

**Two numbers that could not have been wrong out loud turn out to have been right all along.**
That is the least satisfying way for an audit to end and the only honest one — 231 said exactly
the same of `936`.

And **605 is a cross-check nobody asked for.** The prompt says elsewhere, off the object records,
that 605 of the cartridge's 1600 objects carry a non-zero hide flag. This counts them from the
gate side, through a different structure, and gets the same number.

## Two are withdrawn

**"62 gates hold 240 people" — withdrawn.** No split this instrument produces is that. The shape
it was a claim about, gates holding more than one, is **21 gates and 175 objects**. It is withdrawn
rather than corrected: nothing ever computed it and nothing reproduces it, so there is no version
of it to fix.

**"The ceiling is 45 of 437 byte positions" — withdrawn.** `--play` already prints the ceiling in
byte positions, per bucket. At the widest setting:

```
     90 places, 1 routine: nought takes EVERY branch —  1 of   1 byte position(s)
     63 places, 7 routines: nought takes some         — 16 of  39 byte position(s)
    431 places, 9 routines: nought takes none         —  0 of 319 byte position(s)
```

**17 of 359** at the widest, 10 of 344 at the floor. And `437` is `--routines`' count of *every*
branching byte position in the file — a different denominator entirely. Neither the numerator nor
the pairing survives.

## Where the split lives

`WhatTheGatesHold`, not four sums inline in the dump command. A rule in a printer is a rule no
fixture can reach, which this project has fixed at 219, 221, 222, 223 and 257 and would otherwise
have walked into again in the milestone that exists to pay an audit debt.

The gate count is **the flags asked about**, not the flags with something behind them — the boat's
two hold nobody and are still gates. Folding those together is how "322 gating flags" and "320
gate somebody standing there" stop being two facts.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| a gate holding nothing is not a gate | 1 | 1 |
| holding one counts as holding several | 1 | 1 |
| the several's objects are all the objects | 1 | 1 |
| holding nothing falls into the 2-4 band | 2 | 2 |
| **CONTROL:** the bands come out rarest first | **0** | **0** |

The control's nought is 261's kind rather than 257's. `OrderBy` is stable, so both orderings are
deterministic and both diff cleanly between runs; the order is presentation and not a rule about
the world, so there is nothing to guard.

## What is left

* **The rest of the block.** 231 checked 45 lines and found four uncomputed; those four are
  settled now. The other forty lines it did not reach have still not been re-run, and 230's point
  stands: they entered in one commit and were copied forward.
* **Two gates hold nothing and they are the boat's.** That is now printed rather than inferred,
  and it is the only place in the gate reading where "holds nobody" and "gates nothing" have to be
  told apart.
* **`605` agreeing from two directions** is the kind of cross-check worth having more of. Nothing
  else in the gate reading has one.

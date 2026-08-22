# Milestone 296: a slot something else already took

295 replaced a distance with two rules and named the next one in its own leftovers: *the barrier is
the previous CALL; a plain command that consumes the slot would take the value too, and this does
not check for that.* This checks for it.

---

## A value is for the next thing that reads the slot

295 stops the backward walk at the previous call, because a value belongs to the first call after
it. The same argument applies to anything else that reads the slot — `copyvar` taking its source
from it, a command handed it as an operand — and 295 walked straight past those, so a call further
on collected an argument that had already been spent.

Walking backwards, a slot named by any command between here and the call is **taken**, and a
`setvar` to it before that point is not this call's argument.

```
      window   handed a value   in 0x8004   only elsewhere   selectors   arguments
           1               30          18               12           1        1143
           4               37          29                8           1        2185
           6               36          28                8           1        2147
           8               36          28                8           1        2181
          12               37          29                8           1        2185
        4096               37          29                8           1        2185
```

> **37 routines are handed a value in an argument slot, 29 in `0x8004`, 8 only somewhere else.**

295's 39 / 30 / 9, corrected downwards as 295 predicted it must be. Twenty arguments and two
routines were somebody else's.

**And the sweep is no longer monotone**: 37 at a window of four, **36** at six and eight, 37 again
at twelve. A wider window can now take a routine AWAY, because reaching further back finds the
command that spent the value. Under every earlier rule widening could only ever add, which is what a
rule that collects rather than decides looks like.

It still converges at twelve and holds to 4096.

## A setvar reads nothing

Its first word is where a value goes and its second is the value — a literal, even when the literal
happens to equal a slot number. Treating that second word as a reference would bar a slot because of
a number that means nothing.

**This cartridge never does it.** The reading is identical either way, which is exactly why the rule
needs a fixture rather than a measurement: it is a choice about what a `setvar` IS, and the file
cannot be asked.

`copyvar` is not a `setvar`, so its source half is read as the reference it is.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the slots named are barred one number off | 1 | **1** |
| only a SETVAR's slots are barred — the rule inverted | 10 | **10** |
| any taken slot bars every slot | 1 | **1** |
| **CONTROL:** the range test written as two comparisons | **0** | **0** |

Three predictions, three matches. The second is a whole-file break by design: inverting which
commands contribute reads means every `setvar` bars its own destination, so no call has an argument
at all and every fixture in the class goes down together. A rule at the centre of a reading should
take everything with it.

## What is left

* **`copyvar` into a slot is a WRITE this does not see.** `Before` records a `setvar` and nothing
  else, so a value copied into `0x8004` is invisible as an argument and its source is read as
  spending a slot. Both halves of that are wrong in opposite directions and neither is measured.
* **The forward window is still four** and still chosen (294, 295). Everything "compared against"
  in 291-296 rests on it, and it has never been swept.
* **`All`'s threading is still unguarded** (294).
* **Four readings of one number in five milestones** — 44, 62, 39, 37. Each correction was smaller
  than the last, which is what convergence looks like and is not the same as being right.

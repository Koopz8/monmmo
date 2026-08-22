# Milestone 292: the argument sweep read one slot of six

Went after `9.6`'s puzzle — *fifteen doors, `0x8004` against `0x8008`* — and did not find it. What
the looking found instead is that this project has been reading one argument slot since 236 and the
cartridge uses six.

---

## What `9.6` turned out not to be

`9.6` is VERMILION CITY, eleven by twenty-one, and it calls six routines. One of them, `0x015B`, is
**called sixteen times here and nowhere else in the game** — which is as close to a signature as a
map gets. `0x0187` and `0x0188` are called fifteen times each: the fifteen doors.

Asking the new instrument which slot each is handed a value in:

```
    0x015B: none — nothing sets an argument slot in front of any of its calls
    0x0187: none
    0x0188: none
```

**Nothing.** The `0x8004`-against-`0x8008` in 239's note is not an argument to any of them. The
puzzle stays open and is now open with a fact attached: whatever those three routines are told, they
are not told it in an argument slot immediately before the call.

## And what asking the question found

```
    of the 178 routine(s) the map scan calls, 44 are handed a value in some argument slot;
    33 of those in 0x8004, and 11 ONLY in some other slot

    every slot this cartridge's scripts use, by how many routines take a value in it:
      0x8004 x33, 0x8005 x16, 0x8006 x7, 0x8007 x1, 0x8008 x1, 0x800F x1
```

**Eleven routines take an argument and no sweep in this project can see it.** 236 measured that 25
of the 178 take a value in `0x8004`, every reading since has read that slot, and 291's own
instrument — written three hours ago — hard-coded it as a constant with a comment explaining why.

The comment was right about what 236 measured and wrong about what it licensed. *This cartridge
puts a routine's argument in `0x8004`* was never the finding; *236 counted the ones that do* was.

It is 290's stride one list over: **a sweep that can only see one shape reports the shapes it
cannot see as absent.** There the stride was two bytes where the command wanted one; here the slot
is one of six.

## What this does not say

236's 25 and this milestone's 33 are not the same measurement and neither corrects the other: 236
counted routines handed a `0x8004` **in the run**, and this counts routines handed one **in the
scan**. A run takes one arm of every branch. The two numbers belong to different populations and
saying so is cheaper than reconciling them.

And 44 of 178 is what the map scan can see. A routine handed its argument by compiled code, or more
than four commands before the call, is outside `SpecialCalls`' window and outside this.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the grouping reads `0x8004` rather than the slot asked for | 1 | **1** |
| `ArgumentOf` ignores its slot argument | 2 | **2** |
| a slot's VALUES counted as its calls | 1 | **1** |
| the slots grouped by value rather than by slot | 1 | **1** |
| **CONTROL:** `Distinct().Count()` written `ToHashSet().Count` | **0** | **0** |

Four predictions, four matches — **and a fifth attempt that was meant to be the control and was
not.** Reversing the slot ordering killed one, because the fixture asserts the ordered list rather
than its contents. A control has to be a change that cannot matter, and an ordering is not one when
something checks the order. It is 289's lesson from the other end: there a real failure was counted
as a break's, here a real break was labelled a control.

## What is left

* **`9.6`'s puzzle is still open** and now has a shape: three routines told nothing in any argument
  slot, one of which (`0x015B`) is called nowhere else in the game.
* **The eleven.** Which routines they are, and what their other-slot arguments take, is one
  `--special` each and was not done.
* **`0x8005` at sixteen routines** is the second slot and nothing in this project reads it. That is
  the biggest single thing this milestone opens.
* **The window is four commands.** `SpecialCalls.Before` stops at the fourth setvar in front of a
  call, so an argument written five commands early is invisible to all of this.

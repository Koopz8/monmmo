# Milestone 214: the third ceiling is mostly not one

`special 0x0187` heads all three obstacle scripts, so 213 put it next. Reading it took one
hexdump and turned into something bigger: the line `--play` has printed since the routines were
found says **"396 place(s) call 33 routines it could not answer — every one took the zero arm"**,
and for more than half of those places there is no arm.

---

## What `0x0187` is asked

All three obstacle scripts open the same way:

```
  25 87 01              special 0x0187
  21 0D 80 02 00        compare 0x800D, 2
  06 01 E0 7A 1A 08     if equal goto 0x081A7AE0
```

And `0x081A7AE0` is two bytes: `6C 02` — **release, end**. Answer 2 and the obstacle does
nothing at all.

`--routines` has the shape and has always printed it: `0x187 — 376 site(s), 376 branch,
compared against 2x376`. Three hundred and seventy-six calls and the answer is compared against
**one value, 2, every time**.

So the run's silent zero is not a wrong answer with consequences. It is one of every value
except 2, and the file cannot tell it from 3, 4 or 9. At `0x0187` the run behaves exactly as it
would with any answer but one.

## Which makes "it could not answer" three different things

The join has never been made. `--routines` reads the ROM and has never seen an `Attempt`; the
run knows what it asked and nothing about what the file does with the answer. Put together:

```
--play
  396 place(s) call 33 routines it could not answer — and the zero it answered instead is not one thing
       201 of the places, across 24 routine(s): the answer decides nothing
       158 of the places, across  6 routine(s): zero is never the value tested, so it falls
                                                through like any other wrong answer
        37 of the places, across  3 routine(s): tested against zero at some sites and something
                                                else at others
         0 of the places:                       ZERO IS THE VALUE TESTED
```

**Two hundred and one of the three hundred and ninety-six places are calls whose answer nobody
ever looks at.** The routine does something; nothing branches on it. Those places are not a
ceiling in any sense — they are the run stepping over a call whose return value the cartridge
itself ignores.

A hundred and fifty-eight more are compared against a value that is not nought, so the silence
costs nothing that a wrong answer would not have cost.

At the widest lever setting the same split, of 766 places and 63 routines:

```
       186  the answer decides nothing
       433  zero is never the value tested
       145  tested against zero at some sites and something else at others
         2  ZERO IS THE VALUE TESTED — the run's silence TAKES the branch
```

**Two places.** At the floor, none. The part of this ceiling where the run's silence actively
asserts something is two places out of seven hundred and sixty-six, and the headline has been
reporting all of it as one number since milestone 200.

`SpecialCalls.ZeroIsMisleading` has existed for a long time and says this in its own doc
comment — *"a zero is an answer, not an absence"* — but only `--specials` ever asked it, off the
ROM, with no run in sight. The two halves have been in the repository the whole time and never
in the same sentence.

---

## What changed

`SpecialCalls.ZeroAt` joins the run's asked-counts to the file's tested-values and returns one
of four answers per routine: **never tested, a refusal, an assertion, or both**. `--play` prints
it per routine and then as a split of the places.

The rule lives in `SpecialCalls`, not the printer — the ninth time.

Four breaks, four catches:

| break | caught by |
|---|---|
| an answer nobody tests counts as a refusal | the four-way fixture and the no-profile one |
| zero being *among* the tested values makes it an assertion | the mixed routine |
| a refusal is anything that is not an assertion | the mixed routine |
| the order follows the routine number, not what the run asked | the counts test |

One deliberate care: a routine the run asked that has no profile at all reads as **never
tested**, not as an assertion. "Unknown" has to be its own answer rather than being folded into
whichever bucket is nearest — which is the fault 211 and 212 were each caught by, avoided on
purpose this time.

2849 → 2853 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The two places where zero takes the branch.** Two routines, at the widest lever setting,
  and they are the whole of what is left of this ceiling. Neither has been named.
* `0x194` is asked 54 times by the widest run, is compared against 0 and 1, and has **747 sites**
  in the file — the most of anything. Being in the "both" bucket means the run's silence matters
  at some of its sites and not others, and which is which has not been asked.
* `0x0187` answers 2 to mean "do nothing". What makes it answer 2 is compiled code and is not
  readable from here — but `--answer 0x187=2` would measure what the game looks like when every
  obstacle declines, which is a control nobody has run.
* The 201 places whose answer is never looked at are worth removing from the ceiling line
  entirely rather than being reported and then explained away.

# Milestone 201: eight places wide, and worth one MAGIKARP

200 found the third ceiling and did not count it. That was the whole of its owed list, and this
is the number.

---

## Named, and then counted

`--say-yes` and `--boat` are levers: named, printed, switchable. The third gap had none of
those. It arrived at 200 by reading two command widths *correctly* — once the reader could step
over `0x92`, the run walked past every money check in the game and took the arm where the thing
is handed over, with a purse of nought.

200 said so in prose. Prose is not a denominator.

```
--play                                       1 place(s) asked it for money and it answered
                                             neither way — and nothing changed hands on the
                                             far side of one

--play --say-yes                             8 place(s) asked
--play --say-yes --in-order                  8
--play --say-yes --boat                      8
--play --say-yes --boat --in-order           8
--play --say-yes --boat --surf --in-order    8

  16.0 0x0816F75F wanted 500 and handed over #129 at level 5 ANYWAY — this is above the floor
```

**Eight places wide, and worth exactly one Pokémon.**

`#129` at level 5, for 500, at `16.0`. That is the fifth party member 200 found — it is the
`#130` at level 71 the run ends with, the same creature after it grew up. A run whose purse is
nought, standing at counters being refused four things it cannot afford, walked away with it.

And the floor is clean: one place asks, nothing comes of it. **The floor's party of six is
earned; the party of five at the other five settings is four earned and one not.**

---

## Two numbers, because they are two claims

How **wide** the gap is and what the gap is **worth** are different questions, and either can be
nought while the other is not:

* Eight asked, nothing given — the reading has got that far and nothing is riding on it.
* One asked, one given — narrow and load-bearing.
* Nought asked — the run never met a money check at all, which is a fact about the walk.

A single number cannot say which of those a run is in. Both are printed, and the second prints
`and nothing changed hands on the far side of one, so nothing it is carrying is unpaid for`
when it is empty, because an absent line and a nought line read identically and this project has
written that down four times.

---

## Where it lives

`ScriptRunner` records the amount when it steps over `0x91` or `0x92` — it does not answer, it
does not model a purse, and it changes no control flow. The only new thing is that the
walking-past is counted. `HowAScriptRuns` carries it through the call chain, `Attempt` folds it
to **places** rather than times, and the printer says both halves.

Deliberately not done: a lever. `--money N` already exists and is MODELLED, and pointing it at
this would be modelling a purse to answer a question the cartridge answers with a payout table
nobody has located. The honest state is *measured gap, no decision*, and that is what shipped.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the walking-past stops being counted | `ARealMoneyCheckInRealBytesIsWhatProducesIt` |
| every handover counts as unpaid, not just those behind a check | three of the five |
| the two halves collapse into one number | `OnlyThePlacesThatHandedSomethingOverAreWorthAnything` |

None came back green. The fixture is three people on two maps — one asked and gives, one asked
and gives nothing, one gives having never been asked — because the middle one is the entire
discrimination and the third is 195's ordinary case asserted in advance.

**And two of the five tests read the bytes rather than the plumbing.** The other three hand the
runner its answer ready-made, which is exactly the forgiving shape 189 was caught by: a stand-in
that guards the pipe and not the thing. `ARealMoneyCheckInRealBytesIsWhatProducesIt` puts
`92 F4 01 00 00 00 02` in an image and asks for 500 back; its partner puts a `setflag` there and
asks for nothing back.

2791 → 2796 tests, all green.

---

## What is still owed

* **The lever, if one is wanted.** The gap is now measured and it is one Pokémon wide. Whether
  that is worth a `--pay` lever or a located payout table is a decision, and it should be made
  against this number rather than against the worry.
* `0x95` at `0x0816A43E` and `0xC2` at `0x0816CDB6` — still the next two in the queue.
* The eight places are counted and not listed. Seven of them gave nothing on this run, and
  which seven is not printed — the same shape as the five wall flags that "look moved and are
  not".
* Nothing checks that `16.0 0x0816F75F` is the MAGIKARP salesman rather than something else
  asking 500. The price and the species are READ; the identification is not made and is not
  needed.

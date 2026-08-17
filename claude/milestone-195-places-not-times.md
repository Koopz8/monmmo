# Milestone 195: places, not times

Two numbers say how much of a run was decided by something this project cannot read:

```
  5051 calls to 28 routines it could not answer — every one took the zero arm
  399 script run(s) stopped at 3 command(s) this project has no width for
```

They are the error bars. Both counted every run of every script **on every pass**, and the run
is a fixpoint that talks to everybody again each time round.

```
  the four counts below are PLACES and not times: the run asked 5047 / 399 / 223 / 21 times,
  and a fixpoint asks again on every pass.

  319 place(s) call 33 routines it could not answer — every one took the zero arm
  40 place(s) stopped at 3 command(s) this project has no width for
```

**5047 asks at 319 places. 399 stops at 40.** Both numbers are true and they answer different
questions, and only one of them is about the cartridge. The other is about how many times the
loop went round, which is a fact about this repository.

The same correction applies to the two smaller counters the run keeps: the yes-or-nos it stopped
at, and the things it asked for and was refused.

---

## And the prediction was wrong

193 found that this cartridge writes one scene as several entry stubs and that a fixpoint takes
every door. 194 counted them: **22 scenes, 38 runs that are a scene already played.** From that
it followed — and 194's own next-task line said so — that every number counted per script is
inflated by however many doors a scene has.

It is not.

```
  6 of the folding was a scene arriving by another DOOR rather than on another pass
```

Six, out of five thousand and forty-seven. The passes are the whole of it.

**The door shape matters where an effect ACCUMULATES, and not where something is merely
counted.** A person walked once per door ends up four squares from where one walk would leave
them, and that was worth nine maps at 193. A counter that says *how many times* accumulates
nothing: the same scene arriving twice adds one to a number that was already five thousand.

The two kinds of folding are counted apart on purpose. A single number could not have said this.

---

## What this changes

Nothing the run reaches — 183, 243, 381 at every lever setting, and no flag moved. What changes
is what four of its numbers mean, and the labels now say which:

| | before | after |
|---|---|---|
| routines it could not answer, floor run | 5051 calls | **319 places**, asked 5047 times |
| commands with no width, floor run | 399 runs | **40 places**, stopped 399 times |
| routines, `--boat --in-order` | 4863 calls | **601 places** |
| commands with no width, `--boat --in-order` | 69 runs | **12 places** |

The old numbers are still printed, beside the new ones, because a count with no denominator is
the trap this project has written down twice and fallen into a third time.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| places are counted per run again | `OnePlaceAskedOnEveryPassIsOnePlaceAndSeveralTimes`, and one more |
| door folding and pass folding are one number | `OnePlaceAskedOnEveryPassIsOnePlaceAndSeveralTimes` |
| the raw ask count is the place count | two of them |

The second came back **green** first time. Nothing asserted the ordinary case — the same script
on a later pass, which is 5041 of the 5047 — is counted apart from a door. That is the entire
finding of this milestone, and it was not written down anywhere a break could reach.

---

## What is still owed

* `--entries` reads only the scripts the map scan opens, which is 0.6% of the file.
* **12 blocks are reached from more than one map.** Anything keyed on a script address alone is
  wrong about them, the way 193 was. `ran` in the Autoplayer is still keyed that way, and it is
  what `--flags` uses to decide whether a script ran at all.
* The refusals and the yes-or-nos are corrected here but nobody has read what they now say.

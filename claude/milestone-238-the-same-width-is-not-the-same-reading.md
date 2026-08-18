# Milestone 238: the same width is not the same reading

237 left `0x9D` on the list because three of them run before the field effect on `10.14` saying
`0, 255` / `1, 10` / `2, 14`. That reads like filling three slots, which is a guess with an
obvious test: **is the first byte an index?**

Two other unnamed commands have the same width — a byte, then a word — so the question was asked
of all three.

---

## The answers, and two of them are "cannot say"

```
  0x9D — 9 byte position(s) in 5 run(s); the first byte takes 3 distinct value(s)
      EVERY run counts 0, 1, 2 … from nought — drawing each byte from the 3 value(s) it
      uses, that is one in 19683
      the word: 7 distinct across 9 place(s); 3 of them a variable id

        0x081652C4  0, 1 ; 1, 56 ; 2, 2
        0x0816C988  0, 255 ; 1, 10 ; 2, 14
        0x081BDF39  0, 0x800D
        0x081BE032  0, 0x800D
        0x081BE14C  0, 0x800D

  0x7F — 3 byte position(s) in 3 run(s); the first byte takes 1 distinct value
      its byte is the same value at every place, so counting from nought says NOTHING
      here — the question cannot be answered, not answered yes
      the word: 1 distinct across 3 place(s); 3 of them a variable id

  0x82 — 7 byte position(s) in 7 run(s); the first byte takes 1 distinct value
      its byte is the same value at every place, so counting from nought says NOTHING
      the word: 7 distinct across 7 place(s); 0 of them a variable id
```

**`0x9D`'s byte is an index.** Two runs of three counting 0, 1, 2 and three runs of one at 0 —
nine positions, every one holding what its place in the run implies. Drawing each from the three
values that byte actually takes, one in 3⁹.

**`0x7F`'s and `0x82`'s cannot be answered at all**, and that is the part worth building. A byte
that only ever takes one value counts from nought whenever that value happens to be nought, so
`0x7F` "counts" at all three of its places and the floor comes out at **one in one**. Reported as
a yes it is a finding made of nothing; the instrument says the question is unanswerable instead,
which is the same distinction 226 drew between what a command takes and what it does.

`0x82` is the sharper case: its byte is **1** at all seven places. Under an index reading that
would be a command that only ever fills slot one and never slot nought. It is not an index; it is
a constant, and the ordinary argument-column test says what it does have — **seven distinct words
across seven places**, none of them a variable id.

## Which is trap 11 again, from a new side

*A shape that matters somewhere does not matter everywhere.* Three commands, one width, and
bundling them would have carried `0x9D`'s answer onto two commands the evidence says nothing
about. `--slots N[,N]` asks it of any command separately and can come back empty, which is the
only way an instrument like this is worth having.

Free from the same reading and not claimed: the three `0x9D` runs of one are the three obstacle
scripts — CUT, ROCK SMASH and STRENGTH — and all three say `0, 0x800D`, the answer variable the
`findmove` compare above them just used. `0x7F`'s three places say the same thing. And two of
`0x82`'s seven sit in the CUT and ROCK SMASH blocks holding **15** and **249**, which are those
two moves' own ids. Two of two is not a column and this milestone does not build on it.

## The breaks

Four, four catches:

| break | what went red |
|---|---|
| most of the run counting is enough | `EveryElementOrItIsNotCounting` |
| a one-value byte is answered yes instead of unanswerable | `OneValueMeansTheQuestionCannotBeAnswered` |
| the floor is over the runs instead of the places | `TheFloorIsOverEveryPlaceAndNotEveryRun` |
| a run keeps going past a different command | `SomethingElseBetweenThemMakesThemTwoRuns` |

The second is the one this milestone exists for. The third matters because two runs of three is
six bytes that all have to be right, not two runs that do — `3⁶ = 729` against `3² = 9`, and the
whole-cartridge number is 3⁹ rather than 3⁵.

**The flaky one went red again**, on break 4's run: `ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`,
on a 56-second suite against the usual 20. That is the second time in this session, both on slow
runs, both green on a re-run. It is doing exactly what the prompt says it does.

3001 → 3009 tests, all green. **Nothing the run does changed.**

---

## What is still owed

* **What `0x9D` indexes into.** That the byte is a slot number is read; what the slots are for is
  not, and this milestone does not guess.
* **`0x82`'s seven words** — 58, 231, 85, 247, 53 in one region of five, and 15 and 249 in the CUT
  and ROCK SMASH blocks.
* **`0x011E`'s answer**, still behind `[0x89]`, measured and declined at 237.
* **`0x194`'s nineteen doors** on TRAINER TOWER (236).
* **The three numbers nothing computes** (231).
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.

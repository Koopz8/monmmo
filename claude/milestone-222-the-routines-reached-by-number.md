# Milestone 222: the routines reached by number

221 ended with ten sites whose verdict was *not said*, and seven of them were behind a `callstd`
— a call whose address is not in the command. It is an entry in a table, the table has never been
found, and this project has never read a standard routine.

The table hunt failed. The question got answered anyway.

---

## What the maps ask for

```
  5457 asking(s) at 2719 place(s), of 9 number(s), highest 0x09
    callstd 0x04 — 3263 time(s) at 1192 place(s)
    callstd 0x06 —  666 time(s) at  666 place(s)
    ...
    callstd 0x08 —    1 time(s) at    1 place(s)
```

Nine numbers, `0x00` to `0x09` with `0x07` never asked for. So the table has at least ten entries.

## The hunt, and why it failed

```
  runs of 10+ consecutive pointers that all land on something reading as a script:
    24 in this file
     0 in the same file REVERSED  <- the floor
```

The shape is real — nought in the reversal. It also identifies nothing: **twenty-four candidates
and no way to choose between them.** The reason is worth writing down, because it is about an
instrument this whole project leans on:

```
    0x081D6858 — 10 pointer(s), 10 distinct in the first 10
      [0] 0x081D8098   00 02 03 CA 09 0A ...
      [1] 0x081D80A9   00 02 39 20 00 CC ...
```

`00 02` is `nop ; end`. **A pointer to two bytes of nothing passes "reads as a script"**, and most
of the twenty-four candidates are runs of pointers into exactly that. The filter is weak on
purpose — it raises a floor and is never evidence on its own — and this is the shape of how weak.
It is asserted in a test now rather than being a thing one discovers.

## What the callers say instead

The table was wanted to answer one question: does a `callstd` put anything in the answer
variable? That is answerable from the other end.

**If a script says `callstd N ; compare 0x800D ; if` and nothing in front of it could have
answered, the compare is reading what `N` left — whatever `N` turns out to be.** A compare has to
be reading something, and where nothing else wrote the variable there is one candidate.

```
  and which of them ANSWER, read from the callers rather than from the table:
    callstd 0x05 — 416 site(s) at 119 place(s) put a compare on the answer variable straight after
        152 with NOTHING before that could have answered   <- so this one answers
    callstd 0x00 —  17 site(s) at  17 place(s)
          2 with NOTHING before that could have answered   <- so this one answers
```

**`callstd 0x05` answers, and so does `callstd 0x00`.** The walk back is 219's, and this is the
first thing outside its own milestone to use it.

Not circular, and the direction matters: the verdict is derived **only** from sites where nothing
else could have answered, and applied to sites where something else could. The twelve sites 221
could not resolve are all of the second kind — a `special`, then a `loadpointer`, then
`callstd 0x05`, then the compare. `0x05` writes the variable, so it clobbered the routine's
answer, and the compare was never the routine's.

## The verdicts, corrected

```
  before   30 somebody else answered      10 not said
  after    37 somebody else answered       3 not said
```

Seven of `0x0188`'s sites move. That is the routine 215 called the last of the run's ceiling, on
the strength of one clean site; its other ten are now seven that belong to a standard routine,
three still behind a block that jumps away.

---

## What changed

* `StandardRoutines` — what the maps ask for, which numbers answer (read from the callers), and
  the table hunt with its floor. The hunt is kept because a negative with a floor beside it is a
  result.
* `WhoTheCompareBelongsTo` distinguishes a standard routine the callers pin down from one they do
  not, and only the first takes a compare away.
* `ScriptReader.ReadsAsAScript` — **one copy.** There were two, in `TheCoinCase` and
  `EverywhereInTheImage`, and a third was about to be written for this milestone. That is the
  second sweep-wide duplicate folded in two milestones, after 221's five copies of the script
  list.

Six breaks, six catches. **Two came back green first, both for the same reason as 221's:** the
rules — *which sites can say anything* and *which sites prove it* — lived inside a function that
needs a whole cartridge to run, so no fixture could reach them. Extracted as
`AsksTheQuestionHere` and `ProvesItAnswers`, each break fails exactly one test.

That is three milestones running where a green break meant the rule was in the wrong place. The
project's own note says to suspect the fixture first; on this evidence the first question should
be *where does this rule live*.

2903 → 2912 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The table is still not found.** Nine numbers are asked for and two of them are now known to
  answer; nothing is known about what any of them *do*. A stronger filter than "ends the way
  blocks end" would make the hunt worth re-running — the obvious one is requiring the entries to
  be distinct and longer than a `nop` and an `end`, and it has not been tried.
* **`callstd 0x05`'s 250 "not said" sites.** The walk back stopped at something for those. They
  do not change the verdict — 152 clean sites already settle it — but they are 250 places where
  219's walk gives up, and nobody has looked at what it is giving up on.
* **`0x0188`'s remaining three**, behind a block that jumps away.
* **The 411 places.** Still the largest unasked question: every routine sentence in this project
  quoted sites.
* `0x081A77B0`, `0x0153`, and everything owed at 215, 216, 219, 220 and 221 stands.

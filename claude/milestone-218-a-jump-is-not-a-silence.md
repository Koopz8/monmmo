# Milestone 218: a jump is not a silence

217 ended with two owed lists: fifty-seven places whose called block "returns one or nought
depending on a routine", and forty-nine whose call "leaves the answer alone". This reads the
first and, in doing so, finds that nine of the second were never that at all.

---

## What those blocks return

```
  57 place(s) call a block whose STRAIGHT LINE ends by saying the answer out loud — 1 — but an
  arm of the same block asks a routine, so they are NOT constants.
    Read one level of arms, those blocks return:
      0x081BB79C at 38 place(s) — leaves 0 or 1; the choice turns on 0x083, 0x153
      0x081BBB1E at 19 place(s) — leaves 1 or whatever is left by a jump not followed here;
                                  the choice turns on 0x084, 0x153
```

**Two blocks.** Fifty-seven places call one of them, and each is a yes-or-no whose answer turns
on two routines in sequence — `0x083` then `0x153`, or `0x084` then `0x153`. Those are 216's two
routines, the ones asked once and twice by the run and carrying thirty-nine of the ceiling's
forty-four branches. They are subroutines, and this is what they return.

`0x0153` is in both, and 216 put it on the owed list as "a second unanswerable routine inside the
first, counted nowhere". It is counted now: it is half of the decision at every one of the
fifty-seven.

## And the nine

The two blocks do not answer symmetrically, and the asymmetry is the finding.

`0x081BB79C`'s arm ends `setvar 0x800D, 0 ; return` — nought. `0x081BBB1E`'s arm ends
`loadpointer ; callstd ; goto 0x081A77B0` — it **jumps away**, and the reading stops at a goto
because a goto ends a block.

The instrument reported that as `Nothing`: *the call left the answer variable alone.* It did no
such thing. It went somewhere this reading does not follow, and those two are different facts —
one is about the cartridge and the other is about the instrument.

That is the fourth time in four milestones that "nothing found" has been standing in for "did not
look", and it moved a headline:

```
  before   49 leave NOTHING — the compare reads whatever was there before the call
  after    40 leave NOTHING
            9 leave nothing, and the block ends by jumping somewhere this reading does not follow
```

**Nine of the forty-nine were the reading stopping.** Forty genuinely leave the variable alone.

---

## What changed

* `SpecialCalls.Returns` reads a called block's straight line **and one level of its arms**, and
  returns every distinct outcome with the routines the choice turns on. The decider is whatever
  was asked **last** before the branch, not the first thing in the block.
* `LeftBehind.WentSomewhereElse` separates a jump from a silence.
* One level and no further, written into the rule: an arm that branches again is read for what
  its own straight line leaves, and its arms are not followed. Each level is another place to be
  wrong, and this project has been caught by exactly that at 214, 216, 217 and now here.

Four breaks, four catches — a jump is a silence, the arms are not read, the decider is the first
rather than the last, and everything asked is a decider whether or not anything branches on it.

2873 → 2877 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The forty that really do leave the answer alone.** The compare after them reads an older
  answer, and finding whose means walking back past the call in the caller — which 214's barrier
  deliberately stops. Still a third instrument, and still not built.
* `0x081A77B0` is where the jumping arm goes, from nineteen places. Following it is one level
  further and the rule says not to; a separate reading of that block would say what the
  nineteen actually get.
* `0x0153` is asked inside both blocks and is half of every one of the fifty-seven decisions.
  What it is asked about — its own sites, its own compares — has not been looked at.
* Everything owed at 215 and 216 stands.

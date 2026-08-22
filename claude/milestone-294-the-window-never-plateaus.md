# Milestone 294: the window never plateaus

292 and 293 both ended on the same line: *the window is four commands, so an argument written five
commands before a call is invisible to every number here.* This sweeps it.

Four is not enough, four is not a reading, and the number that rests on it is a setting.

---

## It is still climbing at twenty-four

```
      window   handed a value   in 0x8004   only elsewhere   selectors   arguments
           1               30          18               12           1        1143   0x0194
           2               35          24               11           2        1897   0x00A3,0x0194
           3               40          29               11           2        2134   0x00A3,0x0194
           4               44          33               11           1        2204   0x0194   <- everything above is measured here
           6               49          38               11           1        2326   0x0194
           8               52          42               10           1        2485   0x0194
          12               57          47               10           2        3114   0x00A4,0x0194
          16               59          48               11           2        3274   0x00A4,0x0194
          24               62          48               14           2        3821   0x00A4,0x0194
```

**Nothing plateaus.** 292's "44 of the 178 routines are handed a value in an argument slot" is 30 at
a window of one and 62 at twenty-four, and twenty-four is not the end of it. The number describes
`SpecialCalls.Before`'s constant at least as much as it describes the cartridge.

It climbs slowly rather than swallowing the script because the loop stops at the first command that
is not byte-contiguous with the next, so widening only ever reaches further inside one packed run.
That is a real bound and it is not the window.

## And 293's reading is stronger than 293 could say

**`0x194` is the only selector at every window from 1 to 24.** 293 said "the only one" having
measured at four, which is where it happens to be true; this says it at nine settings including the
two either side, which is a different and better claim.

The two that come and go are the warning:

* **`0x0A3`** is a selector at windows two and three and stops being one at four — a *wider* window
  removed it, because the last value in the slot within reach changed.
* **`0x0A4`** becomes one at twelve and stays.

Neither is a fact about the cartridge. **A finding that appears at one setting of a knob and
vanishes at the next is a fact about the knob**, which is 278's cut with a different name.

## So the window is MODELLED

There is no measurement here that picks a number. Widening it keeps finding more `setvar`s, and a
`setvar` twenty commands before a call is not obviously an argument to it — the further back you
reach the more you collect and the less each one means. Four was chosen; nothing has ever defended
it and this cannot either.

It is marked MODELLED now, and every number that rests on it inherits that: 292's 44 / 33 / 11, and
291's "22 routines are called with more than one value".

## Two breaks came back green and only one was the guard's fault

| break | killed |
|---|---|
| `Before` uses the constant rather than the window it was handed | **2** |
| the adjacency check skips gaps instead of stopping at them | **0**, then **1** |
| `All` drops the window on the way to `In` | **0 — unguarded** |
| **CONTROL:** the default changed from 4 to 5 | **0** |

**The gap fixture did not test a gap.** It put the value in a block the read never reached, so it
asserted that an unread command is not found — true, and nothing to do with adjacency. Rewritten
with a `goto` between the two blocks, so both are in the command list and only the adjacency check
separates them, it kills. That is 119 for the fourth time in six milestones.

**The `All` threading is genuinely unguarded** and the break says so rather than my noticing it.
`SpecialCalls.In` is split out precisely so a test can hand it a few bytes — its own comment says
*"a rule only reachable through one is a rule no test reaches"* — and every fixture here uses `In`,
so the one line that passes the window through `All` has nothing on it. Left as it is and written
down, because the alternative is a fixture that needs a whole cartridge.

**And the control is a control for a reason worth stating.** Changing the default from 4 to 5 kills
nothing because the fixtures are written against `SpecialCalls.Window` rather than against 4. That
is deliberate: 294's whole finding is that the value is modelled, and a fixture pinning it would be
a test asserting a guess.

## What is left

* **A principled bound.** Distance is the wrong criterion; "nothing between the `setvar` and the
  call reads or overwrites that slot" is a better one and is not implemented.
* **`0x0A4`**, which becomes a selector at twelve. Either it is one and the default hides it, or the
  wider window is finding a `setvar` that is not its argument. Nothing here separates those.
* **The forward window** is the same constant and is used for what a call's answer is compared
  against. This sweeps the backward half only.
* **Every "N of 178" in this prompt** now carries a hidden parameter. They are marked.

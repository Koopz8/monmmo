# Milestone 295: two rules that replaced a knob

294 ended with the thing it could not supply: *distance is the wrong criterion; a principled bound
is not implemented.* This implements it, and the reading moves for the third time in four
milestones.

---

## Contiguity alone converges, and it converges high

294 swept the window to 24 and reported it still climbing. Swept further:

```
          24               62          48               14           2        3821
          48               62          49               13           2        4290
          96               62          49               13           2        4290
        4096               62          49               13           2        4290
```

**It converges at 48.** So contiguity does bound it after all, and 294's "still climbing" was itself
an under-read — the sweep stopped one row short of its own answer. With the distance removed the
number is **62 of 178**, not 292's 44.

## And then the rule that makes the knob stop mattering

A value put in an argument slot belongs to the **first call after it**, not to every call that
follows. 236's own note is the shape: the FAN CLUB on `14.9` sets `0x8004` and asks `0x0A3` eight
times over, and without this rule the eighth call collects all eight fans.

It is a rule read off the script rather than a distance chosen in this repository. With it:

```
      window   handed a value   in 0x8004   only elsewhere   selectors   arguments
           1               30          18               12           1        1143   0x0194
           4               37          29                8           1        2132   0x0194
           8               38          29                9           1        2201   0x0194
          12               39          30                9           1        2205   0x0194
          24               39          30                9           1        2205   0x0194
        4096               39          30                9           1        2205   0x0194
```

**Converges at twelve and stops.** The default is `NoLimit` now — the backward search is bounded by
two READ rules and by nothing chosen.

> **39 routines are handed a value in an argument slot, 30 of them in `0x8004`, and 9 only
> somewhere else.**

That is 292's 44 / 33 / 11 and 294's 62 / 49 / 13, corrected. Three readings of one number in four
milestones, and this is the first one that does not rest on a chosen constant.

## And both flickering selectors are gone

294 found `0x0A3` a selector at windows two and three, and `0x0A4` at twelve and above, while
`0x194` was one everywhere. Under the previous-call rule **`0x194` is the only selector at every
window in the sweep, and the other two never appear at all.**

That is the confirmation 294 could only guess at: they were artefacts of reaching past a call and
collecting somebody else's argument. A knob whose settings changed the answer stopped changing it
once the rule underneath was right.

## Four greens, and only one of them was a control

| break | killed |
|---|---|
| the barrier names `special` and not `specialvar` | **0**, then **1** |
| the barrier removed entirely | **1** |
| `In` defaults to a distance again | **1** |
| the window boundary off by one | **0**, then **1** |
| arguments handed back in the other order | **0**, then **1** |
| **CONTROL:** the slot range written as two comparisons | **0** |

**Three of the six needed a fixture written before they could kill.**

* The barrier fixture used `special` at both calls, so a version naming one of the two forms passed
  it. **Fifth costume of 119 in this session.**
* The window boundary — the axis of 294's whole table — was pinned by nothing, so an off-by-one
  shifted every row and no test noticed.
* The ORDER `Before` hands arguments back in decides which of two values in one slot wins.
  `ArgumentOf` has a fixture for last-wins and it builds the call by hand, so the order was
  reachable only through a path no test took.

The third is worth its own line: **a rule can be guarded and still be unguarded through the route
that actually runs it.** `SpecialCalls.In`'s own comment says a rule only reachable through `All`
is a rule no test reaches; this is the same sentence one level in.

## What is left

* **Nothing between reads the slot.** The barrier is the previous CALL; a plain command that
  consumes `0x8004` between the `setvar` and the call would take the value too, and this does not
  check for that. It is the next refinement and the numbers can only come down.
* **The forward window is still four** and still chosen. It bounds what a call's answer is compared
  against, which is what every "compared against" column in 291-295 rests on.
* **`All`'s threading is still unguarded** (294) and this did not fix it.
* **62 versus 39** is the size of the previous-call rule: twenty-three routines were being handed
  somebody else's argument.

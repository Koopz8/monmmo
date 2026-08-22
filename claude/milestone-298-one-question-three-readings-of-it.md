# Milestone 298: one question, three readings of it

297 named the cheapest thing owed: *the forward window is still four and still chosen. Everything
"compared against" in 291-297 rests on it and it has never been swept.* Sweeping it took ten
minutes and the answer was the opposite of 294's. Then grepping for the constant found five more
of it.

---

## The forward window plateaus, where the backward one never did

294 swept the backward half and it climbed all the way to twenty-four, which is what made 292's
headline a property of a constant. The forward half does not:

```
      forward   places compared   compares   routines   selectors   branch places
            1               426       1107         42           1             426
            2               434       1115         46           1             426
            3               437       1134         48           1             426
            4               437       1137         48           1             426   <- 291-297
            8               437       1156         48           1             426
           16               437       1159         48           1             426
         4096               437       1159         48           1             426
         none               437       1159         48           1             426
```

Places compared, routines and branch places are flat from **three**, and the SELECTOR count — the
thing 291, 292, 293, 296 and 297 all rest on — is flat from a window of **one**. The forward half
was already bounded by rules read off the script (the `Answering` barrier, contiguity, and the
answer variable being overwritten), so the distance was deciding nothing.

**It is gone.** `NoLimit`, which is what 295 did for the other half, and the only number that moves
at all is the raw compare count — 1137 to 1159, and no sentence anywhere rests on it. Diffed over
the whole of `--routines`: **nought lines change.**

## And there are five more of that four

`grep -rn "const int Window" src/` finds **six** declarations of `4` in the script and sound
readings, plus a `Nearby = 4`. Three of them are the same question:

| where | which way | its barriers |
|---|---|---|
| `SpecialCalls.ArgumentsBefore` | back | the previous call (295), a slot something spent (296), contiguity |
| `SpecialContracts.Arguments` | back | contiguity, and a distance of four |
| `WhatIsWaitedFor.SelectorBefore` | back | stops at the first command that is not a `setvar` |

**295 and 296 replaced the distance in the first and never touched the other two.** So `--routines`
printed **37** routines handed a value in one section and named **44** in the column below it, in
one output, and nothing compared them. That is 220's rule and 224's together — *a rule fixed in one
arm and left standing in the other*, and *five private copies disagree and can be caught by
comparing them* — and the prompt has carried both warnings for seventy milestones.

Asked of the same 936 places:

```
      the reading that was there                           places   agree   it said more   the rules did
      SpecialContracts.Arguments (how many slots)              936     893             39               4
      WhatIsWaitedFor.SelectorBefore (what 0x8004 held)        936     923              0              13
```

**The two miss in OPPOSITE directions**, which is why comparing them to each other would have
caught this and comparing either to nothing did not. The contract count credits values a slot had
already been spent on; the selector reading stops dead at anything that is not a `setvar` and loses
values nothing touched.

The cartridge settles which is right. `2.1` at `0x1C510D`:

```
  1C510D  16 04 80 01 00     setvar 0x8004, 1
  1C5112  16 05 80 02 00     setvar 0x8005, 2
  1C5117  19 06 80 03 40     copyvar 0x8006, 0x4003
  1C511C  25 94 01           special 0x0194
```

The old selector reading meets the `copyvar` and reports a call handed **nothing**, three commands
after it was handed a one. Nothing in this cartridge empties a variable, and 297's own reading is
about that very `copyvar`. **Thirteen places in the game are that shape**, and four of them are the
lifts 297 read.

Both are the shared reading now. Both old ones are kept, take their own window, and are printed
beside the corrected numbers, so the size of the correction stays in the output rather than in a
commit message.

**And 236's headline survives it**: `0 of 93` askings waited for at some places and not others,
against a chance of 26.3 — where it was 0 of 95 against 26.6. The total pairs went UP (269 -> 277)
while the multi-place ones went DOWN (95 -> 93), which is trap 7 again: a fix does not have to move
a number upwards.

## The column this cannot read

**13 of the 244 places credited with a value have a `call` or a branch standing between the value
and the call.** 214 made a plain `call` a barrier in the ANSWER scan, because the block it jumps
into can answer; the ARGUMENT scan has no such barrier, and a called block can write an argument
slot exactly the same way. Printed beside the count the reading is sure of (47), because a verdict
is worth what its does-not-know column is small.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the contract reading goes back to its own copy | 1 | **1** |
| the selector reading goes back to its own copy | 2 | **2** |
| the forward half takes the old distance again | 1 | **0**, then **1** |
| the forward boundary is off by one | 1 | **0**, then **1** |
| **CONTROL:** the leaves list in another order | **0** | **0** |

**Two green breaks, and both were lines nothing reaches** — 219's rule for the third time in this
repository. `After`'s forward default was dead code, because `In` always passes one; it is deleted
and the default that decides is `In`'s. And the forward boundary **can never bind at `NoLimit`**,
where the comparison is against four thousand million, so an off-by-one there was invisible to
every fixture. The fixture asks at an exact distance now: found at six commands past the call, not
found at five.

There was a third fixture fault on the way, and it is 297's, repeated within one milestone: the
first version pinned the contract reading on a run where the two readings **agree** — a `copyvar`
between two `setvar`s, which the crude reading walks past and counts. It is pinned on the run where
they disagree now.

## What is left

* **Four more copies of the run AFTER a call**: `SpecialContracts.ComparedAfter`,
  `WhoTheCompareBelongsTo`, `DaycareLocator`, and `SpecialCalls.After`. `DaycareLocator`'s cruder
  barrier list — only a call, where `SpecialCalls` has eight commands — is worth **nought** on this
  cartridge: 936 of 936 places agree. The other two are unmeasured.
* **`BattleMusicLocator.Window` and `Ferries.Nearby`** are the same number in another domain and
  have never been swept either.
* **A `call` between a value and the call it is credited to.** 13 of 244, and closing it means
  following a `call` one level in the argument direction — which `--through-a-call` already does in
  the answer direction.
* **`All`'s threading is still unguarded** (294, 296, 297). Every fixture reaches `In`.

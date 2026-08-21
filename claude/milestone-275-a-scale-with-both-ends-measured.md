# Milestone 275: a scale with both ends measured

274 left one thing owed: *a band at the size actually being bounded*. Going after it found that the
size was never the problem. **268's bound had never been run on a population whose answer was known
before the arithmetic**, and handed a group that is half real script by construction it reads
**nought**.

Two faults and one instrument.

---

## The first fault: "consecutive" meant whatever the set enumerated

273 built `SamplingBand` out of **consecutive** groups and wrote its justification into the code:

> Consecutive groups are, if anything, the conservative choice — blocks near each other in the file
> are more alike, so a group of neighbours is FARTHER from the whole than a scattered sample would
> be, and the band this returns is wider than the true one.

That argument is about the **cartridge**. Every caller reached the function with a list built out of
a `HashSet` or a dictionary, so consecutive meant *consecutive in whatever order the set
enumerated*, and the band was NARROWER than the documentation claimed — the unsafe direction. It is
trap 18 again: the rule and its violation in the same file, written in the same commit.

Measured, at groups of 114:

```
    population                       groups of 114, set order          in FILE order
    the maps' own scripts                        0.163..0.425           0.156..0.703
    outside, named ALONE                         0.215..0.704           0.215..0.693
    outside, named IN A TABLE                    0.255..0.475           0.248..0.497
    the reversed image                           0.226..0.506           0.231..0.502
```

The maps' own band's top nearly doubles. So the numbers move, and **273's verdict does not**: its 38
unnamed boundary sites now sit against a file-order band of `0.257..0.417` (was `0.220..0.360`) and
the reversal's of `0.362..0.754` (was `0.333..0.822`) — 0.601 is still outside the first and 0.373
still inside the second. *These are the reversal's kind* stands, re-run rather than assumed.

274's own numbers do move: its own-quarters column goes `0.086..0.167` to `0.097..0.229`, and its
"give the band back" correction goes 26.6% to **35.3%** and 25.3% to **34.0%**. Which matters less
than it looks, because of the next part.

## The second fault: the bound puts real script at nought and it is not there

`HowMuchCouldBeReal` is `1 - d(mixed, real) / d(junk, real)`. That divides by the distance to junk
and so **places real script at distance NOUGHT from the reference**. It is not there. The reference
is a *sample* of real script and so is anything scored against it, and the two halves of the maps'
own scripts sit **0.178** apart.

So the bound has been read off a scale whose top mark nobody ever checked, and 274's correction —
subtracting a band from the numerator — was correcting the wrong thing.

`--the-ruler` marks both ends. A **held-out half** of the maps' own scripts must read 100%; a
held-out half of the junk model must read 0%; and between them, **mixtures this command makes
itself** out of known-real and known-junk blocks in a share it chose.

```
      population                     distance  268's bound  between the ends   off by   groups
        0% real, 0 + 972                0.735        0.0 %             0.0 %    0.0 %   3
       25% real, 243 + 729              0.571        0.0 %            29.5 %   +4.5 %   4
       50% real, 486 + 486              0.428        0.0 %            55.1 %   +5.1 %   4
       75% real, 729 + 243              0.194       45.2 %            97.0 %  +22.0 %   2
      100% real, 972 + 0                0.178       49.9 %           100.0 %    0.0 %   2
      outside, named ALONE              0.680        0.0 %             9.9 %        -   2
      outside, named IN A TABLE         0.684        0.0 %             9.3 %        -   2
```

**268's bound reads 0.0% on a group that is HALF real script**, and 0.0% again at 25%. It first
moves at 75%. A quantity that reads "at most nothing" about a population that is half the real thing
is not an upper bound on anything — and sampling noise pushes it DOWN, so the direction of its error
is the direction that makes it look decisive.

Read between the two measured ends the same rows give 29.5 / 55.1 / 97.0 for 25 / 50 / 75, and the
outside populations read **9.9%** and **9.3%** — against 268's 3.1% and 1.8%.

## The junk model, as a population rather than a count

The bound's own documentation says it is only as good as what stands in for junk, and the reversed
image is the one control 268 showed to be **blind** here — a table reversed is still a table — and it
supplies 456 blocks. The nudge (269) is this project's control for anything that follows a pointer
and it has only ever produced a COUNT. `EveryScriptInTheImage.Nudged` is the same loop
`NudgedFloor` now counts, returning the population.

```
    junk model                entries   blocks  of the maps' own  from the maps' own  from the reversal
    the nudge, +4 byte(s)        6875     6993              1189               0.584              0.273
    the nudge, +64 byte(s)       7541     7574              1240               0.580              0.252
    the nudge, +1024 byte(s)     7235     7201              1156               0.662              0.309
    the nudge, +4096 byte(s)     7129     7158              1096               0.732              0.370
    the reversed image              -      456                 0               0.711                  -
```

**The reversal sits 0.711 from real script where this file's own accidents sit 0.584.** And the
outside populations sit at 0.680 — *farther from real script than a real pointer aimed four bytes
wrong*.

The junk model was then chosen the way 79 says a filter must be — by the calibration, not by the
answer — and **the calibration cannot choose**:

```
    junk model                worst mixture miss   outside, named ALONE   outside, named IN A TABLE
    the nudge, +4 byte(s)                 22.0 %     9.9 %                  9.3 %
    the nudge, +1024 byte(s)              22.7 %    22.4 %                 21.8 %
    the nudge, +64 byte(s)                24.2 %    16.1 %                 15.5 %
    the nudge, +4096 byte(s)              25.5 %    28.5 %                 28.0 %

    BUT THE CALIBRATION DOES NOT DISCRIMINATE: the four models' worst-miss column spans 3.5 %
    while their ANSWERS span 19.3 %. Choosing the lowest answer on a 3.5 % difference in
    calibration is choosing by the answer.
```

## Where that leaves 268

268 said the 6621 blocks outside the maps are not scripts, at most 3.1% and 1.8% of them real, about
121 blocks. 274 put a band on it and got 121 to 1266.

**Both are withdrawn as numbers.** The bound that produced them reads nought on a half-real
population, and the version with both ends measured reads 9.3%–28.5% depending on a junk model the
calibration cannot pick between, each with a ±22-to-25-point error bar off its own mixtures. What
survives is the sign: at every size, under every junk model, the outside populations read at the
JUNK end of a scale whose REAL end is 40–80% and whose junk end is nought.

And 268's conclusion still has a second route that this does not touch — 269's region-preserving
floor, where the maps' own targets decode at 99.6% as named and 51–70% nudged against everything
else's 14.9% and 12.0–13.8%. That reading shares no code with this one.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `BetweenTheEnds` becomes 268's formula — divide by the junk end alone | 5 | **5** |
| the no-length guard removed, so ends on top of each other divide by nought | 1 | **1** |
| `InFileOrder` hands the population back unordered | 1 | **1** |
| `Nudged` hands back the ORIGINAL targets, with the right count | 1 | **1** |
| `Groups` overlaps — the step halved | 6 | **7** |
| `BoundPerGroup` does not sort its answers | 1 | **1** |
| **CONTROL:** `Math.Clamp(x, 0, 1)` spelled `Math.Min(1, Math.Max(0, x))` | **0** | **0** |

The one miss is 32's shape and in the harmless direction: `ASmallerSampleSitsFartherFromItsOwnWhole`
was predicted to survive overlapping groups and did not.

The `Groups` break is also the point of the shared loop.
`TheBandAndTheBoundAgreeAboutWhatAGroupIs` came back GREEN on it, correctly — both questions go
through the one loop, so they cannot come to disagree about what a group is, which is exactly why
258's fault cannot happen here.

## What is left

* **A junk model the calibration can choose.** Four nudges calibrate within 3.5 points of each other
  and disagree by 19. Something with more resolution — more mixture shares, or a mixture ladder run
  per junk model rather than one worst-miss number — might separate them. Might not.
* **The mixtures are cut from ONE held-out half against ONE junk half.** There is no band on the
  mixture rows themselves: 4 groups at 25% and 2 at 75% is what the populations can supply.
* **The 0.178 is a fact about the maps' own scripts and it has no band either.** It is one number
  from one split, and the split is halves in file order.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — still owed, untouched.
* **`--operands-everywhere` still prints 268's and 274's numbers**, with a line saying both are
  superseded and naming the command that supersedes them. Whether the old bound should stay in that
  output at all is a decision, deliberately not made.

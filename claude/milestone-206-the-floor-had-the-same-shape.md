# Milestone 206: the floor had the same shape as the thing

205 built `HowClustered` and re-read nothing with it. Its own owed list said so. This is that
list, and the answer is not what the headline number suggests.

---

## The control could never see this

Both whole-file sweeps in this project are read against a **reversed-image floor**: sweep the
same bytes backwards, count what the same filter finds, and if the two numbers are alike the
list is noise. It is a good control and it has one blind spot that has been there the whole time.

**Reversing a file preserves byte frequencies. It also preserves shape.** A table reversed is
still a table, its records still repeat, and it still clumps exactly as hard. So the control
catches noise with the same frequencies as signal and cannot catch noise with the same
*distribution* — and both sides of every comparison have been counting clumps twice.

## Both sweeps, re-read

```
  --who-knows    600 sites against 787   ->   415 places against 444
  --flags       4109 sites against 4167  ->  1445 places against 1329
```

`--who-knows` is unchanged in substance: it was below its floor by site and it is below by
place. The finding there was never the raw sweep — it is 7 jumped-into against 0 — and that
still stands.

**`--flags` changes sign.** Counting sites, this file is *behind* its own reversal. Counting
places, it is *ahead by 8.7%*.

That is the result, and it is not a rescue. Eight point seven per cent is not `7 against 0`;
two ways of counting that disagree about which side of a floor a number falls on are two ways of
saying **the raw sweep is not a finding**, which is what this project already had written down.
What is new is that the disagreement is visible, and that neither number was ever what it looked
like. The output says so in as many words rather than quoting whichever one flatters the file:

```
  SO THE COMPARISON IS 1445 place(s) against the reversed image's 1329 — not 4109 against 4167.
  The real image is ahead by 8.7%.
  NOTE THAT THE TWO COMPARISONS DISAGREE ABOUT THE SIGN, and neither margin is large.
```

The jumped-into rates — 8.0% here against 1.3% in the reversal — are still the only part of that
report clearly above anything, and they are untouched by this.

---

## The break came back green, and why

| break | first attempt | re-broken |
|---|---|---|
| the floor stops being asked how clumped IT is | **green** | caught |

Nothing asserted that the reversed-image floor's place count was clump-aware at all. Worse, the
first re-break was aimed at the wrong function: there are **two** reversed-image floors eleven
lines apart with near-identical returns — `NoiseFloor` for the flag sweep and `MoveNoiseFloor`
for the move sweep — and the break edited one while the test watched the other.

That is the fourth time in this project a break has passed because it pointed somewhere the test
was not looking, and the first time two functions were similar enough to swap silently.

The fixture that catches it writes its sites **backwards** — `02 00 xx 29` becomes
`29 xx 00 02` once the sweep reverses the image — because a fixture for a function that reverses
its input has to reach the far side of that reversal. The first version omitted the `end` and
produced zero sites, which failed loudly and correctly.

**`MoveNoiseFloor`'s place count is not guarded.** Its sweep matches a different pattern and this
fixture produces no clumped sites for it; asserting anyway would give a test that cannot fail.
It is written into the test and onto the owed list instead.

2807 → 2808 tests, all green.

---

## What is still owed

* **`MoveNoiseFloor`'s place count has no test.** It needs a fixture in the move sweep's own
  shape, written backwards like this one.
* The clumping threshold is a kilobyte and MODELLED. The entropy cut is 4.5 bits and MODELLED.
  Both are said out loud on the constants; neither has been varied to see whether the answers
  move.
* Nothing has re-read `--in-the-image`'s per-flag counts in bulk. 205 did `0x0089` by hand and
  206 did the two aggregate sweeps; the other 321 gating flags have not been looked at.
* Six stops, the money ceiling, and `0xE6` — all unchanged.

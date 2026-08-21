# Milestone 277: the null was the wrong shape

276 withdrew 273's verdict on the 38 unnamed boundary sites, on the grounds that a band's top is a
maximum and grows with how many groups you take. That was right about maxima and wrong about the
38, because the band it read was cut the wrong way. **Cut to match the population being read, real
script never reaches the 38's distance at all — 0 of 102, against the reversal's 19 of 109 — and
273's verdict comes back.**

---

## Consecutive is conservative only if the thing being read is a run

273 built its band out of CONSECUTIVE groups and wrote the reason into the code: neighbours in the
cartridge are alike, so a group of them sits farther from the whole than a scattered sample would,
and the band comes out wider than the truth. Every word of that is true.

What it does not say is that a wider null is only *conservative* when the population being READ is
itself a run of neighbours. **The 38 are scattered from `0x028514` to `0xEA7A8F`** — thirty-eight
sites, megabytes apart. Against a null made of runs, the null carries the file's regional structure
and the reading carries none of it, and everything comes back unreadable.

`Cut.Interleaved` is every n-th item, so a group is a scatter across the whole population. It is
exactly as reproducible from the file as consecutive is — no randomness anywhere — and it is the
matching shape. Both are printed wherever a band is taken, because the difference between them is
how much of a population's spread is regional structure rather than sampling noise.

## What it does to the 38

```
      end                                              in runs        SCATTERED   groups   at least as far (0.601)
      REAL: the maps' own sites, against the REST    0.278..0.451   0.132..0.225       11   0/11
      REAL: the maps' own SCRIPTS                    0.213..0.826   0.111..0.285      102   0/102
      JUNK: the reversal's sites                     0.423..0.896   0.441..0.743      109   19/109 = 17.4 %
      JUNK: the maps' own sites NUDGED +4            0.301..0.496   0.308..0.380        7   0/7
```

276's finding was that real script reaches 0.601 in **6 of 102** run-shaped samples, which made
273's verdict look too strong. Cut to match, it reaches it in **none of 102**. The rates, with the
band 276 owed:

```
      population                        rate in runs  rate SCATTERED   band, scattered   blocks
      REAL: the maps' own SCRIPTS              5.9 %           0.0 %      0.0 %..0.0 %   4
      JUNK: the reversal's sites              33.0 %          17.4 %     8.0 %..12.0 %   4
      THE TWO RATE BANDS DO NOT MEET — the worst real block (0.0 %) is below the best junk
      block (8.0 %), so the difference is bigger than either population's own spread.
```

**273's verdict stands, and 276's withdrawal of it was an artefact of the null's shape.** 276's
other correction — that a band's top is a maximum — is untouched and is why the reading is a rate
and not an in-or-out.

And 276's second loose end closes with it: the maps' own SITES gave 0/11 where the SCRIPTS gave
6/102, and the two looked like they disagreed. Scattered, a block of eleven real-script groups holds
none at or beyond 0.601 in **9 of 9**. They never disagreed; the runs did.

## Three ways a scale fails, and they are different facts

The ends now cross under **no** junk model. But three of the four put the 38 *beyond* the junk end,
which is not a strong answer — it is a broken model, because the 38 cannot be more junk than junk.
All three are the nudged site, sitting `0.308..0.380` where pure real script sits `0.132..0.225`.
Only the reversal answers, at **0%..27.5%**, and that share has no calibration, because the mixture
rows it would be calibrated against are built out of the nudged site.

**The rate is the reading and the share is not.**

## And it sharpens 275's answer

`--the-ruler` was cutting every band into runs too, and every population it handles is spread over
the whole image. Scattered:

```
      973-block groups   KNOWN REAL      mixture misses            outside ALONE
      cut into runs      14.9 %..60.5 %  +4.5 / +5.1 / +22.0       9.9 %
      SCATTERED          44.9 %..52.6 %  +5.0 / +10.5 /  +9.2      9.9 %
```

The mixture calibration goes from a worst miss of 22.0% to **10.5%**, and under the best-calibrated
junk model (+64, worst miss **4.5%**) the outside populations read **16.1%** and **15.5%** — so
*under about 20%*. 268 said 3.1% and about 121 blocks; 274 said 121 to 1266; 275 said under about a
third. **Under about a fifth**, with a calibration curve under it.

And the junk-model choice passes a test it could have failed: **the best-calibrated model is not the
one with the smallest answer** (that is +4 at 9.9%), so the criterion is not choosing by the answer.
The command says so rather than leaving it to be noticed.

274's homogeneity reading survives the same treatment: scattered, the maps' own score
`0.068..0.185` and the reversal `0.134..0.188`, still overlapping. *Homogeneity does not
discriminate here* was not an artefact of the cut.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the interleaved arithmetic transposed, `g*groups + i` | 3 | **4** |
| `Groups` ignores the cut it was handed | 1 | **1** |
| `RateBand` scores the whole band rather than the block | 1 | **1** |
| the group count rounds UP | 2 | **9** |
| **CONTROL:** interleaved written `(i * groups) + g` | **0** | **0** |

Two misses, both under-predictions. The transposed arithmetic also kills the rate-band fixture,
which uses the interleaved cut and which I had not counted; the round-up kills every group-count
assertion in four files, which is what sharing one cut between five questions buys and is exactly
what 258's fault cost when it was not shared.

## What is left

* **Which cut is right is now a judgement per reading, and nothing enforces it.** The rule is the
  shape of the population being READ, and it is written in `Cuts`' documentation and applied by
  hand at each call site. A reading that picks the wrong one will not fail any test.
* **The 0%..27.5% share still has no calibration**, because the only same-image junk model is the
  nudged site and the nudged site is not junk. Nothing here fixes that.
* **A rate of nought has no ratio.** 0/102 against 19/109 cannot be quoted as a fold-change, and the
  command says so instead of dividing.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — still owed, still untouched.

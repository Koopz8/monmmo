# Milestone 278: script-land is two and a half per cent

277 gave this project two ways to cut a null and left which one to use as a judgement at each call
site — a knob that changes conclusions and fails no test. 278 went to measure it. The measurement
says the choice **cannot be made from the data**, and on the way it produced a fact about this
cartridge that nothing here had ever stated.

---

## Every block of script this project knows lies in 2.5% of the file

```
    population                  blocks     of 64 slices   span
    the maps' own scripts         3888                3   0x08160487..0x081C5528   2.5 % of the file
    outside, named ALONE          2671               33   0x08000058..0x08EA084A  91.4 % of the file
    outside, named IN A TABLE     2154               27   0x08000500..0x08EAD390  91.7 % of the file
    the reversed image             456               36   0x08000800..0x08FFEF00 100.0 % of the file
```

**3888 blocks and 435 flag sites, all of them, between `0x08160487` and `0x081C5528`** — 404 KiB,
three of sixty-four slices. Every population this project has been comparing against them is spread
over ninety per cent of the cartridge or more.

That is not, by itself, a defect in the null. The null is *if these were real script*, and a sample
of real script is region-confined because the script is. What it does mean is the next part.

## The cut cannot be measured for the 38

The cut is a question about how a sample of the REFERENCE should be shaped. So it can only be read
off members of the population being read that lie inside the reference's span — the rest have no
position within it to have a shape.

**Three of the 38 do.** And at that size the two answers are not even different: a consecutive group
of three of the maps' own sites touches 1..2 slices of script-land and an interleaved group of three
touches 1..2 as well.

So **the cut is MODELLED**, and it is marked as such now everywhere it decides anything — in
`--flags` and in `--the-ruler`, both of which read it. 277 chose SCATTERED by reasoning (a sample of
real script would be spread through script-land rather than be a run of neighbours) and reasoning is
what it stays. 276 chose the other way, also by reasoning. **The 273 → 276 → 277 sequence was three
milestones arguing about an assumption, and the reading is printed both ways because of it.**

## Two slicings, for two questions

The first version of this measured the read population's spread over the REFERENCE's own span and
read the 38 — which cover fourteen megabytes — as touching **three** slices, because everything past
the reference's last member clamps into one bucket. The fixture that catches it is in the suite.

The two questions want different slicings and it is worth saying why:

* *How far is this population spread compared with the reference?* — over the span they **both**
  cover.
* *What shape does a sample of the reference have?* — over the **reference's own** span, at the size
  there is evidence for, because that is the only place its samples can be.

Mixed, the second is unanswerable whenever the reference is small: every group of it lands in one
slice of the wide span and the lean is a coin toss. That was the second version, and the fixture for
it was written before the fix.

## And position alone says nothing

If all the script lives in one stretch, a site outside it is evidence on its own — worth asking,
because it needs no command mix at all.

> **3 of the 38** are inside script-land (7.9%), against **3.9%** of the 3674 sites that read as a
> script and that the map scan does not open.

Two-fold on a count of three: nothing. And the floor matters — **the area would have said 2.5% and
the measured base rate is 3.9%**, so quoting the area would have made a null result look like a
1.6-fold enrichment. A floor for "how often does this land there" is the share of the things that
actually land, not the share of the file.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| `Touches` drops the clamp at the top slice | 2 | **1** |
| the below-the-span guard removed, so an underflow decides | 1 | **1** |
| the shape question sliced over the COMBINED span | 1 | **1** |
| `fits` counts the members outside the reference too | 2 | **2** |
| **CONTROL:** `Distinct().Count()` written `ToHashSet().Count` | **0** | **0** |

One over-prediction: the missing clamp was expected to flip a `WhichCut` fixture as well and did
not, which says those fixtures do not exercise a member sitting exactly on the top of the span.

## What is left

* **The cut stays MODELLED and there is no route to measuring it.** It would need known real script
  outside script-land, and there is none — this is the fifth wall of that kind (248's base, 257's
  starting nought, 258's 99, 262's compiled code, and now this).
* **3 of 64 slices is a fact about the map scan, not necessarily about the cartridge.** What it says
  is where the script THIS PROJECT FINDS lives. 268's outside populations, if any of them were real
  script, would be script outside that stretch — which is precisely the question, and this does not
  settle it, it sharpens it.
* **`AsksWhoKnows`'s nudge** (272) and **the seam** (269) — still owed, and now three milestones
  running.

# Milestone 229: waiting on a number nobody writes

The arrival scripts ask eleven routines nothing else asks (227) and move eleven flags nothing
else moves (228). They are also the only kind of script with a **condition on the front**: a map
runs one when a variable holds a particular value. This asks which of them can run at all.

---

## Both halves of the condition

```
  350 condition(s) — 69 distinct (variable, value, script) — on 58 script(s),
  across 61 map(s), naming 27 variable(s)

       0 condition(s),   0 distinct — a variable NOTHING in the scan writes at all
     282 condition(s),  28 distinct — a variable something writes, but nobody writes THAT VALUE
      68 condition(s),  41 distinct — a setvar in the scan can satisfy it
```

**Nought.** Every arrival condition on this cartridge names a variable something writes. An
instrument that asked only *is this variable written* would report all 350 as reachable and be
finished.

Ask the fuller question — *is this value ever written to it* — and **28 of the 69 distinct
conditions are waiting on a number no `setvar` in the scan produces.**

## And it is mostly one variable

```
    0x406F —  268 condition(s),  14 distinct, on  20 map(s)
             wanted 1/2/3/5/6/7/8; written 0 at 3 place(s); 268 condition(s) nobody writes
```

Twenty maps run something on arrival when `0x406F` holds one of seven different values, and the
only thing in the whole map scan that writes `0x406F` writes **nought**, in three places. Whatever
sets it to 1 through 8 is not a script — it is the game's own code, and this is the code boundary
with twenty maps' worth of arrival content behind it.

The two views disagree by design and both are printed: 282 against 68 counting conditions, 28
against 41 counting distinct ones. The gap is `0x406F` being asked the same fourteen ways on
twenty maps.

## What this is allowed to be wrong about

Only a `setvar` says what value it writes. A `copyvar` or an `addvar` puts something in a
variable too and what it puts there is not in the bytes — so a condition satisfiable only through
one of those reads here as satisfiable by nothing. **That overstates how much is behind the
boundary rather than understating it**, which is the direction this can safely be wrong in, and
it is asserted in a test rather than left implied.

---

## What changed

* `WhenAMapRunsSomething` — the condition on every arrival script, against what the scan writes
  to the variable it names. `--arrivals`.
* `For`, `WhatIsSet` and `Tally` are each reachable from a test with no cartridge.

Four breaks, four catches after one re-siting. The green one was **the seventh** with the same
cause, and this time it was the places-not-reads rule itself — the very fault 220 and 223 spent
two milestones on, walked into again in a new instrument and caught by its own break. Pulled out
as `Tally`, it fails one test.

2946 → 2952 tests, all green. Nothing the run does changed.

---

## What is still owed

* **`0x406F`.** Twenty maps, seven values, and nothing in any script writes any of them. What
  reads it, what the three writers of nought are, and which twenty maps — none of that is read.
* **The other 27 unsatisfiable conditions**, on `0x406E`, `0x4068`, `0x4079`, `0x407E`, `0x400D`
  and eight more with one apiece.
* **The eleven routines and eleven flags** these scripts have to themselves (227, 228).
* **What `0x63` and `0x65` do** (226 read what they take).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.

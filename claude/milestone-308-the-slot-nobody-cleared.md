# 308 — the slot nobody cleared

**3554 tests green.** Base: 307, 3529.

307 ended by finding, by accident, that `--trace 0x800D` says **968 of 3646 reads found a value
already in the slot, against nine writes in the whole run** — and by saying out loud that the
number had no denominator of the right kind. This is that denominator, and then the cause behind
it, which turned out not to be the thing 307 named.

---

## The right denominator

Almost every read of a slot is an ordinary read of something a script legitimately wrote. A
leftover can only be **mistaken for an answer** at a comparison that follows an unanswered call
*with nothing in between*. Those are the places, and there are four things that can happen at one.

A PLACE is `(map, script, the call's own byte position)`, so a block reached from nineteen maps is
nineteen places and seven passes of one is one — 224, in the run rather than in the scan. Worst
pass per place, because a place where a leftover reached a comparison once is a place where it can.

Measured under the behaviour every number before this milestone was printed with
(`--remember-slots`), at the widest setting:

```
    1143 places step over a routine
     598  nobody reads the slot at all — a leftover there costs nothing whatever is in it
      12  answered NOUGHT — the sentence this project has quoted since 214
     533  read a value an earlier script left
```

And of those 533, **the comparison came out differently than it would have at nought 506 times.**

## Which was still the wrong number, by a factor of six

A comparison's *result* differing does not mean the conditional after it cares.

`special 0x0187` heads all three obstacle scripts, its answer is compared against **2** at every
one of its sites, and every conditional there tests **EQUAL**. A slot holding 129 gives `Greater`
where nought gives `Less` — the comparison plainly differs — and **neither is equal**, so the
branch is the same both times.

Read off the branch instead of off the comparison:

```
      routine  slot    compare differs   A BRANCH DID   what was in the slot
      0x039    0x800D              231              0   214
      0x187    0x800D              166              0   129, 214
      0x194    0x800D               31             20   214
      0x180    0x800D               19             19   1, 129, 214
      0x171    0x800D               14             14   1
      …
```

**506 → 85**, and the two biggest contributors are 397 of the 506 and **nought** of the 85. Both
columns are printed permanently, because the loose one is the argument for the tight one (25).

That also settles a standing line honestly: *"the run answers nought and therefore behaves as it
would for any answer but one"* was written about `0x0187` at 214, and it is **still true** — 0 of
its 166 places take a different arm.

---

## And then: why was there anything in the slot at all?

`HowAScriptRuns.FirstRemembered` decides what a scene leaves for the next one. Its own paragraph
is about the twelve pads below it in the `0x400x` band — *a pad three hundred scripts scribble on
is not something the story remembers*. It is written as `variable >= 0x4010`.

**The test is one-sided, and the engine's argument slots are numerically above it.** So the band
the rule exists to exclude sailed straight over it. Printed by the instrument, at `--namespaces`'
own band split:

```
      band      numbers   places   places per number   remembered as it stands
      0x0000+      145      501                3.5   partly
      0x4000+       77      856               11.1   partly
      0x8000+       16     3428              214.2   YES
```

**214.2 places per number against 11.1.** The scratchiest band in the game, by twenty-fold on the
rule's own criterion, was on the remembered side of a cut written to exclude scratch — with the
calibration row in the same table.

What it costs is read off the bytes, not described. `41.0 person 1` at `0x0817206D` ends:

```
    0x172080  16 04 80 D6 00   setvar 0x8004, 214
    0x1720A4  19 0D 80 04 80   copyvar 0x800D, 0x8004
```

and 214 is still in the slot when `12.4 person 2` runs on the next pass, two maps away.

### Adopted

Well clear of 237's bar, so this is a correction and not a lever:

* **0 maps at every setting.**
* The two flags it stops setting, `0x0248` and `0x0251`, **hold nothing and gate nothing** —
  checked with `--moved`, not assumed (7).
* The one it starts setting, `0x0070`, **gates nineteen objects** and stops flickering pass to
  pass — 307's toggle was this.
* Cross-script leftovers **533 → 39** at the widest.

`--remember-slots` is the pre-308 behaviour and it stays, because a control the reader cannot
re-run is not a control (241). On this cartridge `>= 0x8000` and `0x8000..0x800F` are the same
sixteen numbers, so there is no boundary to argue about.

### And the table controls itself

```
      setting                                    places  nobody read  NOUGHT  LEFTOVER  differs  A BRANCH DID
      --play                                        512          337     175         0        0             0
        and with --remember-slots, pre-308          512          337     174         1        1             1
      --play --say-yes                              815          483     299        33       31            31
        and with --remember-slots, pre-308          827          491       6       330      302            43
      --play --say-yes --boat                      1424          886     498        40       38            38
        and with --remember-slots, pre-308         1438          896     372       170      142            55
      … --boat --surf --in-order --on-load         1072          545     488        39       38            38
        and with --remember-slots, pre-308         1143          598      12       533      506            85
```

The corrected column is **38 at four different settings**; the control swings 1 / 330 / 170 / 477 /
533. A number about the cartridge should not move with a lever the way the control's does, and
that stability is evidence in its own right.

---

## THE NEGATIVE, and it names the next milestone

**The floor is nought.** `--play` with no levers reads a leftover at **0 of 512** places.

Every one of the 38 that remain needs `--say-yes` — and they are all the value **1**, which is
what `HowAScriptRuns` writes into `0x800D` when it answers a yes-or-no, on the stated grounds that
*the variable the box answers into is the one everything reads*. A later unanswerable call in the
same script then finds it there.

So the residue is not the cartridge's and not the memory rule's. **It is one MODELLED lever
leaking into a routine's answer slot**, and it is the whole of what is left.

---

## The guards

Six breaks, six predictions written first:

| break | predicted | killed |
|---|---|---|
| the branch reading collapses into the loose one | 1 | **2** |
| the cut goes back to one-sided | 1 | **4** |
| a write between them no longer spends the slot | 1 | 1 |
| the call nobody reads is dropped | 1 | 1 |
| the worst pass stops winning | 1 | 1 |
| **the control**: the non-conditional close removed | **0** | **0** → then **1** |

Two over-shot, both usefully. The first killed the decoy as well, which is the decoy earning its
place. The second killed four because the boundary test is a `[Theory]` and four of its rows are
in the band — a prediction that miscounts InlineData rows is a fact about the prediction (32).

The sixth was predicted nought and came back nought, and **that one was not left there.** The line
it breaks is a rule this cartridge never exercises — 0 of 527 read places have no conditional after
the compare — so no break aimed at it can ever go red against the real image (57). A decoy fixture
was written and the same break re-run kills exactly one.

**And one fixture caught a real fault before any break did.** `Rank`, which decides which pass of a
place is kept, was ordering by the loose column — so the `0x0187` shape tied with a genuine arm
change and a place could keep either depending on which pass ran last. Five levels now, named in
order.

Both rules were moved somewhere a fixture can reach before being broken:
`HowAScriptRuns.IsRemembered` and `WhatTheRoutineLeft.Reading`. That is 219/221/222/223's
four-green-breaks-running cause, applied in advance rather than after.

---

## What this leaves

* **The 38 are `--say-yes`'s own doing.** Whether the lever should write into `0x800D` at all, or
  into something the routines do not share, is a modelling decision and it is not made here.
* **`--answer-nought` exists and is not on.** It drives the leftover count to nought by
  construction — that column is the instrument's own control (211) — and costs 0 maps at every
  setting. With the memory rule fixed there is very little left for it to do, which is why it is
  a lever and not a second correction.
* **The floor table's flags moved.** `--say-yes` is +72 where it was +74, and **`--surf` is now
  +1 flag where the prompt has said −1 since 239.** A sign change in a delta this project quotes:
  it is printed by `--the-floor` off the same seven runs, and nothing is maintained by hand.
* **`0x0194`'s 20 arm changes and `0x0180`'s 19** are the two routines where a leftover genuinely
  decided something under the old rule. Both are gone now; nobody has asked what they should have
  answered.

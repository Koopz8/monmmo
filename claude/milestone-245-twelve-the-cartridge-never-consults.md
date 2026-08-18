# Milestone 245: twelve the cartridge never consults

184 built `--who-writes`. 214 needed the other half — *does anything read this?* — and had to
hand-grep for it. `--who-reads` arrived and answered honestly, and then said so about itself:

```
  in the save's own band (0x4000-0x7FFF): 1086 written, 1973 read, 650 written and never read
    the same band on this file REVERSED: 1474 written, 1989 read, 1070 never read
    WHICH IS THE SAME ORDER OF NUMBER, so the aggregate is what these bytes do by accident.
```

**A question asked of sixteen megabytes has no answer.** 244 had just built the population where it
does — the map scan, split by which operand of which command named each number — so this milestone
asks it there.

---

## Twenty-six, and then twelve

```
  26 of the 90 variable(s) the map scan WRITES are never looked at by any operand that names a
  variable — 26 if the value-naming operand(s) are counted as looks, so 0 were hidden by a literal

    14 are looked at somewhere the map scan never opened — past the code boundary, not unread:
       0x400E, 0x400F, 0x4010, 0x4050, 0x4053, 0x4061, 0x4062, 0x4063, 0x406E, 0x4081,
       0x4082, 0x4083, 0x8001, 0x8002

    and 12 are looked at NOWHERE IN SIXTEEN MEGABYTES:
       0x4026, 0x403E, 0x4059, 0x405B, 0x405C, 0x405D, 0x4075, 0x407C, 0x407D, 0x4084,
       0x4088, 0x408B
```

**"No script the map scan opened looks at it" and "nothing in the image looks at it" are opposite
findings** — the first is the code boundary and the second is a variable this cartridge writes and
never consults. Twenty-six as one number could not tell them apart.

## And a second instrument agrees, on a different population

The twelve were put to `--who-reads`, which is a whole-image sweep with a reversed-image control —
different method, different population:

```
  0x4026 — 1 site(s) look at it, 0 of them read as script    REVERSED: 4 sites, 1 as script
  0x403E — 7 site(s),            0 as script                 REVERSED: 1, 0
  0x4059 — 0 site(s),            0 as script                 REVERSED: 0, 0
  0x4088 — 22 site(s),           0 as script                 REVERSED: 5, 0
  … all twelve: 0 read as script, against reversal floors of 0 or 1
```

Every one: **nought sites read as script anywhere in the image.** Two of them — `0x4026` and
`0x4075` — sit *below* their own noise floor, which is the honest way to say nothing.

`0x4059` is 214's variable, the last piece of that milestone's ceiling, found then by hand and
computed now. It is written once, by one arm of the one branch the run's silence still decides, and
nothing anywhere ever looks at it.

## The literal hid none of them, and that is worth printing

244 found that one operand names values rather than variables, and the obvious worry is that a
literal equal to a variable's number would hide a deaf variable by looking like a reader. Measured:
**26 either way, so nought were hidden.** The fault that ruined 243's headline does not touch this
one — which is a thing you can only say by computing both.

## A break came back green

Computing the list from *every number a variable command names* rather than from *the ones
something writes* passed all ten tests. The fixture for that rule was built out of flags, and a
flag is in neither set — a literal is in one of them. Rewritten around a literal, the same break
goes red.

**That is trap 20 for the second time in four milestones**, and both times the tell was the same: a
break that kills fewer tests than the rule has consequences. It is now worth doing routinely —
predict the count before running the break.

## The breaks

Three, three catches (after the fixture was fixed):

| break | what went red |
|---|---|
| the value-naming operands counted as looks | `BeingHandedToARoutineAsALiteralIsNotBeingLookedAt` |
| the list computed from every named number, not the written ones | `ANumberNothingWritesIsNotAVariableWrittenAndNeverLookedAt` |
| the raw control stops being raw | `BeingHandedToARoutineAsALiteralIsNotBeingLookedAt` |

The third is what makes the "0 were hidden" line mean anything: without a control that genuinely
counts literals as looks, that nought is two identical computations agreeing with themselves.

3064 → 3067 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **What the twelve are for.** A variable written and never consulted is either dead space, a
  thing the compiled game reads by address rather than by script, or a counter kept for a routine.
  Nothing here distinguishes those and this milestone does not guess.
* **The fourteen past the boundary.** `0x4050` is one of them: three sites read as script, none
  opened by the map scan, against a reversal floor of two — at the noise floor either way.
* **`0x4001`'s other two flag sites** (244); **whether `EverywhereInTheImage.Reads` should stop
  counting `0x1A arg2`** (244) — still marked rather than moved.
* **`0x0002`** — 23 flag sites, gating eight objects, unread on the flag side.
* **`10.6 (4,1)`**, the one sign nothing can stand beside (242); the 17 walls (242); why the
  floor's seven flags are what they are (241).
* **`0x026C` and `0x0807`** (240), **`0x194`'s nineteen doors** (236), **`0x82`'s seven words**
  (238), the three numbers nothing computes (231), `0x406F` (229), and everything owed at 215
  onwards.

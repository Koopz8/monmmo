# Milestone 215: one writer, and nobody listening

214 left one thing standing. Of everything a run cannot answer, exactly one routine — `0x188` —
is branched on in a way nought decides, and it is branched on at **two** of its hundred and
twenty-seven records. This is what those two are, and the instrument it took to say so.

---

## The one place

Only one byte position in the whole image has a compare on the answer variable immediately after
`special 0x0188` with nothing in between that could have answered instead. Twelve sites have a
compare within sixteen bytes; eleven of them have a `specialvar`, a `call`, a `callstd` or a
`0x47` in the way — the barriers 214 finished assembling.

`0x1634DB`, on `1.93` SECTION 52, after a `trainerbattle`:

```
  0x1634C3   25 87 01              special 0x0187          the obstacle guard, again
  0x1634C6   21 0D 80 02 00        compare 0x800D, 2
  0x1634CB   06 01 E0 7A 1A 08     if equal goto 0x081A7AE0   (release; end)
  0x1634D7   25 56 01              special 0x0156
  0x1634DB   25 88 01              special 0x0188
  0x1634DE   21 0D 80 00 00        compare 0x800D, 0
  0x1634E3   06 01 F5 34 16 08     if equal goto 0x081634F5
```

The arm nought takes says a message and then:

```
  0x16350B   16 59 40 01 00        setvar 0x4059, 1
```

## And nothing anywhere reads `0x4059`

Of fifty-nine `59 40` byte pairs in sixteen megabytes, exactly one sits in a command position,
and it is that `setvar`. **One writer, no readers.**

Saying that took a hand-grep, because this project has had `--who-writes` since milestone 184
and nothing at all on the other side. "Nothing sets this" has been askable for eleven
milestones; "nothing reads this" has not — so a variable written once and never looked at has
read exactly like a variable that gates something.

`--who-reads` is that mirror:

```
  0x4059 — 0 site(s) look at it, 0 read as script, 0 the map scan opened
    the same sweep on this file REVERSED finds 0 site(s)
    and 1 place(s) write it
    NOTHING IN THE FILE LOOKS AT IT.

  0x4055 — 23 site(s) look at it, 21 read as script, 18 the map scan opened
    the same sweep REVERSED finds 3, 0 reading as script
    and 10 place(s) write it
```

The story counter reads 21 against a floor of nought. The variable the last of the routine
ceiling writes reads nought against a floor of nought.

**So the last branch a run's silence decides, decides the value of a variable nothing ever
asks about.** What remains of that ceiling after 214's four-way cut is six routines in the mixed
bucket, and this one place, which comes to nothing.

### Which operand is the whole rule

`--who-reads` is a different question from `--who-writes` and not the same one twice, and the
thing that makes it different is which operand: **the source of a copy is a read and the
destination is a write.** Counting both would make every write a read as well, and "nothing
reads this" could then never be true of anything anybody had written to. Both directions of that
are asserted, and both are broken and caught.

`comparevars` is in the table twice, because it looks at two.

### The aggregate is noise, and the instrument says so

The obvious next question — how many variables does this cartridge write and never read — comes
back:

```
  raw, across the whole image: 5039 written and 9997 read
  in the save's own band (0x4000-0x7FFF): 1086 written, 1973 read, 650 written and never read
    the same band on this file REVERSED: 1474 written, 1989 read, 1070 never read
    WHICH IS THE SAME ORDER OF NUMBER, so the aggregate is what these bytes do by accident.
```

The reversal is **higher** than the real image. Six hundred and fifty is not a finding and the
line says so instead of quoting it. Only the per-variable answers, each with its own floor
printed beside it, are worth anything — which is the same conclusion this project already
reached about the raw whole-file flag sweep, arriving independently at a different instrument.

Eight breaks, eight catches. One green first time: the band fixture had one variable of each
kind, so a rule that reported *read and never written* instead of *written and never read*
answered 1 either way. Given a second read-only variable, caught.

2858 → 2867 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The six mixed routines** are what is left of the ceiling: 61 places at the widest lever
  setting, 44 of their 68 branches taken by nought. `0x194` is the big one — 747 sites, 1 of 18
  branches taken. None has been read.
* `special 0x0156` sits between the guard and `0x188` at the one place above and is unread.
* `--who-reads` looks at four commands. Anything that reads a variable by handing it to a
  routine — `specialvar`'s argument slots, the buffer commands — is not counted, so "nothing
  reads it" means "no compare and no copy reads it". Said out loud rather than left implied.
* The 650 written-and-never-read in the save's band are below their own floor as an aggregate,
  but the individual ones are still askable one at a time and nobody has asked.

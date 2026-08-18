# Milestone 220: the other arm of the barrier

219 ended by catching two of this project's own instruments giving opposite answers about the
same nineteen sites. This is the measurement of what that cost, and it is larger than the
disagreement that revealed it.

---

## The two arms

`SpecialCalls` scans forward from a `special` for the compare that reads its answer, and **stops
at anything that could have answered in the meantime** — another `special`, a `specialvar`, a
`callstd`, `0xA0`, `giveitem`, and since 214 a plain `call`. That barrier was assembled because
getting it wrong credits one routine with another's reply, and 214 paid 42 of 1097 attributions
to add the last entry.

`SpecialContracts` does the same scan for the same reason and had **no barrier at all**. It
walked four commands forward and stopped only at a `setvar` to the answer variable. It is what
`--routines` prints, and `--routines` is what every sentence this project has written about
routines was read off.

## What it cost

```
  before   63 of them are branched on, which is what makes an answer matter
  after    46 of them are branched on

           145 site(s) across 24 routine(s) branch on the answer only PAST something that may
               have answered instead
            17 routine(s) are branched on ONLY that way — every branch this project credited
               them with was read across a call
```

**Seventeen routines out of sixty-three.** Not one of them had a single branch that was not read
across something that may have answered first.

The compares past the barrier are **kept and printed apart**, not dropped. "Nothing branches on
this routine" and "the branch is past something that may have answered instead" are different
facts, and 219 showed the second can resolve in the routine's favour — the thing in the way at
`0x01C`'s and `0x01D`'s nineteen sites is `copyvar 0x8012, 0x8013 ; return`, which cannot have
answered anything. An instrument that dropped them would have turned a *reading to be made* into
a *no*.

## Three of the seventeen, by name

**`0x0156`, and a compare counted twice.** At `1.93`:

```
  0x1634D7   25 56 01              special 0x0156
  0x1634DB   25 88 01              special 0x0188
  0x1634DE   21 0D 80 00 00        compare 0x800D, 0
  0x1634E3   06 01 F5 34 16 08     if EQUAL goto
```

One compare, and the table credited it to **both** routines. 215 read these exact bytes by hand
and wrote up the `0x0188` half — the last branch a run's silence decides. The `0x0156` half was
sitting in `--routines` the whole time, and 215 listed `special 0x0156` as "unread" while the
table was quietly reporting what it answers.

**`0x0028`, at `31.0` SEVEN ISLAND.** This is the exact site 214 found by hand — `special
0x0028 ; call 0x081A4EAF ; compare` — the one the barrier was added for. The other arm was still
crediting it six milestones later.

**`0x01C` and `0x01D`, nineteen sites each.** 219's pair. `--through-a-call` says the call in
between touches nothing, so these two are the ones that will come back.

## And places are not times

Asking the same question the other way exposed a second thing, six milestones older than the
first:

```
  the branches below are 1037 site(s) at 411 byte position(s) — a block hanging off two
  triggers is read twice

    0x187  376 site(s), 376 branch at  72 place(s)
    0x188  127 site(s),   2 branch at   1 place(s)
```

**`0x0187` is 376 reads of 72 addresses.** The table has been counting reads and printing them
as though they were places since before 195 said which of those two is the number about the
cartridge.

`0x0188` is the check: 2 branch at 1 place. 215 grepped all sixteen megabytes for
`25 88 01 21 0D 80` and got exactly one hit, and both of `1.93`'s triggers run that same block.
The two readings now agree, arrived at from opposite ends.

---

## What changed

* `SpecialCalls.AnswersItself` — the barrier list asked from outside. **One list, not a copy.**
  A copied list is precisely how the two arms came apart: `SpecialCalls` learned `call` at 214
  and the other had nothing to learn through.
* `SpecialContracts.WhatIsComparedAfter` returns what is read directly and what is read only
  past a barrier, separately, and is reachable from a test with a handful of bytes rather than
  a whole world.
* `SpecialContract` gains `Places` and `PlacesAcross` beside `Branches` and `AcrossABarrier`.
* `--routines` prints the seventeen in their own section, saying what they were counted as
  until now and which instrument answers the question they raise.

Five breaks, five catches — the barrier removed, the far side thrown away, `setvar` demoted to a
barrier, the shared list forked, and places counted as reads.

**One of the fixtures lied first and the break found it.** Padding every barrier code to five
bytes pushed the compare out of the four-command window for the one-byte `callstd` and the
no-argument `0xA0`, so those two rows passed with the barrier removed: they were testing the
window, not the list. Written at each code's own width, with an assertion about *where the
compare landed*, all four rows discriminate. That is fixture-lie 10 — ask where in the fixture
the thing you are asserting about actually is.

2882 → 2894 tests, all green. **Nothing the run does changed** — the playthrough reads
`SpecialCalls`, which has had the barrier since 214.

---

## What is still owed

* **The seventeen, one at a time.** Each one's compare is past something, and
  `--through-a-call` can say what that something leaves. Nineteen and nineteen are already
  answered; `0x0A5` at eight sites and `0x138` at six are the next largest.
* **`0x0188`'s ten.** The routine 215 called the last of the run's ceiling has ten more sites
  whose compare is past a barrier, and they were being counted with the two that are clean.
* **The 411 places.** Every routine sentence in this project quoted sites. Which of them were
  really about the cartridge and which about how many triggers share a block is now askable and
  has not been asked.
* `0x081A77B0`, `0x0153`, and everything owed at 215, 216 and 219 stands.

# Milestone 225: what each kind opens alone

224 found the shared script list reading three of five kinds and put the other two back. It could
not say what that had cost except by naming things that reappeared. This is the number, and it is
the one that would have caught the fault at 221 rather than at 224.

---

## The cost of dropping a kind, printed

```
  by which of the five kinds hangs the script:

    person    1584 entry(ies) at 1250 address(es), 39446 read(s) at 16590 place(s)
                                                      — 15966 of those places NO OTHER KIND opens
    sign       519 entry(ies) at  360 address(es), 22507 read(s) at  3214 place(s)  —  3015
    trigger    228 entry(ies) at  128 address(es), 16963 read(s) at  2643 place(s)  —  2134
    on load    234 entry(ies) at  163 address(es),  2770 read(s) at  1518 place(s)  —  1324
    on arrival 350 entry(ies) at   58 address(es),  8938 read(s) at  1495 place(s)  —  1167
```

**The two kinds the shared list was missing open 2491 byte positions nothing else reaches** —
one in ten of the 24491 the scan opens at all.

The column that matters is the last one. A kind's own count says how much it reads; only the
*alone* count says what dropping it would lose, and those are different questions — `on arrival`
reads 8938 commands and 1167 of its positions are its own. Nothing in this repository printed
that figure before, which is why 221's list could lose two kinds and read as a tidying-up.

It can also come back nought, and has to be able to: a kind every one of whose positions some
other kind reaches costs nothing to drop. That is a fact about a cartridge and not a property of
the instrument.

## And the routine that was hiding there

`0x0A3` is what 224 found appearing out of nowhere — asked four times by the widest run, its
silence deciding eight byte positions, more than everything else in the mixed bucket together.
It is asked at sixteen places, all on map `14.9`:

```
  14.9 person 1  preceded by 0x8004=0
    answer is 1  -> "I'll always be cheering for you!"
    otherwise      -> "I'm sorry. I was your fan before."

  14.9 person 3  preceded by 0x8004=1
    answer is 1  -> "Oh! It's {FD}{01}! Too cool!"
    otherwise      -> "BROCK's my hero! He's a man among men!"
```

**Eight fans, numbered nought to seven in `0x8004`, each asking whether they are a fan of you.**

The other eight are the map's own on-load script, and they are one chain:

```
  0x16F163   25 A7 00              special 0x00A7
             16 04 80 00 00        setvar 0x8004, 0
             26 0D 80 A3 00        specialvar 0x800D, 0x00A3
             21 0D 80 00 00        compare 0x800D, 0
             07 01 07 F2 16 08     if EQUAL call 0x0816F207
             16 04 80 01 00        setvar 0x8004, 1
             26 0D 80 A3 00        specialvar 0x800D, 0x00A3
             ...
```

Eight times over, one per fan, each calling a block when the answer is nought. Those blocks are
`0x63 ; 0x65 ; return` — two commands whose widths this project derived at 187 and whose meanings
it never named, both of which take **a person id on that map** as their first word.

So the run's silence at `0x0A3` does not merely pick the dialogue for eight people: it runs an
unnamed per-person command on all eight of them, on arrival, every time. And every one of those
sixteen sites sits in scripts hung off a person or the map's own list — one kind that was in the
short list and one that was not, which is why the routine half-existed rather than not at all.

---

## What changed

* `--the-scan` gains the per-kind table with the *alone* column.
* `WhatTheScanOpens.OnlyHere` and `KindOf` take plain collections and strings, so both are
  reachable from a test with no cartridge.

Four breaks, four catches: the exclusion dropped, only the first other kind compared against, a
kind counted as its own neighbour, and the two two-word kind names folded into one — that last is
exactly the two kinds 224 was about.

2924 → 2934 tests, all green. Nothing the run does changed.

---

## What is still owed

* **`0x63` and `0x65`.** Widths read at 187 from the shape of what surrounds them, meanings never
  named, and now known to be what the fan-club room does to eight people on arrival. The first
  word is a person id at every site — that is the whole of what is known.
* **`special 0x00A7`**, which opens that chain and is unread.
* **The 97 command codes whose reads and places differ.** `--the-scan` says which; only the
  routine tables have been corrected.
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.

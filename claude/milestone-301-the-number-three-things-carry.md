# Milestone 301: the number three things carry

The window seam closed at 300. This one starts from a line in the prompt that four milestones had
disproved and nobody had re-read — and ends with a species, a byte and a routine.

---

## The stale line, and what it should have said

> *`0x0138` is the only routine handed values in TWO slots at once (`0x8005` and `0x8006`) and what
> that pair means is unasked.* — this prompt, since 293.

Under 295's and 296's rules `0x0138` is handed **nothing at all**. And the count is not one:

```
  37 routine(s) handed a value; 8 in MORE THAN ONE SLOT

  0x0136    0x8004(x4) 0x8005(x4) 0x8006(x4) 0x8007(x2)     24 place(s)
  0x01BB    0x8004(x2) 0x8005(x2) 0x8006(x1) 0x800F(x1)      2
  0x0173    0x8004(x16) 0x8005(x6) 0x800F(x1)               112
  0x018B    0x8004(x2) 0x8005(x1) 0x8006(x1)                 2
  0x0194    0x8004(x18) 0x8005(x3) 0x8006(x2)              1066
  0x00EC    0x8004(x1) 0x8005(x1)                             1
  0x0174    0x8004(x16) 0x8005(x1)                           19
  0x01BA    0x8004(x1) 0x8005(x1)                             1
```

**Eight, and one of them takes four.** 49 is trap-shaped: the milestone that disproves a line is
the one least likely to re-read it, and this one took four.

## `0x01BB` is handed a species

`0x01BB`'s two places are `2.38` and `2.56`, which the cartridge's own region-name table calls
**NAVEL ROCK** and **BIRTH ISLAND**. It is handed `0x8004 = 249` and `0x8004 = 410`, and the
species table this project located for a different question names those **LUGIA** and **DEOXYS**.

That on its own is nothing — 411 of the table's 412 entries are named, so the span is enormous.
**Fifteen operand positions in the map scan have every distinct value inside it and `0xA1 arg0`
ranks eighteenth of a hundred and two.** The command prints that first, because a number in the
span is a fact about the span (25).

The evidence is elsewhere.

## Two commands, one number

`0xB6` is `species, a byte, 00 00` — **ten byte positions, eight species**, and its third byte
takes 30, 34, 50 or 70, one value per species. `0xA1`'s first word names the same number.

The floor is 290's, one command over: **of the 63 operand positions that occur in the ten blocks
holding a `0xB6`, exactly TWO ever name the number it names.**

```
      operand      agrees in   occurs in
      0xA1 arg0           10          10   <- every one
      0x16 arg2            4           6   <- a setvar's VALUE
```

The second is the finding. **Six blocks put the species in an ARGUMENT SLOT, and the slot is
`0x8004` six times out of six.**

```
      map       species                 slot     beside it   0xB6?   routines called after
      1.74       150 MEWTWO          0x8004           0     yes
      1.87       144 ARTICUNO        0x8004           0     yes
      1.95       145 ZAPDOS          0x8004           0     yes
      1.101      146 MOLTRES         0x8004           0     yes
      2.38       249 LUGIA           0x8004          70     NO    0x01BB, 0x0138, 0x00B4
      2.56       410 DEOXYS          0x8004          30     NO    0x01BB, 0x0138, 0x00B4
```

Four of the six also hold a `0xB6`. **The two that do not are the only two places in the game that
call `special 0x01BB`** — 2 of 2 — and there the species goes in one slot and the same 30..70 byte
goes in the **slot beside it**. The two fields `0xB6` carries in one command, in two argument slots.
(`0x00B4` is at 17 places and `0x0138` at 6, so neither is distinctive; `0x01BB` is.)

**What that byte IS, is not read.** It takes four values, one per species, which is a column and
not an index. The one band this cartridge already affords is the wild tables' own levels — 2..67
over 4352 slot values read for another question — and the byte lies inside it. Lying inside a band
that wide is not a name. 226's wall, where what a command TAKES is read and what it DOES is not.

## A span is not a table

The first version of the range test took the COUNT of named entries and asked whether the value was
below it. **The unnamed entries are not at the end.** 386 of the 412 carry a name and the
twenty-six that do not are in the MIDDLE — indices 252 to 276 are a single `?` apiece. So
`value <= 386` threw away index **410**, which is named, and the reading lost **one of the two
places it exists to explain**.

264's rule — a placeholder is not a name — asked of the INDEX rather than of the count. It is a set
now, and the fixture is that shape exactly: a named index above the count and an unnamed one below
it, so a version testing the count passes neither.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the named set read as a span | 1 | **1** |
| the command's byte read as a halfword | 1 | **0**, then **1** |
| the second field is the same slot | 1 | **0**, then **1** |
| any variable counts as a slot | 1 | **1** |
| **CONTROL:** the named test written the long way | **0** | **0** |

**Two green breaks, and both were rules inside a whole-cartridge sweep** — 219, 221, 222, 223 and
298's own, for the sixth time. `InOneBlock` is split out and both fixtures reach them.

And the halfword break needed a fixture the cartridge cannot supply: **every `0xB6` in the game
carries a species whose high byte is nought**, so a halfword read at offset two gives the same
answer at all ten places and the fault is invisible on this file. The fixture uses 410, which is
the one species above 255 the game names and which no `0xB6` carries.

## What is left

* **`0x0136` takes FOUR arguments at 24 places on 3 maps** — `1.120` DOTTED HOLE, `2.35` TANOBY KEY,
  `2.38` NAVEL ROCK — with values like `(3,0,12,3)` and `(1,1,8,3)`. The richest argument signature
  in the game and nothing has been asked of it.
* **`0x018B` on `6.0` PEWTER CITY** is handed 142 AERODACTYL and 141 KABUTOPS, both with the same
  second and third values. It has no `0xB6` and no `0xA1`, so nothing cross-checks it.
* **`0xA2` has TWO species-shaped operands over 533 places** — 35 and 33 distinct values, both
  entirely inside the named set. Unasked.
* **What the 30..70 byte is.** Compiled code, or a second structure that carries the same pair.
* **The 24 `0xA1` places whose species nothing else in the block names.** Three of them are 144,
  145, 146 on three different maps — a consecutive run across maps, which is 291's shape and has no
  reading yet.

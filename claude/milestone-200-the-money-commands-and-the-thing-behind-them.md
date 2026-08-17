# Milestone 200: the money commands, and the thing behind them

199 read `0xB3` and `0xB4` and said the stops are a queue rather than a set. This is the next
three off it — and the first time reading a width has put a Pokémon in the party.

---

## `0x92` and `0x91` — nine sites each, and they are the same nine values

```
  92 | 32 00 00 00 00 | 21 0D 80 00 00 ...        50
  92 | C8 00 00 00 00 | 05 CB C0 16 08            200
  92 | 2C 01 00 00 00 | 05 CB C0 16 08            300
  92 | 5E 01 00 00 00 | 05 CB C0 16 08            350
  92 | E8 03 00 00 00 | 21 0D 80 00 00 ...        1000
  92 | 10 27 00 00 00 | 21 0D 80 00 00 ...        10000
  92 | 32 00 00 00 00 | 21 0D 80 00 00 ...        50
  92 | F4 01 00 00 00 | 21 0D 80 00 00 ...        500
  92 | F4 01 00 00 00 | 21 0D 80 00 00 ...        500
```

A four-byte little-endian value and a byte. The values are a column of prices, and the top three
bytes of every one of them are zero — which is what a 32-bit money field looks like in a game
whose largest number is 999999. Six of the nine resume on `compare 0x800D 0`: the idiom whole,
ask about the money, compare the answer to nought, branch if it is not there.

`0x91` carries the identical nine values, and its three clearest sites are consecutive:

```
  0x0816C0B6   91 | C8 00 00 00 00 | 03      two hundred, return
  0x0816C0BD   91 | 2C 01 00 00 00 | 03      three hundred, return
  0x0816C0C4   91 | 5E 01 00 00 00 | 03      three hundred and fifty, return
```

Twenty-one bytes, three commands, three returns and three prices. Each is jumped to separately,
so the three are their own column rather than one read repeated.

### And the false column, which is the point

Read **two, three or four** wide, all nine sites of `0x92` resume on `0x00`.

Nine agreements — the widest agreement anywhere in the width table, and it is worth nothing. It
is one agreement, not nine: every site is landing in the middle of the same run of zero bytes
inside the same argument. **A nop slide is what a false column looks like from inside**, and
this project has now seen it from both ends — in its own fixtures, where a zero-filled image
lets a short read walk to the assertion, and here in the cartridge, where it produces the most
confident-looking wrong answer available.

## `0xB5` — and a width refuted by a pointer

Three sites, all `B5 | 02 40` — the variable `0x4002`, the same shape as `0xB3` five entries
above, whose seven sites hand over `0x4001` and `0x800D`. The block at `0x0816CF43` does both
within a dozen bytes: `0xB3` hands over `0x800D` and the `0x22` after it compares `0x800D`
against `0x4002`.

Width nought says the block **ends** at the byte after it. Seventeen bytes further on:

```
  0F 00 A7 56 1A 08 | 09 05 | 21 0D 80 01 00 | 06 01 83 CD 16 08 | 05 10 CC 16 08 | 02
  loadpointer        callstd  compare          if 1 goto           goto              end
```

Unmistakably script, ending properly, with a real text address in it. And nothing in the file
points at it:

```
  0x0816CDB3  1 pointer(s)   <- a block start
  0x0816CDB4  0 pointer(s)
  0x0816CDB6  0            0x0816CDBD  0            0x0816CDC7  0
```

**You do not fall into a block that has its own pointer, and you do not reach one that has none
except by falling in.** That is the test that settled `0xD0`, run backwards. Widths three and
four are refuted too, and by fact rather than preference: both resume on `0x05` with four bytes
reading `0x4001007D` at two sites and `0x8001001A` at the third — not addresses in a 16 MiB
file.

---

## What it moved

```
  3803 -> 3829 blocks read to a proper end
    49 -> 46   stopped
```

| | flags before | after | party before | after |
|---|---|---|---|---|
| `--play` | 153 | 153 | 6 | 6 |
| `--play --say-yes` | 230 | **231** | 3 | **4** |
| `--play --say-yes --in-order` | 232 | **233** | 4 | **5** |
| `--play --say-yes --boat` | 292 | **293** | 3 | **4** |
| `--play --say-yes --boat --in-order` | 293 | **294** | 4 | **5** |
| `--play --say-yes --boat --surf --in-order` | 291 | **292** | 4 | **5** |

Reach unmoved at all six. `--play --say-yes` now settles in five passes instead of six, and the
boat run wins one more fight, loses two fewer and heals seven fewer times.

**A fifth party member: `#130` at level 71**, from pass one.

## And the run did not pay for it

```
  it was handed NOTHING to spend, which is the default and is why it buys nothing
  20 shop counter(s) stand on ground it reached; it stood in front of 20 of them
     — bought 0, could not buy 4
```

The run has an empty purse, is refused four things at a counter, and comes away with a Pokémon.

Both are true. Reading a command's width is not the same as being able to execute it: the run
now steps cleanly **over** `0x92` without answering it and takes the arm where the thing is
handed over. Nine places in this cartridge ask about money and nine take it, and this run
answers none of the eighteen.

That is a **ceiling**, and unlike the other two — `--say-yes` and `--boat` — it has no lever and
no name. It is the third gap of that kind and the first one found by getting *more* right rather
than less. The party number is now above the floor in a way the flag numbers are not, and until
there is a lever, `5 in the party` should be read as "5, one of which was not paid for".

---

## Guards broken on purpose

Fifteen breaks, all caught: each of `0xB5`, `0x92` and `0x91` read at 0, 1, 3, 4 and 6, plus the
fixture auditing its own wall.

`0x92` and `0x91` are five bytes of mostly zeroes — **the most slide-prone shape in the whole
table**, and the same shape that produces the false column on the cartridge. The wall goes in
the *last* argument byte on purpose, because that is the one a short read has to cross: at four
the read lands on it and stops instead of stepping over a zero.

2787 → 2791 tests, all green.

---

## What is still owed

* **The money ceiling has no lever.** `--say-yes` and `--boat` are named, printed and counted.
  This one is not: nothing says how many places the run walked past a money check, and nothing
  separates a party member it earned from one it did not. That is the top of this list.
* **`0x95` at `0x0816A43E` and `0xC2` at `0x0816CDB6`** — the next two in the queue, both
  exposed by this milestone. `0x95` sits immediately after `0x91` at four of its nine sites and
  after `0x94` at the GAME CORNER; `0xC2` sits immediately after `0xB5` at all three of its.
  The pairs keep pairing.
* Remaining stops: `0x95`, `0x9B`, `0x73`, `0xCA`, `0x43`, `0xC4`, `0xC3`, `0xC2`.
* `0xE6` is now the wall two milestones' fixtures lean on. Whoever gives it a width rebuilds six
  fixtures, and there are two tests that fail the moment they forget.
* What `0x91` and `0x92` mean is **not claimed** — only how wide they are. The prices and the
  `0x800D` idiom are suggestive and suggestive is not read.

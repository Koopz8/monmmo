# Milestone 199: three widths behind a counter

198 taught the walk to talk across a shop counter, and a command with no width appeared that
nothing had ever reached. This reads it, and the two behind it.

The whole chain exists because of one square. `0xC1` had been sitting in this cartridge for the
life of the project, one place, on the far side of a till.

---

## `0xB3` — seven sites, and the argument checks itself

```
  0x16C734  62 C7 16 08 02 | B3 | 01 40 21 01 40 DE 26 06 04 9E
  0x16C706  06 C7 16 08 02 | B3 | 01 40 21 01 40 1C 25 06 04 9E
  0x16C803  00 39 C8 16 08 | B3 | 01 40 21 01 40 06 27 06 04 2B
  0x16C8BA  00 39 C8 16 08 | B3 | 01 40 21 01 40 FC 26 06 04 E2
  0x16C91A  00 39 C8 16 08 | B3 | 01 40 21 01 40 FC 26 06 04 42
  0x16CC7C  01 10 CC 16 08 | B3 | 0D 80 22 0D 80 02 40 06 00 A5
  0x16CF43  02 80 00 01 40 | B3 | 0D 80 22 0D 80 02 40 06 00 A5
```

Two argument bytes, and they are a **variable** — `0x4001` at five sites and `0x800D` at two.
At every one of the seven, the command immediately after reads back *the same variable it was
just handed*.

An argument column can happen by accident. An argument column whose value reappears as the
operand of the next command cannot. Read at any other width the stream desynchronises at once:
at 0 and 3 the next byte is `0x01` or `0x0D`, at 1 and 4 it is `0x40` or `0x80` — halves of a
variable id, not commands.

## `0xC1` — two sites, and `0x94`'s shape

```
  94 00 00 | C1 00 05 | 6C 02      GAME CORNER, 0x0816C77A
             C1 00 00 | 6C 02      the cancel branch, 0x0816CC10
```

An argument, then release and end. Widths 0 and 1 are **refuted** rather than unpreferred: both
leave site one resuming on `05` with the four bytes after it reading `0x000F026C` — not an
address in a 16 MiB cartridge. So the question is two against three, and three swallows the
`0x6C` as data at both sites.

Measured, in the region these two live in:

```
  688 of the 4145 bytes sitting immediately before an `end` are 0x6C — 16.6%
  the pair `6C 02` occurs 1030 times in the file, against the ~46 chance would give
```

`release; end` is the second commonest thing an `end` follows. Two independent sites both
ending with that exact byte in that exact place, as an argument, is the coincidence.

**Two sites is below this project's usual bar of five**, and it is said out loud in the table
rather than left in a commit message. What licenses it is that the bar has already been met this
way twice: `0x94`, four lines above in the same table, is two sites of this exact shape, and
`0x35` is two of three.

## `0xB4` — five sites, and a column of round numbers

Three the instrument can see; two of them it cannot, because they sit behind `0x92` — read off
a hexdump by hand:

```
  B4 | 0A 00 | C7 03 | 0F 00 0D 6B 19 08     0x0816C811    ten
  B4 | 14 00 | C7 03 | 0F 00 47 6D 19 08     0x0816C8C8    twenty
  B4 | 14 00 | C7 03 | 0F 00 F8 6D 19 08     0x0816C928    twenty
  B4 | F4 01 | 91 10 27 00 00 00             0x0816C725    five hundred
  B4 | 32 00 | 91 E8 03 00 00 00             0x0816C753    fifty
```

10, 20, 20, 500, 50. Arguments have columns and opcodes do not — the test that settled `0xA1`
at milestone 55 and `0x97` since.

And the chain, which is `0xB7`'s test: `0xC7` is *already known* to take one argument, so read
two wide the stream goes `B4 → C7 → loadpointer`, three commands each parsing into the next.
Read three wide it stops dead on a `return` with a loadpointer stranded after it; read four wide
both the `C7` and the `03` vanish into an argument.

---

## What it moved

```
  3783 -> 3803 blocks read to a proper end
    53 -> 49   stopped at a command with no width
```

| | flags before | after |
|---|---|---|
| `--play` | 150 | **153** |
| `--play --say-yes` | 227 | **230** |
| `--play --say-yes --in-order` | 229 | **232** |
| `--play --say-yes --boat` | 289 | **292** |
| `--play --say-yes --boat --in-order` | 290 | **293** |
| `--play --say-yes --boat --surf --in-order` | 288 | **291** |

**+3 at every single lever setting**, and reach unmoved at all six — 183, 243, 243, 381, 381,
381. A consistent delta across six independent runs is what a real width looks like.

## And the next one behind it

Adopting `0xB3` exposed `0xB4`. Adopting `0xB4` exposed **`0xB5` at `0x0816CDB3`**, which is on
the owed list rather than guessed at here. That is the shape of this job: the stops are a queue,
not a set, and each one is only visible once the one in front of it has a number.

The remaining named stops are now `0x92`, `0x9B`, `0x73`, `0xCA`, `0x43`, `0xC4`, `0xC3` and
`0xB5`. `0x92` is the interesting one — it is the wall in front of the two `0xB4` sites that had
to be read by hand.

---

## Guards broken on purpose

Thirteen breaks, all caught:

| break | caught by |
|---|---|
| each of `0xB3`, `0xC1`, `0xB4` read at 0, 1, 3 and 4 | the test named for that command (12 breaks) |
| the anti-slide byte given a width of its own | `TheByteHoldingTheSlideOpenStillHasNoWidthOfItsOwn` |

**The fixture's anti-slide device is `0xE6`**, a byte this project still has no width for. A
zero-filled image is a nop slide — every `0x00` is a valid no-op, so a read that drifted one
byte short walks through the padding and arrives at the assertion anyway, passing at the wrong
width for the right-looking reason. Each command here carries a widthless byte in its arguments,
so a short read stops *dead* instead of sliding.

The thirteenth break is the fixture auditing itself: if `0xE6` ever acquires a width, all three
tests above quietly become nop slides and start passing for the wrong reason. That is asserted
rather than left to be discovered.

2783 → 2787 tests, all green.

---

## What is still owed

* **`0xB5` at `0x0816CDB3`.** The next in the queue, exposed by this milestone.
* **`0x92`.** The wall in front of two of `0xB4`'s five sites, and the reason they had to be
  read by hand rather than by instrument. It stops one place at `0x0816A402`.
* `0xE6` is doing double duty — it is on the owed list *and* it is the wall these fixtures lean
  on. Whoever gives it a width has to rebuild these three fixtures in the same commit, and the
  thirteenth test is there to make sure they notice.
* The GAME CORNER is now readable and nobody has looked at what it says. Three of these
  commands and `0x91`/`0x92` around them are a coin exchange — `92 <u32> <u8>` checking money
  against 10000 and 1000, `B4` handing over 500 and 50 of something, `91` taking the money.
  None of that is claimed here; it is what the next read will be looking at.

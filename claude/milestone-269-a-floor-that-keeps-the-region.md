# Milestone 269: a floor that keeps the region

268 found that every control in this project is the image reversed, and that reversing keeps every
**table** — so this cartridge's accidents, which come from its tables, survive it. The floor said
456 blocks where the truth is about 6300.

`--the-control` is the replacement. It took two tries, and the first one failing is the more useful
half.

---

## Rotation is a bad floor and says so itself

A rotation by a multiple of four keeps every byte, every frequency, every table, every alignment
and the direction of the file, and breaks only the correspondence between a pointer and what it
points at. On paper that is exactly the null.

```
      control              named   entries   blocks
      the file itself      46143      8860    10240
      BACKWARDS            12084       451      456
      ROTATED 0x400000     46143      2301     2433
      ROTATED 0x800000     46143       289      295
      ROTATED 0xC00000     46143      2449     2475
```

**289, 2301, 2449.** An eightfold spread across three offsets of the same control. A floor with
that much variance is not a floor, and the reason is plain once seen: rotating by four megabytes
moves every pointer *out of the part of the file scripts live in*, so what it measures is whether
some other region decodes.

**Its own variance is what condemns it**, which is the only honest way for a proposed control to
fail.

## The nudge keeps the region

What the sweep claims is that an aligned pointer **names** the block at its target. The null for
that is not a file with different statistics and not a different region — it is these same
pointers, in this same file, aimed a few bytes off.

```
      nudge               all           the maps'          the rest
      as named             8860  19.2 %    2337  99.6 %    6523  14.9 %
      +4 bytes             6875  14.9 %    1607  68.5 %    5268  12.0 %
      +8 bytes             7479  16.2 %    1638  69.8 %    5841  13.3 %
      +16 bytes            7005  15.2 %    1563  66.6 %    5442  12.4 %
      +64 bytes            7541  16.3 %    1545  65.9 %    5996  13.7 %
      +256 bytes           7558  16.4 %    1523  64.9 %    6035  13.8 %
      +1024 bytes          7235  15.7 %    1422  60.6 %    5813  13.3 %
      +4096 bytes          7129  15.4 %    1199  51.1 %    5930  13.5 %
```

**It is stable from four bytes to four thousand — 14.9% to 16.4% — and stability is the argument
for it.** Three orders of magnitude of nudge move it by a point and a half, where three rotations
moved by a factor of eight.

## And the answer, from a route that shares no code with 268's

**The maps' own targets carry thirty to forty-eight points of signal**: 99.6% as named against
51–70% nudged. That is what a pointer naming a script looks like.

**Everything else carries one to two**: 14.9% as named against 12.0–13.8% nudged. That is what a
pointer not naming anything looks like.

268 reached the same conclusion from the command mix and a mixture bound. This reaches it from a
resampling of the pointer set. The two share no code, no statistic and no assumption, and they
agree that the maps lead to very nearly all the script this cartridge has.

**And the reversal said 451.** Against a region-preserving floor of about 7300, the old control
under-counted the accidents sixteen-fold.

## Why the maps' own fall only to 51%

A pointer aimed four bytes into the middle of a real script still decodes to a proper end **two
thirds of the time**. The reader resynchronises: it lands inside somebody's arguments, reads
whatever those bytes are as commands, and arrives at the same `end`.

That is why "reads as a script" was never the strong filter its name suggests, and it is now a
number rather than a worry. It is also why the fixture for the nudge asserts that a four-byte
nudge **still decodes** — the obvious assertion is that it does not, and the obvious assertion is
wrong about this cartridge.

## Where the new control does not apply

```
  2. THE LITERAL-POOL TEST over the 7531 variable(s) something writes (246, 248)
      the file itself     622        BACKWARDS     193
      ROTATED 0x400000    622        ROTATED 0x800000    622        ROTATED 0xC00000    622

  3. WRITTEN AND NEVER READ, over 0x4000-0x40FF (245, 247)
      the file itself     122 / 148 / 23           BACKWARDS     65 / 74 / 37
      every rotation      122 / 148 / 23
```

**Identical at every offset, by construction.** Both of those sweeps are content-relative — a
PC-relative load reaches a word a fixed distance from itself, and a variable id is a variable id
wherever it sits — so a cyclic shift of the file is a no-op for them up to the seam.

That is not a failure of the control, it is the control's scope. Those readings are not about
which address is which, so a null that only breaks addresses tells them nothing, and the reversal
is the right control for them and remains what they have. Printed rather than left for the next
session to rediscover.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the rotation drops bytes off the end instead of wrapping | **3** | **1** |
| the rotation is not rounded to a multiple of four | 1 | 1 |
| the offsets are not aligned | 1 | 1 |
| backwards is the image forwards | 1 | 1 |
| the nudge is not applied | 1 | 1 |
| the nudge's list holds only the targets that already decode | **1** | **0** |
| ... and again, with the decoy | 1 | 1 |
| **CONTROL:** the aligned list comes out in the other order | **0** | **0** |

**The sixth is the one worth keeping.** The fixture pointed at a region of zeros to get an address
that does not decode — and a run of zeros is a run of no-ops that walks all the way to the next
`end` in the file. **Trap 1, inside a fixture written for a milestone about controls being wrong.**
The decoy points at the tail instead, where the no-ops run off the end of the image, and the test
now asserts out loud that the address does not decode before asserting that the list keeps it.

## What is left

* **Re-run the readings whose floor was the reversal AND which are about addresses.** This
  milestone built the control and applied it to one sweep. `--in-the-image`'s jumped-into sites,
  the coin-chain floor and the field-effect floor are all of that shape and have not been asked.
* **A nudge for the three-byte sweeps.** `Moves`, `Writes` and `AsksWhoKnows` scan for a pattern
  rather than following a pointer, so "aim it a few bytes off" does not translate. What does is
  asking for a flag id the cartridge does not use, and nothing here did that.
* **The seam.** A rotation joins the end of the file to the beginning, and one block's worth of
  the control is bytes that were never adjacent. It is four bytes in sixteen million and it is
  unmeasured.
* **`0x00` is a no-op with no arguments**, so any run of zeros reaches whatever `end` follows it.
  That is why the reversed image finds 451 entries at all, and it is worth knowing before the next
  fixture is written.

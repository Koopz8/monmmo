# Milestone 202: the false column, and the long chain

The last two of the pair-chain. `0x95` and `0xC2` — and they are the two opposite kinds of
evidence this project has, one right after the other.

---

## `0x95` — seven agreements, worth nothing

Seven sites, every one reading `95 00 00 00`.

```
    0 bytes: 0x00 (nop) x7
    1 bytes: 0x00 (nop) x7
    2 bytes: 0x00 (nop) x7
    3 bytes: 0x30 x2, 0x80 x1, 0xC2 x1, 0x0F (loadpointer) x1, 0x31 x1, 0x19 x1
```

**Three wrong widths, each agreeing with itself seven times over.** That is the widest agreement
anywhere in this table and it is the least evidence in it: every site is landing in the middle
of the same run of zero bytes inside the same argument. One agreement, not seven — the trap
milestone 200 wrote down after `0x92` showed it, met head on the very next time.

Read three wide the seven sites resume on **seven different bytes**. Opcodes vary between sites
and arguments have columns, so here the *disagreement* is the evidence.

And the chains behind it are long. `0x0816F875` reads on for eight commands:

```
  95 00 00 00 | 31 01 01 | 67 <0x0819DBD3> | 66 32 | 7D 00 81 00 | 03
  0F 00 <0x0819DC07> | 09 04 | 94 00 00 | 6C 02
```

ending on `94 00 00 | 6C 02` — the exact shape `0x94` was settled on eleven entries above it,
and the same shape `0xC1` was settled on at 199. `0x081BF4F7` lands on `19 08 80 0D 80`, a
copyvar between two real variables. `0x0816D3E6` lands on a loadpointer carrying `0x08197D07`.

## `0xC2` — the opposite proof

Three sites, and the longest chain in the table. Read two wide, `0x0816CDB6` runs:

```
  C2 00 05 | 7D 00 01 40 | 31 01 01 | 67 <0x081A5DF1> | 66 32
  0F 00 <0x081A56A7> | 09 05 | 21 0D 80 01 00 | 06 01 <0x0816CD83>
```

**Eight commands, each parsing into the next**, two carrying addresses that are real and one
comparing against `0x800D`. That is `0xB7`'s test several times over.

Width one is refuted rather than unpreferred: it resumes on `0x05` at all three sites, and the
four bytes after read `0x4001007D` twice and `0x8001001A` once — not addresses in a 16 MiB file.

---

## What it moved, and what it did not

```
  3829 -> 3848 blocks read to a proper end
    46 -> 38   stopped
```

And the run:

| | reach | flags | party | money ceiling |
|---|---|---|---|---|
| all six lever settings | **unchanged** | **unchanged** | **unchanged** | **unchanged** |

183 / 243 / 243 / 381 / 381 / 381. 153 / 231 / 233 / 293 / 294 / 292. Not one number moved.

**What moved is the error bars:**

```
  --play                                40 places at 3 commands  ->  37 places at 2
  --play --say-yes --boat --in-order    10 places at 5 commands  ->   4 places at 4
```

Nineteen more blocks read to a proper end and the answer is identical. That is worth saying
plainly rather than dressing up: **the reading and the run are different denominators.** The
map scan opens 2915 scripts; the run executes the ones it can stand in front of. A width can
improve one and leave the other alone, and this one did.

It is the 196 shape with the sign flipped — 196 fixed something real that changed no output,
and this changes no output while shrinking the stated uncertainty. Neither is nothing, and
neither is a headline.

## And the queue has run out

`0xB3` exposed `0xB4`, which exposed `0xB5`; `0x92` exposed `0x91`, which exposed `0x95`;
`0xB5` exposed `0xC2`. Four milestones of pairs pairing, and this is the end of it — nothing
new appeared behind these two.

What remains has no chain into it: `0x43`, `0x9B`, `0x73`, `0xCA`, `0xC4`, `0xC3`, `0xD3`,
`0xA4`. Eight commands, one block apiece, each of which has to be found on its own.

---

## Guards broken on purpose

Eight breaks — each command at every width from 0 to 4 except its own — all caught.

The true value substituted for itself was run too, at `0x95 = 3` and `0xC2 = 2`, and passes.
That is not a guard; it is the control that says the harness is not simply failing on any edit
to that line, which is a way a break can look green for the wrong reason and has not been
checked here before.

The wall is still `0xE6`. **Six fixtures across four milestones now lean on it**, and `0x95` is
the most slide-prone shape yet — three zero bytes, which is exactly why the wrong widths agree
on the cartridge. Without a widthless byte among them the fixture would agree with them.

2796 → 2799 tests, all green.

---

## What is still owed

* Eight stops with no chain into them: `0x43`, `0x9B`, `0x73`, `0xCA`, `0xC4`, `0xC3`, `0xD3`,
  `0xA4`. `0x73` is already ruled dead — it stops runs and is worth nothing, the block ends two
  bytes later at all four of its sites.
* `0x66` and `0x67` appear all through these chains and are handled outside the width table.
  Nothing here checked what they are; they were taken on trust because the addresses they carry
  resolve.
* The money ceiling is still measured and still unlevered — eight places, one MAGIKARP.
* `0xE6` has now been load-bearing for four milestones without ever being read.

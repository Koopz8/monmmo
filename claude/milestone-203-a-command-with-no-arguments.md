# Milestone 203: a command with no arguments

202 ended the pair-chain and left eight stops with nothing leading into them. Ranked by what is
behind them rather than by count, `0x43` was the one worth doing: five sites, all block starts,
and a column that is unmistakable.

---

## Nothing, and the dependency proves it anyway

```
  43 | 18 0D 80 01 00 | 19 04 80 0D 80        0x081A8C27, 0x0816CD83
  43 | 21 0D 80 06 00 | 06 01 <0x0816891F>    0x081688BA
  43 | 21 0D 80 06 00 | 06 05 <0x081A77A9>    0x0816D462
  43 | 18 0D 80 01 00 | 7F 00 0D 80           0x081BF500
```

`0x18` and `0x21` both take four arguments, both are handed `0x800D` — this game's standard
result variable — and what comes after either reads `0x800D` again or branches on it.

That is `0xB3`'s shape **with the argument removed**. An argument column can be a coincidence;
an argument that reappears as the next command's operand cannot. Here the command carries
nothing at all and the dependency is still visible in what follows: something wrote `0x800D`,
and the only candidate is the byte in front.

All five are block starts. Two are `goto` targets and one is the far side of the `0xC1` at
`0x0816CD83` that milestone 199 read — so this is still, at one remove, the counter.

## The false column, a third time running

```
    1 bytes: 0x0D x5
    2 bytes: 0x80 x5
    4 bytes: 0x00 x5
```

Ten agreements across two widths, and they are one: `0x0D` and `0x80` are the two halves of
`0x800D`, read as though they were opcodes. Four wide is the plain nop slide.

`0x92` at 200, `0x95` at 202, `0x43` here. **Three milestones in a row where the widest-looking
agreement was the wrong answer**, and each time the right answer was the width whose sites
*disagreed*. That is now less a trap than a rule: in this cartridge's script stream, a column of
identical resume-bytes across many sites is evidence of a misalignment, not of a boundary — the
only agreement worth anything is agreement about *shape*.

---

## What it moved

```
  3848 -> 3853 blocks read to a proper end
    38 -> 34   stopped
```

Reach, flags and party unchanged at every lever setting — 183 / 243 / 381, 153 / 233 / 294, and
the money ceiling still eight places and one MAGIKARP.

The error bars again, and this time sharply:

```
  --play                                37 places at 2 commands   ->  37 at 2  (unchanged)
  --play --say-yes --in-order            4 places at 4 commands   ->   1 at 1
  --play --say-yes --boat --in-order     4 places at 4 commands   ->   3 at 3
```

**The `--say-yes --in-order` run now stops at one place, at one command, in the whole
playthrough.** Two milestones ago it was ten places at five.

A new stop appeared behind it — `0x36` at `0x08160875` — so the queue is not quite as dead as
202 called it.

---

## Guards broken on purpose

Four breaks, at widths 1, 2, 3 and 4. None green.

A nought-argument command is **the most slide-prone thing a fixture can hold**: being wrong by
one costs it a single byte, and a single byte of zero is a valid no-op. The wall here is doing
double duty — the flag behind the command is `0x00E6`, so read one wide the command swallows the
`setflag` opcode and the very next byte is `0xE6`, which this project still has no width for.
The read stops dead instead of drifting to the assertion.

That is deliberate and it is asserted separately, because a number chosen for a reason and a
number picked at random look identical in a fixture.

2799 → 2801 tests, all green.

---

## What is still owed

* Seven stops left, and one new one: `0x9B`, `0xCA`, `0xC4`, `0xC3`, `0xD3`, `0xA4`, `0x36`, and
  `0x73` which is already ruled dead. `0x9B` has four sites and `0xCA`, `0xC4` and `0xC3` three
  each; `0xD3` and `0xA4` have two.
* `0xD3` is worth a look before the others: both its sites read `D3 <pointer> 04 6C 92 1A 08`,
  where the same address appears twice in eleven bytes.
* `0xE6` is now load-bearing in **seven** fixtures across five milestones and has still never
  been read. It stops nothing reachable, which is why it works — and why nobody has had a reason
  to read it.
* The money ceiling: still eight places, still one MAGIKARP, still no lever and still a decision
  rather than a reading.

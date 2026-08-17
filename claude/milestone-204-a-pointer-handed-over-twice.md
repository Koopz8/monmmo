# Milestone 204: a pointer handed over twice

203 said `0xD3` was worth doing before the rest, because both its sites showed the same address
twice inside eleven bytes. It is, and the reason is a repeating column.

---

## The bytes

```
  16 06 80 00 00 | 78 C5 92 1A 08 | D3 C5 92 1A 08 | 04 6C 92 1A 08
  16 06 80 00 00 | 78 D0 92 1A 08 | D3 D0 92 1A 08 | 04 6C 92 1A 08
  16 06 80 00 00 | 78 DC 92 1A 08 | D3 DC 92 1A 08 | 04 6C 92 1A 08
```

Three times in thirty-three bytes at `0x08163BBB`, three more at `0x0816442F`. `0x78` is already
known to take four, so `0xD3` is handed **the same pointer the command in front of it was just
handed**, and then a `call` goes somewhere else entirely. At any other width the second copy
does not line up.

## And a control, because "the same value twice" is a feeling

That claim is exactly the kind that feels decisive and is not — so it was measured across the
whole 16 MiB rather than argued from six lines:

```
  78 <4 bytes> D3 <4 bytes>    73 occurrences,  22 identical    30.1%
  78 <4 bytes> 77 <4 bytes>   507 occurrences,   1 identical     0.2%
  78 <4 bytes> 79 <4 bytes>   148 occurrences,   0 identical     0.0%
  78 <4 bytes> 04 <4 bytes>   462 occurrences,   0 identical     0.0%
  78 <4 bytes> 05 <4 bytes>   283 occurrences,   0 identical     0.0%
```

Thirty per cent against nought, nought, nought and a fifth of a per cent. Fourteen hundred
chances for the control to produce this by accident and it produced it once.

---

## What it moved

```
  3853 -> 3856 blocks read to a proper end
    34 -> 32   stopped
```

Reach, flags, party and the money ceiling are **unchanged at every lever setting**, and so are
the run's error bars — 37 places at 2 commands on the floor, 1 at 1 with `--say-yes --in-order`,
3 at 3 with the boat. This one is entirely a reading, and the run never went near it.

A new stop appeared behind it: `0xC6` at `0x081A8C3C`.

## Guards broken on purpose

Six breaks, at 0, 1, 2, 3, 5 and 6. None green.

There is a second test that asserts `0x78` still takes four. The whole argument for this width
is that two commands carry the same four bytes, which says nothing at all if the first one's
width has moved since that was measured — so the dependency is written down rather than assumed.

2801 → 2803 tests, all green.

## What is still owed

* Six stops and two new ones: `0x9B` (four sites), `0xCA`, `0xC4`, `0xC3` (three each), `0xA4`
  (two), `0x36`, `0xC6`, and `0x73` which is ruled dead.
* What the 22 paired sites MEAN is not claimed. A pointer given to two commands in a row and
  then a call elsewhere is a shape, not a reading.
* `0xE6` is load-bearing in eight fixtures across six milestones and has still never been read.

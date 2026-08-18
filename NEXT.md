Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **"396 places call routines it could not answer — every one took the zero arm" is mostly
  not true, and the mistake is the word "arm".** For **201 of those 396 the cartridge never
  branches on the answer at all**. Of the rest, 158 are places nought takes no branch.
- Two instruments were wrong on the way, and both were caught by an instrument printing two
  numbers that could not both be true:
- **A plain `call` was not a barrier.** SEVEN ISLAND's `special 0x0028 ; call ... ; compare`
  credited the compare to `0x0028`; the thing called is three commands long and the first is
  `special 0x005D`. **42 of 1097 attributions were reading somebody else's answer.**
- **And "does nought matter" is about the branch, not the compared value.** `compare 1 ; if
  LESS` is taken by nought and does not test nought — one routine is tested against 1 and 2
  and nought takes nineteen of its twenty-one branches.
- What is left of that ceiling is **one routine, branched on at two sites in the whole
  cartridge**. Eight breaks, eight catches.

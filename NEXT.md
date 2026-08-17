Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **`0x3F` is seven.** Twenty sites of one shape — a byte, a counter, `0xFF` (how
  this cartridge writes *the player*), and two little-endian words. Six parses
  too; what decides it is that at seven the next command is `compare` **20 of
  20** and at six it is a nop 20 of 20. 80 → 65 stopped blocks, and **no world
  number moved**.
- **`--derive` cannot say so**, and I nearly made it lie. It throws out both
  widths for resuming on a column — sound in general, backwards here, because
  twenty sites of one idiom mean the *right* width resumes on a column too. I
  wrote a suppression and removed it: tuning a scorer until it agrees with a
  reading is decoration, not evidence.
- **Kept the honest half**: the report says *which* rule threw a width out (it
  named three at once before, and one of them was throwing out the right answer),
  and prints how much of their run-up the sites share so the column test's worth
  is visible. Measured, printed, not wired into any verdict.
- Padding reads as an idiom — four sites in dead space score a perfect one. That
  weakness is in the measure's own doc, and is the second reason it doesn't vote.
- A test was renamed for what it actually asserts: it never told six from seven.

Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The noise floor had the same shape as the thing it was measuring.** Every whole-file
  sweep here is read against the same bytes swept backwards. Reversing preserves byte
  frequencies — and it preserves SHAPE, so a table reversed still clumps exactly as
  hard, and both sides have been counting clumps twice.
- Re-read: `--who-knows` goes from 600 sites against 787 to **415 places against 444**;
  `--flags` from 4109 against 4167 to **1445 against 1329**.
- **The flag sweep changes sign.** By site it is behind its own reversal; by place it is
  ahead by 8.7%. That is not a rescue — two ways of counting that disagree about which
  side of a floor a number falls on are two ways of saying the raw sweep is not a
  finding. The output now says exactly that instead of quoting whichever flatters.
- The break for this came back **green twice**: nothing guarded the floor's own place
  count, and then the re-break edited one of two near-identical functions while the
  test watched the other.

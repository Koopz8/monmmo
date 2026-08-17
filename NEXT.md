Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **"396 places call routines it could not answer — every one took the zero arm" is mostly
  not true, and the mistake was in the word "arm".** For **201 of those 396 places the
  cartridge never looks at the answer at all.** There is no arm to take.
- Of the rest, 158 are compared against a value that is not nought — so the silence costs
  nothing that any other wrong answer would have cost. At the widest lever setting, of 766
  such places, **exactly 2** are ones where nought is the tested value and the run's silence
  actually decides something.
- The routine that started this: `special 0x0187` heads all three obstacle scripts, and its
  answer is compared against **2 and only 2** at all 376 of its sites. The arm answer 2 takes
  is two bytes long — `release; end`. It means "do nothing".
- The repo has known half of this for ages (`ZeroIsMisleading`, with a doc comment saying so)
  and the other half for ages, and never put them in the same sentence.

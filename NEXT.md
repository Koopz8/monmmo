Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **"8 places asked it for money" finally says which eight.** The count has been printed
  since milestone 200 and the list never was. At the floor there is exactly **one**, and it
  is the GAME CORNER coin counter read last milestone — the reading had been calling it
  "1 place" for nine milestones.
- The counter offers two prices. **The run only ever sees one of them**, and the bytes say
  why: which arm runs is picked by `0x8009`, twenty-two scripts write that variable and
  **none of them is on that map**, so the run holds nought and takes the ¥1000 arm. The other
  arm is chosen by a menu row — compiled code, past the code boundary.
- So the whole-image reading sees two exchanges and the run sees one, and neither is wrong.
- Four breaks, four catches. The new fixture shape worth keeping: **a script that answers
  differently on the second pass** — without one, a run that overwrote instead of merging
  looked identical to one that did it right.

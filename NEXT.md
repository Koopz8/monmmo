Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The one thing this project had never built: following a `call` to see whose answer a
  script is reading.** Two milestones ago the scan was taught to stop at a call rather than
  guess — losing 42 attributions, deliberately. This gets them back and more.
- **336 places read an answer through a call. 225 of them now have an owner** — six routines
  that were being credited to nobody. 14 blocks, 38 maps.
- I got the rule wrong twice, in the same shape both times. First it counted only routines,
  and credited one at 57 places where the block **throws the routine's answer away** and ends
  by saying the answer out loud. Then it called that literal a constant — and it isn't: the
  same block's other arm returns a different number, so it answers one or nought depending on
  a routine nobody can run.
- Both fixes are in the shipped rule. Four breaks, four catches.

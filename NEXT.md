Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **A script command that carries no arguments at all** — `0x43`, five sites, every one
  a block start. What proves it is what comes AFTER: each site is followed by a
  comparison of `0x800D`, the game's result variable, and then by something that reads
  `0x800D` again. Something wrote it, and the only candidate is the byte in front.
- **The widest agreement was wrong for the third milestone running.** Read one byte
  wide all five sites resume on `0x0D`; two wide, all five on `0x80`. Ten agreements,
  and they are one — those are the two halves of `0x800D` read as opcodes.
- 3848 → **3853 blocks read to a proper end**, 38 → 34 stopped. Reach, flags and party
  unchanged everywhere.
- **The `--say-yes --in-order` run now stops at one place, at one command, in the whole
  playthrough.** Two milestones ago it was ten places at five.

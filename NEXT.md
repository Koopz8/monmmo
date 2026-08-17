Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **A fight has two exits and only one was ever read.** The runner, meeting a
  trainer it had already beaten, jumped into that fight's own script — the badge,
  the flags, the thing the victory was for. That script belongs to *winning*. Run
  on every later pass, it handed all eight gym leaders' TMs over once per pass,
  for ever.
- **`--fights`** reads both exits of all **729** `trainerbattle` sites. Only 27
  carry a second exit at all; **10 of those skip a guard** — a `checkflag` in the
  bytes after the command that the jump never arrives at, named by nothing else in
  the file. Eight of the ten are the eight gyms.
- **So a beaten trainer falls through**, and the victory is handed back and run
  once, on the pass that wins it. Flags up at every lever setting, the fixpoint
  settles one to two passes sooner, and `--in-order` gains a level.
- **The run now says whether anything changed hands twice.** `0 of 125` places
  with the levers on, `0 of 198` with the sea open — and `11 of 103` on the floor
  run, which is what `--in-order` is for. The denominator is printed: *none of
  them twice* and *nothing hands anything over* read the same before this.
- The fixture that guarded the old reading passed the whole time. Its bytes after
  the command were a line and an end — the shape **both readings agree about**.

Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **It was never a flag. There was only ever one scene.** PEWTER CITY writes one
  cutscene as four twelve-byte stubs — `lockall; 0x4001 <- N; goto the scene` —
  one per square you can cross to start it, each saying which door it came in by.
  A player crosses one. A fixpoint stands on all four and plays the scene four
  times.
- **61 movement commands, asked for 416 times** on the floor run. Every entry runs
  the same commands at the same addresses, so the same command is the same
  movement and it applies once. Identity, not a decision — nothing here is
  modelled.
- The boat runs go **390 → 381**, and down is the honest direction: the extra nine
  were reached by walking people repeatedly out of their own doorways.
- It also closed the thing the last milestone couldn't. The run reported 390 while
  its own last pass reached 381, because the settle test compares counts. Now the
  final walk and the last pass agree **exactly** — two designs costed for that
  problem turned out not to be needed.
- The break came back green first time: the test read the counter the milestone
  added instead of the world it changed.

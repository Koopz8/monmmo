Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **There was no wall.** `0x003E` is set unconditionally, right after GIOVANNI in
  SILPH CO., three bytes from `clearflag 0x003F`. Both `0x4001` branches in front
  of it are conditional *calls*, and a call comes back. Our walk broke out of the
  block at the first one and threw the whole scene away.
- **The wall list is 9 people behind 5 flags**, not 13 behind 6. Four numbers
  milestone 174 called "a stricter reading moving in the direction it should"
  moved back: 397→388 stuck, 13→9 in doorways, 53→46 absent, 248→245 boundary.
- **SAFFRON is a strength problem now.** The doors open when GIOVANNI is beaten;
  the run reaches him at level 25 and loses. `--play` says so out loud now.
- **Next: the other five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`,
  `0x0017` — re-read with a walk that follows conditional calls. Then `0x009D`'s
  nineteen who never arrive, and the 20 flags with an entry point nothing opens.
- **The flag race** — a script's flags reach the server from the client, so two
  conversations inside one round trip both see the old state.

Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The second width found WRONG rather than missing.** `[0x6F]` was 1; it is 4.
  Five sites, all after a `setvar 0x8004`, all landing on `copyvar` then `compare`
  at four and on padding at three. Found by following the drift rather than the
  stop — the `0xC0` stop it caused sat thirty-seven bytes downstream of it.
- **A wrong width does not only hide things. It invents them.** Read one byte
  short, this command's own arguments decode as a `setflag`. Fixing it took flags
  moved 259 → 258 and the playthrough's own count 286 → 284 — **down**. Every flag
  figure this project has published was inflated by a misalignment, which is the
  opposite of the failure it has been chasing and reads identically from outside.
- **58 → 53 stopped blocks, 3806 → 3836 reached.**
- The new guard asserts `DoesNotContain` — the first one here that catches a read
  for holding something rather than for missing something. Its fixture says in
  writing that it separates four from one and **not** four from three, because a
  nop absorbs that difference on the cartridge too.

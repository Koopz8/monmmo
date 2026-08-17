Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **SAFFRON is open in the playthrough now**, not just in the reading. A
  `trainerbattle` is its own conditional — beaten, the script carries on — and
  the run **never told the reader who it had beaten**. `HasBeaten` was false at
  every site on every pass, so every script with a fight in it stopped at the
  fight forever, however many the run won.
- **A trainer was marked fought before the fight**, so a loss was final: it met
  GIOVANNI on pass one with what it had and never went back while the party
  doubled in level. Beaten stays beaten; lost to does not.
- **The continuation after a question carried flags and not variables**, so
  PALLET TOWN's `givemon` read the starter's species as nought. No run this
  project ever printed had a starter.
- **`--play --say-yes`: 211 → 215 maps, 176 → 195 flags, 25 → 31 field moves.**
  Three fixes in a row moved nothing before the fourth moved it.
- **Next**: move the playthrough's reader into the library — two of these three
  fixes are in `Program.cs` and cannot be guarded. Fifth instance.

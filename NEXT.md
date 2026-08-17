Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **Ninety-four fights won and not one level gained.** `BattleFactory.Save` says
  a battler carries no experience and every caller starting from a save puts it
  back. The autoplayer's fight didn't, so each win reset the total and the next
  award started from the bottom of the level it was already at. Six party members
  at 25 for the whole game. **Highest level 25 → 40, 94/63 won-lost → 108/49.**
- **The floor still did not move** — 179 of 425 maps. Still loses to GIOVANNI at
  40 with five LAPRAS and a EEVEE.
- **And these numbers are not a floor.** Every pass re-runs every script, so a
  gift is taken once per pass: four of the six are duplicates. `--play` says so
  out loud now. **The starter is not in the party at all** — `givemon` with the
  species in a variable, unresolved.
- **Next**: the starter; the gift taken every pass; then the unknown commands
  ranked by what is behind them (`0x3F` leads).

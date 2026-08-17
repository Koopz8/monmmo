Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The first width that was WRONG rather than missing.** `[0x1F]` was 5, from the
  Ruby set; it is 2. Five consecutive blocks say so. A missing width stops a read
  and says so; a wrong one stops nothing — it eats the commands after it and reads
  whatever it lands on.
- **It never failed.** It produced a phantom stop at `0xE6` twenty-four bytes
  downstream, on a byte sitting *inside a `gotoif`'s pointer*.
- **`--stops 0xNN`** is the new instrument: every stopped read of one command,
  the run-up, what follows, what each width would resume on — and **where the read
  started**, which is the half that mattered. A stop is only a command if the
  reader was in step to begin with.
- **`0xA7` = 2** (the block after it is named by something else, 4 of 4) and
  **`0xC0` = 2** (3 of 5 sites one shape; the other 2 had already drifted).
  65 → 58 stopped blocks, 3771 → 3806 reached, 258 → 259 flags.
- **A zero-filled fixture is a nop slide** — one guard passed at the wrong width
  because a drifting read walked through sixty 0x00s to the target. Third fixture
  defect this session, all the same shape: the fixture was more forgiving than the
  cartridge.

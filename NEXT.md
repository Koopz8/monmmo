Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The biggest number was the wrong number.** `--play` never reported the
  commands it could not *read* — only the routines it could not answer. Asked:
  **399 runs stop at a command with no width, 378 of them on `0x73`**. And `0x73`
  is worth nothing: at all four sites the block ends two bytes later.
- **`--scripts` now ranks stops by what is behind them**, not by how often they
  happen — the same "a count is not a ranking" rule 174 wrote down. The width is
  unknown so it picks none: it tries them all, keeps the ones reaching a proper
  end, and reports what they find between them.
- **`0x3F` is the top of the real list** (15 blocks, a `clearflag` and a `call`
  behind it), then `0xE6`, `0xC0`, `0xA7`. 29 of 35 have something behind them;
  four have *no* width that reads on, which means those blocks are misread
  earlier and that is a different job.
- **`--play`'s floor still hasn't moved** — 179/425 maps. The reading is no
  longer the suspect; the party is. Six at level 25, 63 fights lost, GIOVANNI
  among them.

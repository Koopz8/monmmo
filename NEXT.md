Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The number every session reads first was wrong for thirteen milestones.** The floor
  table — six rows of "how far a playthrough gets at each lever setting" — was re-run in
  full for the first time. The map counts were right. **Every flag count was wrong**, four
  party sizes were wrong, one row had the wrong number of passes.
- Nothing written *about* it was false: `--surf` still costs two flags, `--in-order` still
  adds two and a party member. Each milestone re-ran the pair it cared about and pasted the
  delta onto a base nobody re-ran. **A table maintained by deltas drifts and stays
  self-consistent.**
- `--play --say-yes` turned out to be milestone **193's** reading. It moved at 198, at 199
  and at 200 — "the money commands", which is not a milestone anybody would expect to move
  the walk.
- Also: the second reversed-image noise floor is guarded now, and each break was run against
  **both** tests. Break the move floor, the flag test stays green; break the flag floor, the
  reverse. That 2×2 is the guard — a single green run never said which test was watching.

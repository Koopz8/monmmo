Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The debt at the top of the roadmap, paid.** The playthrough's script reader
  was 140 lines of local function in `Program.cs` — no tests, no fixture can
  reach it, nothing can break it. Two live fixes were sitting in there and both
  breaks came back green last milestone.
- **It was stuck there for a reason**: it needs `Rom` (RomExtract) and returns
  `PlayedScript` (Server), and neither assembly can see the other. So
  `PlayedScript` moved to Core — a contract belongs where both sides can see it
  — and the reader moved to RomExtract.
- **Not one number moved**: 215 maps, 195 flags, 31 field moves, 281 won, 52
  lost to, party of 3 at 59. Checked against the pre-refactor build rather than
  against memory, which caught a stale figure I'd have reported as a change.
- **Both green breaks now bite**, along with their opposite halves. A third was
  green because the fixture was wrong — yes and no reached the same command — and
  a test I wrote called `AnsweringNoHandsNothingOver` that did not answer no was
  deleted rather than kept.
- **Next**: CERULEAN CAVE is the only blocked doorway left; who writes `0x4055`.

---
channel: devlog
title: Guilds, tiers, a ladder — and a bug that let two people own one creature
ping: devlog
thread: true
---

**The one that mattered.** That "small window" I mentioned in the market's
writeup could put a creature **in your box and on the market at the same time** —
both halves internally consistent, nothing thrown, nobody finding out until two
people owned it. A save is a photograph of memory developed a moment later; the
market writes the same character by hand while its owner is still playing.
Telling the writer to forget its queue doesn't help once the photograph has
already left the queue.

Fixed with a per-account hold, and the ordering is the whole of it: the writer
takes the gate **before** it takes the photograph off the queue. Reversed, it's
the same bug by a slower road. Full writeup in #milestones.

**Also landed**

- **Guilds** — invitations, roster, and a screen on **G**
- **Tiers** — five bands, computed from the quintiles of the cartridge's own base
  stat totals. Nobody curates it; a different image gives different boundaries
- **A ladder** — Elo, one rating per band, so a strong party can't farm the
  bottom. Nothing refuses a cross-band duel yet; measure first
- **Eight boxes** instead of one
- **Held items** — the consumed half, so berries and herbs do something
- 34 of 66 held-item effects and 44 of 76 abilities now do something. Both counts
  printed at export rather than rounded up

**1938 tests.**

**Still open:** four more regions, the cartridge font, a ladder screen, whether
cross-band duels should be refused at all, and the thousand-player measurement —
still the only item blocked on hardware rather than code.

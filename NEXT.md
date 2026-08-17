Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The three flags.** Find what clears `0x0037`, then `0x0036`, then `0x0035`.
  Each is a script on a map that is already reachable — the whole middle of the
  story is three scripts deep, not three regions deep.
- **Re-run both measurements** afterwards. Every previous version of the roadmap
  was ordered by an instrument pointed at the wrong thing.
- **The code boundary is drawn.** 322 flags move somebody; only 74 can be moved
  by any script a run could reach. 397 people stand somewhere for ever and just
  **13 of them are in a doorway** — that is the whole wall list, `--flags` prints
  it, and `0x003E` (SAFFRON, 4 doors) is the top of it.
- **The half nothing had noticed**: 53 people never arrive at all. `0x003F` keeps
  7 out of SAFFRON while `0x003E` holds 8 in place — one broken scene, failing in
  both directions, and only one direction was ever visible.
- **The shared routines, in blocking order.** ~15 routines are 83% of all calls.
- **The flag race** — a script's flags reach the server from the client, so two
  conversations inside one round trip both see the old state.

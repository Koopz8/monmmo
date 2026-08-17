Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **Read the file, not the world.** Every scan here started at a map and followed
  the jumps out of it — which cannot answer *is there anything the maps do not
  point at*, and does not fail when asked. `--in-the-image 0xNNNN[,0xNNNN]` scans
  all 16 MiB for the bytes that move a flag, says of every hit whether the map
  scan ever decoded that byte, and climbs to whatever names it.
- **The boundary, re-asked of the file.** `--flags` now splits the 248 into flags
  moved by script nothing opens (an entry point to find) and flags moved by no
  script anywhere (compiled code, unreachable by reading). With the sweep run
  again on the image reversed, as the control.
- **`0x003E` and `0x003F` are one scene** — 8 held in place on SAFFRON, 7 kept
  off it. `--in-the-image 0x003E,0x003F` looks for one piece of script moving
  both. Not yet run against the cartridge.
- **The shared routines, in blocking order.** ~15 routines are 83% of all calls.
- **The flag race** — a script's flags reach the server from the client, so two
  conversations inside one round trip both see the old state.

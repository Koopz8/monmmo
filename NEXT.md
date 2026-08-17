Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **"110 gating flags it never set" turned out to be four findings added together.** Sorted:
  44 have no opener anywhere in the file, 31 are scripts the run never ran, 18 are set only
  where the map scan cannot see, and 17 are things it never picked up. **48 a longer walk
  would open; 62 it would not.**
- The first sort had three buckets and **its own output caught the mistake**: "nothing in the
  file sets it" read 134 at the floor and 56 with the levers on. That cannot happen — whether
  anything in the file sets a flag is a property of the file, not of the run.
- The cause: the run sets sixty-five flags that **no `setflag` in the cartridge names**.
  Picking a thing up sets the flag that hides it, inside compiled code — written down in this
  repo years ago and rediscovered the hard way.
- Fixed, the boundary bucket reads **44 at every lever setting**, which is how a fact about
  the cartridge has to behave. Five breaks, five catches.

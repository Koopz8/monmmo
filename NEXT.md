Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **The error bars were counting the fixpoint's own passes.** *5051 calls to 28
  routines it could not answer* and *399 script runs stopped at 3 commands* both
  counted every run of every script on every pass — and the run talks to everybody
  again each time round. The floor run asks **5047 times at 319 places**, and stops
  399 times at **40**.
- Both numbers are true and they answer different questions. Only one of them is
  about the cartridge; the other is about how many times the loop went round.
- **And the prediction was wrong.** The last two milestones found that one scene is
  written as several doors and that this walk takes all of them, and it followed
  that every count would be inflated by the door count. It is **six** out of 5047.
  The door shape matters where an effect *accumulates* — a person walked once per
  door ends up four squares out, and that was worth nine maps — and not where
  something is merely counted.
- The break for that came back green: nothing asserted what happens in the ordinary
  case, which is 5041 of the 5047.

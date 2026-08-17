Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **Three script commands read, and the whole chain came from one square.** Letting
  the robot talk across a shop counter exposed `0xC1`; reading that led to `0xB3`
  (seven sites) and, once that was adopted, `0xB4` behind it.
- `0xB3` is the clean one: two argument bytes that are a **variable**, and at all
  seven sites the very next command reads that same variable back. An argument
  column can happen by accident; one whose value reappears as the next command's
  operand cannot.
- **3783 → 3803 blocks now read to a proper end**, 53 → 49 stopped, and **+3 flags at
  every one of the six lever settings** with reach unmoved. A consistent delta across
  six independent runs is what a real width looks like.
- The stops are a **queue, not a set**: adopting `0xB4` exposed `0xB5` behind it.

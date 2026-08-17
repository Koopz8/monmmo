Copy this file to the **root of the monmmo repo** as `NEXT.md`. The daily recap
reads it and quotes it under **Next**. Keep it short — it gets truncated around
700 characters, which is roughly what people will read at the end of a recap.

Everything below the line is the part that gets posted. Edit it as things move.

---

- **One missing argument width was hiding nineteen people.** `0x9E` had no entry
  in the table, so a read stopped eleven bytes before a `call` to the `clearflag`
  that puts them on eleven maps. 53 people who never arrive is now **21**.
- **Three scans, one blind spot.** `--scripts` said 8 blocks stop at an unknown
  command; the real figure was **142** — it walked people, first block only.
  `--derive` rolled its own four-kind list, so `0xD0`, which stops 51 blocks,
  wasn't scored low, it was *absent*. 80 stopped blocks now.
- **`0xD0` = 2, and every continuation test said 3.** At 3 it swallows an `end`
  and reads the next script. What caught it: 11 of its 16 following blocks are
  pointed at by something else, and you don't fall into a block with its own
  pointer. `--derive` counts that now.
- **Next**: the remaining 80 stops (`0x3F` leads with 15); the other five wall
  flags; and the ~28 hand-rolled "every script" lists in `Program.cs`, none of
  which can be guarded — that fault has now been found four times.

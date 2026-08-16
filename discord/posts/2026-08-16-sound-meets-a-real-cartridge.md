---
channel: devlog
title: We pointed the music reader at a real game file and it mostly didn't work
ping: devlog
thread: true
---

Last update said sound was in. It was — against our own test data. Pointed at a
real game file, most of it fell over. Here's what a week of chasing that looked
like.

**One wrong assumption was starving everything.** We'd assumed a group of
instruments was laid out one way when it's laid out another. Fixing that single
idea took us from **8 songs playing to 113**, and from 16 song headers found to
316. One assumption, sitting under everything else.

**The song list started one entry earlier than we'd found it.** We were being
strict — throwing away any entry that pointed somewhere we hadn't already
confirmed. That quietly chopped the first song off the front of the list, which
moved where the list appeared to start, which is why our cross-check kept
disagreeing. Being strict in the wrong place hid the thing we were checking for.

**142 songs still don't play**, and until this week that was all we could say
about them. Now the tool says which of three different places each one broke in,
and counts each — because "142 failures" is a number nobody can act on.

**The part actually worth telling.** We had a theory about what's still wrong, so
we built a measurement to test it. The measurement said yes. Then we looked
harder and realised it *couldn't* say no — the way it was scored, any answer that
made the reader run faster scored better, whether or not it was right. It was
rewarding the shape of a wrong answer.

So we fixed the measurement instead of arguing with it, and this time it pointed
nowhere. Two rounds of clever reasoning, two dead ends.

The fix for that isn't a third round. There's now a command that prints the raw
bytes of a song next to what our reader made of each one. **Where the two stop
agreeing is the answer** — visible, rather than deduced. Sometimes you have to
stop being clever and go and look.

**2,411 tests.**

---
channel: devlog
title: The game has sound now, and every move finally does what it should
ping: devlog
thread: true
---

**Sound is in.** Music on every map, and the noises the creatures make. It's all
read out of your own game file while you play, exactly like the graphics already
were — nothing ships with the client, nothing sits on the server.

**Moves are finished.** Until now a big chunk of them just hit for damage and
quietly skipped the interesting part. SUBSTITUTE didn't put anything in front of
you. COUNTER didn't counter. METRONOME did nothing at all. That list is now
empty — every move does its actual thing.

**Attack animations too.** Each move has its own animation stored in the game,
and we now read those properly instead of flashing something generic at you.

**One thing that went wrong, because it's a good story.** The code that hunts
through your game file for sounds was quietly missing an entire category — every
single creature cry. No crash. No error. Not even a zero where a number should
have been. It printed a confident list of everything it found, and the list was
just short.

We only caught it by deliberately breaking our own safety checks to see whether
the tests would notice. They didn't. That's the frightening kind of bug: the one
where everything looks fine. The only real defence is to count things and check
the count against a number you already knew.

**2,274 tests passing.**

**Still to do:** the game's own lettering (it has beaten four attempts now), some
leftover item and ability effects, and a proper test with a thousand players at
once — which needs a second computer rather than more code.

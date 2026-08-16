---
channel: devlog
title: You can play the story with a friend now
ping: devlog
thread: true
---

**Co-op works.** Invite someone and you travel as a company — you land in the
same copy of every map, and doors keep you together instead of quietly splitting
you up.

The good part is what your friend can *see*. Say they join three gyms behind you
— while you're travelling together, the game shows them their own progress **or**
anything the people with them have opened. They can follow you through a door you
unlocked and keep up.

**Nothing is written to their save.** They're borrowing your world, not being
handed it — they haven't been given three gyms they didn't play. When they leave
they keep exactly what they earned, and there's nothing to undo, because nothing
was ever done to them.

**We also built a robot that plays the game.** It starts from a fresh save and
walks the story as far as it can get, so we can measure what actually stops a
player instead of guessing. Its first real run found four things missing and one
genuinely embarrassing bug.

**The embarrassing one: the player had no bag.** Every character in the game who
asks whether you're carrying something has been told "no" since the day that
walker was written — because the answer they read was never filled in. Every
guard, every "bring me one of these", every trade. All refusing, silently, for
months. They have a bag now.

**And one of our own tools caught itself lying.** A report said "there is no way
into this map at all", then printed a list of unreachable maps that didn't
include it. Same run, two lines that couldn't both be true. There *was* a door —
just not from anywhere we'd reached yet. It gives three answers now, because
"nothing leads here" and "everything leading here is itself unreached" are
different problems and only one is a dead end.

**Switching creatures mid-duel** landed too, which fixes an ordering bug we'd
previously written down as unfixable without it.

**2,425 tests.**

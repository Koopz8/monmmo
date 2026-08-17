---
channel: devlog
title: The robot never bought anything, and it wasn't broke
ping: devlog
thread: true
---

**Our robot player has never bought anything.** Not once, in the entire game. The
obvious explanation was that it had no money.

That wasn't it. **Shopkeepers stand behind a counter**, and the walker would only
talk to somebody it could stand directly beside. Twenty tills in the game, every
one of them two squares away, and it strolled past all of them for months.

So we built a tool to prove the clerks were walled in — and it came back and said
they weren't. Every clerk has two or three perfectly good squares next to them.
**The thing we built to confirm the theory is what killed it.** The real
distinction was one we'd never drawn: a square you can walk on and a square you
can actually *get to* are different things.

It talks across a counter now, and every shop in the game opened up. Then it got
refused at all of them, because it had no money after all. It has a purse now.

**And then it walked off with a free creature.** Carrying nothing, it went past
every "do you have enough?" check in the game and was handed one anyway. That
isn't the game being broken — it's us reading that check correctly for the first
time, and not yet having any money for it to refuse.

**The honest bit.** A scene where somebody steps out of your way was being
applied as one giant leap instead of a step at a time, which threw 364 people
clean off the edge of the map. Fixing it dropped how much of the game we can
reach: **390 places down to 381.** The lower number is the true one. It stays.

**The worst one.** The summary table we quote at the start of every session had a
wrong count in every single row, and had done for thirteen rounds of work. Nobody
caught it, because every *difference* anyone ever quoted from it was still
exactly right. A number can be wrong for a month while every conclusion drawn
from it is fine.

There is a **#plain-english** channel now for people who want this without the
engineering. **2,841 tests.**

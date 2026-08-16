---
channel: devlog
title: Sound, animations, and the silent half is finished
ping: devlog
thread: true
---

**The one worth writing down.** Every milestone ends by deliberately breaking
each new guard to confirm a named test catches it. This pass found seven rules
nothing could fail — and the seventh was hiding an entire category of the
cartridge's contents.

A cry isn't stored as audio. It's the difference between one sample and the
next, four bits at a time, and it carries its marker in the **first** byte of
its header rather than the fourth. The format's documentation says a sample
header is three zero bytes then a loop flag; this project implemented exactly
that, faithfully, from a good source — and silently rejected every packed
recording on the file.

No error, no warning, no zero where a number should be. The locator reported
hundreds of recordings and printed a confident count that was short by a whole
category.

Same shape as every serious mistake here — **every one of them looked fine**.
The countermeasure hasn't changed: count something, and check the count against
a number you knew beforehand.

The generalisable half: *a fixture built only from correct data cannot test a
rule whose job is rejection.* Five of the seven were about choosing between
candidates, and the fixture held one candidate.

**Also landed**

- **Sound** — samples, instruments, voicegroups, songs, sequences, a mixer, and
  the cries. Each layer proved by the one below it, so none of it needs to know
  where anything sits on any particular cartridge
- **Animations** — a move's animation is a program in 48 opcodes, and it's read
- **Every map's music id**, which had been read since the first header and went
  nowhere for 160 milestones
- **The battle engine's silent half is finished.** 23 groups in the last batch
- **Fourteen of those 23 needed no new machinery at all** — a line pointing at
  something the engine already had. A family named for the machinery it appears
  to need is usually named wrong
- A guard was **removed rather than propped up**: PURSUIT going first against a
  switcher couldn't fail, because a switch is never resolved inside the battle

**2274 tests.**

**Still open:** the cartridge font (four searches defeated, the oldest question
here), LOW KICK's species weight, PURSUIT's ordering, ten held-item effects,
thirty-two abilities, and the thousand-player measurement.

Nothing derived from a cartridge is written to the repo, sent over the wire, or
shipped — sound included. Extraction happens on your machine, from your file.

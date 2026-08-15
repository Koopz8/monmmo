---
channel: devlog
title: Thirteen milestones of server surgery, and then two people in a room
ping: devlog
thread: true
---

The brief changed: pillars first, and it has to hold thousands. So the first
thing built was not a fix — it was an instrument.

**`src/Tools/Crowd`** opens hundreds of real sockets, speaks the real protocol,
registers, logs in and walks. It reports how long joining took, how long a step
took to come back as the server's own answer — which is what a player actually
feels — and **what one player costs everybody else**.

Then it found something nobody guessed.

## The wall was the door

Two cores, a hundred players, all registering:

```
  93 of 100 got in, over 45.0s
  joining      24560 / 42188 / 44404 ms   (median, 95th, worst)
  a step took      2.7 /   380.2 /  883.0 ms
```

For everybody already inside, the world was answering steps in under three
milliseconds. **Getting in took half a minute.**

One password hash cost 997 ms and 64 MiB — and it was unbounded. A hundred
arrivals were a hundred simultaneous Argon2 hashes all fighting for two cores. A
thousand would be 64 GiB of demand and a quarter hour of one core. That is not a
slow door. It is a closed one.

Two changes. The cost became **OWASP's published Argon2id baseline** — 19 MiB,
two passes — measured at 91 ms here. Eleven times the throughput, a third of the
memory, still memory-hard, and a number somebody else argued for in public rather
than one this project picked. Every hash carries the parameters it was made
under, so older accounts still verify.

And the door got a **width**: one permit per spare core, spare because the last
core is the game and the people already inside were there first. The rate is
printed at startup next to the port, because a door has a rate and a server that
pretends otherwise just fails at a larger number.

```
  100 of 100 got in
  joining       7782 / 13853 / 14753 ms
  a step took      2.0 /    14.7 /  589.4 ms
```

The 95th-percentile step went from 380 ms to 15.

## Then the next wall, already visible in the same report

**52 messages a second, per player.** Every step told to everybody on the map, so
the crowd's message count grows with the *square* of the crowd. At a thousand in
one place: a million messages a second. Worse, the dispatch loop took the
server's global lock once per recipient per message.

What followed, in order: **interest instead of broadcast**, an index instead of a
scan, an outbound queue per connection so one full socket cannot stall the world,
and a sight circle so you are told about what you could actually see.

## The disk

A hundred players doing one non-walking thing every two seconds: **21 ms a save**.
A thousand players at that rate is five hundred saves a second — ten seconds of
writing per second, ten times over.

Half of them were never needed. `SavedCharacter` is a record holding lists, and a
record compares lists by *reference* — so two snapshots of somebody who hadn't
moved a muscle compared unequal, always. "Has anything changed?" could only ever
answer yes. The sibling type had closed the same trap on itself, with a comment
saying why. **2370 things done became 1254 saves.**

Then the save moved off the player's input path, and finally it started writing
only what changed — by section, never by row, so a half-written party has nowhere
to exist. The commonest save went from about **thirty statements to one**.

The dangerous version of that change is a section silently not written, so the
rule is: **not knowing means writing everything.** A fresh server knows nothing
about anybody, so its first write for each account is whole.

## The crowd in one room

Three options, and only one of them is honest. Capping how many people are drawn,
or shrinking the sight circle, both leave a crowd standing there that the player
cannot see and can walk into. **A copy has no crowd in it that anybody is being
lied to about.**

So: instancing. A **map** is a place, a **copy** is one instance of it. Past forty
in a copy the next arrival opens another, lowest-numbered first, so somewhere busy
at noon is back in one copy by midnight. Forty because everybody seeing everybody
is n² — 1,600 at forty, 160,000 at four hundred — and because forty is about as
many people as fit on one screen, so a full copy looks busy rather than looking
like a queue.

VIRIDIAN FOREST, a hundred players:

| | one copy | three copies |
|---|---|---|
| messages/sec, whole crowd | 12,887 | **4,880** |
| messages/sec, per player | 129 | **49** |
| a step, 95th | 9.2 ms | 7.1 ms |

Sixty-two per cent off, which is exactly what splitting a hundred into 40/40/20
predicts.

## And then two people in a room

Thirteen milestones rebuilt how a login is admitted, how a message reaches
anybody, who counts as being in a place, how far you can see, where a character is
written down, when, which parts, and how many copies of a room there are.

Every one of those has tests. **None of those tests was two people in a room.**

So milestone 124 is no code at all — a verification on the real client against the
real server. They arrive beside each other at (1,2) and (2,2). They can see each
other. `L` lists the other one. A duel starts, is accepted, and resolves with each
screen showing the fight from its own side.

That last one exercises nearly all of it at once: a message that has to reach a
specific player, a battle whose events are swapped per side, dispatched through
the outbox to two connections in the same copy of the same map.

**1568 tests, no cartridge required.**

## What is still open

The scaling list is closed as far as code goes. What remains:

- **A thousand players on hardware that could hold them.** Every number above is
  two cores, with the load generator sharing them with the server it is
  measuring. On a sixteen-core box the door alone goes from ~11 arrivals a second
  to ~165. That needs a second machine and an afternoon — and it is the only open
  question code cannot answer.
- **~119 silent effect groups**, most of which should probably stay silent:
  FLAIL's power table, SONICBOOM's twenty. Numbers nobody can derive shouldn't be
  invented.
- **The cartridge font**, which has now defeated four methods.
- **A shop to buy cosmetics from** — the last piece of a feature three-quarters
  built for fifteen milestones. Money is the thing that makes any of it a choice.
- **More than one box**, and the species fields still unread: abilities, safari
  flee rate, egg cycles, base friendship, egg groups, body colour.

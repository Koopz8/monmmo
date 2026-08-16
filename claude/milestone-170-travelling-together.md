# Milestone 170 — Travelling together, and a world you borrow

The first step of co-op, and the rule instancing has owed since milestone 117.

It turned out to be two things, and the second one was not in the plan. It arrived from one
sentence of the brief: *my friend hops on for an hour, we play, he leaves, I keep going, he
catches up later.* That sentence rules out most of the obvious designs, and the one it leaves
is smaller and better than the one that was written down.

## A company, not a party

This game already has parties and they are the six creatures somebody is carrying. One word
for two things is how a bug gets written by somebody reading the right code and thinking of
the wrong subject, so a set of people travelling together is a **company**.

It is the same shape as `Trade` and `Duel`: one at a time each, an invitation that dies when
either side walks away, and asking somebody who has already asked you is how it begins. Three
verbs behaving the same way are three verbs a player learns once — the argument milestone 100
made, holding a third time.

One difference, and it is why this is not a copy of `Trades`: a company holds more than two.
Asking somebody while you are already in one adds them to yours rather than starting a second,
and both sides are checked for room before either is moved — a handshake that can half-succeed
is worse than one that refuses.

Four members, modelled, for two reasons that agree: a company arriving is stood beside whoever
it is following and the squares around one tile number eight, so four always fit; and a
company that could approach forty would be a crowd, which is the thing copies exist to divide.

## The copy rule, finally kept

`CopyWithRoom(mapId, preferred)` has preferred the copy you came from since milestone 118.
That keeps two people together while they walk through the same door and **stops the moment
one of them warps, takes another route, or is sent somewhere by a script** — which is three of
the most common ways to move in this game.

A company asks where the company is instead of where you were. All three cases keep you
together. A full copy still does not refuse: forty is a target for arrivals rather than a wall,
and somebody who has asked to travel with a friend would rather stand in a copy of forty-one.

## The part that was not in the plan

The design note said flags propagate to whoever is present. That is right for *earning* and it
is not enough for *travelling*, and the drop-in sentence is what shows the gap: if I am three
gyms ahead when my friend joins, propagation gives him nothing — he was not present when I set
those flags — and he cannot follow me through a single door I opened.

The three ways to fix that are not equal:

| | |
|---|---|
| copy the flags across on joining | irreversible, hands him three gyms he did not play, and puts his save in a state its own inventory cannot justify |
| make him play up to me | not drop-in co-op |
| **let him look through mine** | reversible, hands him nothing, and he walks where I walk immediately |

**The seam turned out to be a predicate.** This is `VisibleTo`, and it has been this shape
since flags existed:

```csharp
if (!entry.Template.IsHereFor(player.Script.Has)) continue;
```

`IsHereFor` takes a function. So borrowing a world is not a state change at all — it is a
different function:

```csharp
return flag => player.Script.Has(flag) || theirs.Any(t => t.Has(flag));
```

While travelling, what you see is your own story **or** anything the people with you have
opened. Nothing is written to anybody's save. When somebody stops travelling it goes back to
being their own flags, with nothing to undo, because nothing was done.

### What that buys, said plainly

- Somebody three gyms behind joins and walks everywhere their friend can, at once.
- Nothing is handed to them. They have not beaten Brock and their save does not say they have.
- The friend logs off after an hour and they keep **exactly what they earned** — a flag is
  written to a save only by a script that save actually ran.
- They can catch up alone at their own pace, or borrow again next time.

The failure mode this avoids is the quiet one: a save that loads fine, looks right, and
behaves two hours later as though an event happened that this character has no evidence of.

### And it makes a measurement unnecessary — for now

The design note proposed measuring every flag to decide which ones are world gates and which
are personal marks, because propagating a badge would be wrong. **Borrowing does not need that
classification at all.** Borrowing a badge flag lets you through a door and does not give you
a badge, because nothing is given.

The measurement is still owed for step three — what happens when a flag is *earned* with
somebody standing next to you — and it is still the right way to answer it. It is no longer
blocking, which is why it is not in this milestone.

## Said in words that already exist

A company forming changes what every member can see, which is the same event a script causes
when it writes a flag: somebody is now on the other side of a gate. So it calls `Reconcile`,
the method that already turns that into people appearing and disappearing. A company breaking
calls it in both directions — the one who left, and the ones still travelling — because a door
that stays open after the person who opened it has gone is exactly the state this arrangement
exists to avoid.

Somebody joining is moved with the same code a doorway uses, for the reason milestone 118
gave: to everybody watching, somebody walked out of one copy and into another. No client needs
a new case for it.

## One thing found on the way

`TravellingWith` is sent to everybody a company change touched. A company that has fallen to
one is over — and the last member was being told they were travelling with themselves: a list
with their own name in it, indistinguishable from still having company.

`Company.IsOver` already said a company of one is not a company. It was stated in one place and
not honoured in the other, which is the same shape as milestone 169's `ContinueFrom` — a rule
written down and a caller that did not ask.

## Six guards, all of which fail

Every new rule broken on purpose; each failed a named test at once. No decoys needed this time,
which is worth noting because it is unusual here — the tests were written from the rules rather
than from the code, and the two rules most likely to go unwatched (nothing is written; it is
handed back) each got a test of their own before either was implemented.

## Read and modelled

**Read.** Nothing. There is no cartridge rule for two people; every line here is a decision.

**Modelled.** Four to a company. That a company's copy outranks your own. That being within
reach is required to ask — milestone 118's limit, kept for its reason. That what you see is
the union and what you own is only what you earned.

## What this owes

- **Initiating from the client.** Accepting is a key; asking is still `/travel <name>` on the
  console, exactly as `/with` was. The people screen has the leave key and not the ask key,
  because it holds names and the message carries an id.
- **The other three places flags gate something**: the ferry passes, hidden objects at
  `5966`, and warp gates. Only the map view borrows so far. The ferry one is entangled with an
  item, which is genuinely yours and should not be borrowed — that needs its own decision.
- **Step three**: what happens when a flag is earned with somebody standing there. That is the
  measurement, and it is next.
- **Nothing yet stops a company spanning two maps.** Members are only gathered when one of
  them arrives somewhere; two people who walk apart stay in one company and simply are not
  together. Harmless, and worth deciding on rather than leaving.

2476 tests.

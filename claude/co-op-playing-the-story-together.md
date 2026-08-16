# Co-op — playing the story together

*Research. No code has been written for any of this yet.*

The brief: two people play FireRed's story together, start to finish, and both of them come
out of it having played it.

Three decisions are already taken and everything below assumes them:

1. **Everyone progresses.** Each keeps their own save. What happens while you are together
   happens to both of you. Nobody is a guest.
2. **A story fight is fought by each of you separately.** The engine is one-on-one and always
   has been; two Brocks is a cheaper fiction than a rewrite.
3. The duel work that was in flight is finished and shipped (milestone 169). Nothing below
   depends on it.

---

## The problem, in one line of code that already exists

```csharp
if (!entry.Template.IsHereFor(player.Script.Has)) continue;
```

That is `GameWorld.VisibleTo`. `Script` lives on `ServerPlayer`. So **two friends standing on
the same square in the same copy of a map already see different worlds** — different people,
different hidden objects, different doors. One of them has freed MR. FUJI and the other is
looking at an empty room.

This is the whole of co-op's difficulty and it is also the reason it is tractable. The server
does not send a map; it sends *a player's view of a map*, and it has done since instancing.
Nothing needs inventing. Something needs deciding, and then the same seam needs pointing at a
group instead of a person.

## The four seams, measured rather than guessed

**Story state is per-save.** `ServerPlayer.Script` — flags and variables. Read by `VisibleTo`,
by the ferry passes, by hidden objects, by every warp gate. There is exactly one of these per
player and nothing shares one.

**A script is a conversation with one person.** `RunScript(int playerId, ScriptRan ran)` sets
flags on that player and nobody else. Worth noting: the *client* runs the script and reports
what it set. So two people talking to the same NPC each run it, independently, at whatever
moment they press A.

**The engine is strictly one-on-one.** `Battle(Battler player, Battler opponent, uint seed)`.
Decision 2 above is what makes this a non-problem.

**There is already a group, and it is the wrong shape.** `Guilds` is a named roster with a
shared channel. A guild is a social list. What co-op needs is a *travelling party*: a small
set of people who share a copy, and to whom things happen together. Those are different
objects and conflating them would make every guild member share a room.

## What has to be built, in order

### 1. A party

The primitive everything else hangs off, and most of it exists.

`/with <name>` already moves you into the copy somebody else is in and stands you beside them
(milestone 118). Doors already keep you together — walking into a new map prefers the copy you
were already in. Copies already pack rather than drift (120). What is missing is that all of
that is *incidental*: it happens because you both walked through the same door, and it stops
the moment one of you takes a different one.

A party makes it deliberate:

- Invitation, acceptance, and one party at a time each — **the same shape as `Trade` and
  `Duel`**, which is the argument milestone 100 made and it holds again: two verbs that behave
  the same way are two verbs a player learns once.
- Copy affinity becomes a party rule rather than a door rule. A party member arriving anywhere
  lands in the party's copy of it.
- A full copy still does not refuse. Forty is a target for arrivals, not a wall, and somebody
  who has asked to be with a friend would rather stand in a copy of forty-one.

This replaces the "a name in a list with a *go to* on it" that milestone 118 wrote down as
owed. A go-to button is the weaker version of the same feature.

### 1a. Borrowing a world — added after the note was written

*This section supersedes part of §2 below. Built in milestone 170.*

The brief gained a sentence: **a friend hops on for an hour, you play together, he leaves, you
keep progressing alone, he catches up later.** That rules out most of the obvious designs.

Propagation is right for *earning* and not enough for *travelling*. If you are three gyms
ahead when he joins, propagation gives him nothing — he was not present when you set those
flags — and he cannot follow you through a single door you opened.

The seam turned out to be a predicate. `VisibleTo` calls `IsHereFor(player.Script.Has)`, and
`IsHereFor` takes a function. So while travelling, what you see is your own story **or**
anything the people with you have opened:

```csharp
flag => player.Script.Has(flag) || theirs.Any(t => t.Has(flag))
```

**Borrowed, never written.** Nothing goes into anybody's save. Stop travelling and it reverts,
with nothing to undo because nothing was done. He walks where you walk immediately, has not
been handed three gyms he did not play, and keeps exactly what he earned when he leaves.

This also removes the blocker on §2: borrowing does not need flags classified, because
borrowing a badge flag opens a door and does not give you a badge. The measurement is still
owed for §2 proper — what happens when a flag is *earned* with somebody standing there — but
it is no longer in the way.

### 2. What propagates, and the measurement that should decide it

**This is the one genuinely open decision, and it should not be made by reasoning.**

The obvious rule is: a flag set while you are together is set for everybody in the party. It
is obvious and it is not quite right, because flags are not all the same kind of thing:

- **World gates.** A door unlocked, a guard moved out of the way, a boat that will now sail.
  These are facts about the world, and they must propagate or you are not playing together.
- **Personal marks.** A badge, which starter you chose, whether you have been shown the box
  tutorial. These are facts about you, and propagating them means your friend beats Brock and
  you are handed the badge.

The cartridge does not distinguish them. There is no bit anywhere that says "this flag is
about the world". So the classification cannot be read — but **it can be measured**, and this
project already owns both instruments:

- `FlagClearers` finds which script sets each flag.
- The reachability walk from milestone 86 computes which maps a save can reach from its flags,
  and `WhereThisSaveCanGet` already prints it.

So: for every flag, ask what turning it on actually changes. Does it open a warp? Move a
person? Reveal an object? Or does it change nothing except a count? **A flag that gates
nothing but itself is a personal mark; a flag that gates geography or a person is a world
gate.** That is derivable, printable, and checkable against a cartridge — which is the only
kind of answer this project accepts, and it is much better than a hand-written list of flag
numbers that nobody could ever verify.

The number to print before writing any of this: *how many flags gate something, and how many
gate nothing.* If it is a clean split, the rule writes itself. If it is not, the shape of the
mess is the finding.

**Until that is measured, assume everything propagates** and count what looks wrong. That is
the same discipline as the silent-move list: the wrong answer, visible and counted, beats a
plausible answer nobody is watching.

### 3. A story event happens to everyone standing there

Flags are not the only thing a script does. It hands over items, and creatures, and money.

The rule that keeps a save internally consistent is the blunt one: **whatever the event gives,
each person present gets one.** Two parcels, two starters, two fossils.

The alternative — propagate the flag, hand the item to one person — puts a save into a state
its own inventory cannot justify: you have "delivered the parcel" set and no parcel, and the
moment you play solo, a script asks whether you are carrying it and the answer is wrong. A
desynchronised save is the kind of bug that shows up an hour later in a room nobody connects
to the cause.

Two of each is a fiction. An inconsistent save is a defect. Take the fiction.

### 4. Story fights

Each of you fights your own copy of the trainer, per decision 2. Which leaves one question the
decision does not answer: **when does the gym door open?**

Proposal, and it is a proposal rather than a finding: the world gate opens when *anybody* in
the party clears it, and the fight stays available to whoever has not had it. You can walk in
and fight Brock because you want to; you are not blocked because your friend already did. A
party that has to wait for its slowest member at every gate is a party that stops playing
together.

If the badge turns out to be a world gate rather than a personal mark when the measurement in
§2 is run, that answer changes, and it should change from the number rather than from taste.

## What this deliberately does not do

**Cutscenes still run per player.** A script is driven by the client that pressed A, so two
people watching the same scene watch it at different moments. Making a scene play once, for a
room, is a different feature — it needs the server to own scene playback rather than the
client — and it is not required for a playthrough to work. Named as not done.

**No double battles.** See decision 2.

**No shared inventory, no shared money, no shared box.** Trading already exists and is the
right verb for moving things between people.

**Nothing about more than two people**, beyond the party being a set rather than a pair. The
copy limit of forty is the only real ceiling and it is far above any party.

## The order I would build it in

1. ~~**The party.**~~ **Done — milestone 170**, as a *company* (this game's parties are the
   six creatures you carry). Invitation, acceptance, copy affinity, and borrowing.
2. **The flag measurement.** Print what every flag gates. One tool run against a cartridge,
   no engine changes, and its output decides step 3.
3. **Propagation**, with the rule the measurement produced.
4. **Events give one to each**, and whatever that turns up about scripts that hand things over.
5. **A play-through**, together, start to finish, with the count of what looked wrong.

Steps 1 and 2 are independent and produce nothing that has to be undone if step 3's answer is
surprising, which is the usual and correct shape for this project.

## The one thing that could go badly

A propagated flag putting a save in a state it could not have reached alone. Everything in §3
is aimed at it, and it is worth saying plainly that the failure mode is *quiet*: the save
loads, the world looks right, and something two hours away behaves as though an event
happened that this character has no evidence of.

The countermeasure is the one this project has used every time: **count something, and check
the count against a number known beforehand.** After a co-op session, the reachability walk
should be run on both saves. Two people who played the whole story together should be able to
reach the same maps. If they cannot, the difference is the list of what propagation missed.

# Milestone 265: the way back

Every reach number this project has ever printed is a **forward** number. The walk leaves the
starting square, follows steps, doors, borders and ledge hops, and reports what it arrived at.

That is reachability in a **directed** graph, and this project has been reading it as
connectedness for two hundred milestones. "The run reaches 174 maps" has always been said as
though reaching and returning were one fact.

They are not, and the cartridge is why: a ledge is hopped one way and climbed none, a door names
a warp on the far map and nothing makes that warp name one back, and nineteen exits in this game
name a map no bank has because the room decides at runtime where you came from.

---

## The edges are the walk's own

The one thing that would make this measurement worthless is deriving the edges twice. A "can it
get back" built from a second, separately-written idea of what a step is measures the difference
between two authors — 241's rule, and 261 was caught by it inside four milestones.

So `WorldWalker` hands out the edges it takes, and `TheWayBack` traverses that record and nothing
else. Every enqueue in the walker goes through one function, which records and then enqueues; a
walk that grew a new way of moving and forgot to record it would report a square nothing can get
back from, and be wrong quietly.

## At the floor, two thirds of what the run reaches is a one-way trip

```
    no move, nobody stepping aside
      35142 square(s) stood on over 174 map(s), 117173 step(s) taken
      11113 can get back to 4.1 (1, 2), 24029 CANNOT
      on 140 map(s), 137 of them stranded WHOLE
```

**And the way into all of them is eighteen ledge hops, all on ROUTE 4, in two rows.**

```
      the last step in was: hop x18
        3.22  ROUTE 4  (75, 9) -hop-> (75, 11)   ... eleven of them
        3.22  ROUTE 4  (90, 8) -hop-> (90, 10)   ... seven of them
```

That is the drop below MT. MOON. Past it lie ROCK TUNNEL, CELADON, SAFFRON, VERMILION, ROUTE 11
and DIGLETT'S CAVE — and DIGLETT'S CAVE is the joke, because its other end opens onto the fenced
part of ROUTE 2 that a floor party cannot cut its way out of. The whole east of the map is a
funnel with one lip.

**CERULEAN CITY is 837 of 848**, not all of it. Eleven squares of it are on the map-edge strip
that joins ROUTE 4 *above* the ledge. The instrument discriminates at the square, which is the
only reason that line is believable.

## With moves and through people, 48 squares — and three of them are lifts

```
      48264 can get back, 48 CANNOT — on 4 map(s), 3 of them stranded WHOLE
        10.6  CELADON CITY      16 of  16     1.58  SILPH CO.       15 of 15
        1.46  ROCKET HIDEOUT    15 of 15      3.7   FUCHSIA CITY     2 of 881
      the last step in was: door x21, hop x2
```

`10.6`, `1.58` and `1.46` are **lift cabins**. Every floor has a door into them and their own exit
is the runtime sentinel, which the walker reads and then `continue`s past.

**That is half a reading.** `Warp.Dynamic` was derived to stop this walker reporting nineteen
ordinary exits as holes in the world; it understands the sentinel well enough not to call it a
hole, and not well enough to come back out through it. So the walk steps into three lifts, counts
each as a map reached, and stands in it forever.

The FUCHSIA two are a genuine pocket behind a ledge, and they are the fixture this milestone
needed (below).

## Asked of the map data alone, which does not know the walk exists

```
    the door each one names
      1294 warp(s): 19 decided at runtime, 0 name a map this file lacks, 0 name a warp it lacks
      of the 1275 left: 920 name THIS door back, 237 come back by another door, 118 ONE WAY

    CONTROL: the NEXT door along on the same map
      of the 1275 left: 219 name THIS door back, 233 come back by another door, 823 ONE WAY
```

**920 against 219 is the whole of the evidence** that a warp pair is a pair. Most maps' doors all
lead to the same place, so "comes back to this map" is nearly free — it scores 237 against 233,
which is to say nothing at all. The tight half is the one that moves.

The 118 one-way doors are the lifts (24 of them), SEAFOAM ISLANDS' holes, and the ROCKET HIDEOUT's
spinning floor. Nothing in this project could say so before.

## And the third kind of edge

A world is made of steps, borders and doors. Steps are symmetric unless a ledge says otherwise;
the doors are above. Nobody had asked the borders.

```
    116 join(s), 114 declared back from the other side, 2 NOT
      3.50  SEVII ISLE 6  Up -> 3.14 (THREE ISLAND), which declares Down -> 3.49, Left -> 3.48
      3.51  SEVII ISLE 7  Up -> 3.14 (THREE ISLAND), which declares Down -> 3.49, Left -> 3.48
```

**Three maps claim to be south of THREE ISLAND and it claims one of them back.** Two joins in a
hundred and sixteen, both onto the same neighbour. Whatever that is, it is not the format going
wrong at random.

## The nine rooms with no way out, read without walking

```
    0.0 – 0.4  (the rooms above a POKéMON CENTER)   0.1 and 0.4 have 19 doors in from 19 maps
    1.46 ROCKET HIDEOUT   1.58 SILPH CO.   2.11 TRAINER TOWER   10.6 CELADON CITY
```

Nine maps whose **every** exit is the sentinel, off the warp lists with nothing walked. The walk
gets into three of them. The other six are entered by a script rather than by standing on a
square, which is why they have never appeared in a reach number at all — and `0.1` and `0.4` each
having exactly nineteen doors in, one per town, is the cable club being nineteen doors to one room.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the target does not reach itself | 4 | 4 |
| the edges are read forwards | 4 | 4 |
| a ledge hop is enqueued without being recorded | **1** | **0** |
| ... and again, with the decoy | 2 | 2 |
| coming back to the map counts as coming back through the door | 1 | 1 |
| the runtime sentinel is an ordinary door | 1 | 1 |
| the opposite of Up is Up | 2 | 2 |
| **CONTROL:** the reverse walk uses a stack rather than a queue | **0** | **0** |

**The third break is the one worth keeping.** "Every enqueue is recorded" is exactly the rule this
measurement rests on, and on an open map it could not be broken: the landing squares have
neighbours, and the walk records a step into a square it has already seen, so every one of them
still had an arrival from the side. The rule was real and unreachable — 240's shape.

The decoy is a ledge whose landing has nothing beside it. There are two of them in FUCHSIA, which
is where this milestone found them.

The control's nought is 261's kind rather than 257's: a reachable set does not depend on the order
its walk visits neighbours in, so there is nothing there to guard.

## What is left

* **What the lifts are worth.** The walk gets into three and models them as rooms with no exit.
  Joining every floor with a door into a lift is the same upper bound the boat already takes, and
  the difference it makes to reach has never been measured. It is a lever and it is MODELLED.
* **The other six sentinel rooms** are entered by script, so `--play` with `--through-scripted-doors`
  is the setting that would walk into them. Not run.
* **The two borders.** `3.50` and `3.51` both name THREE ISLAND upward and it names `3.49`
  downward. Whether a walker crossing north from either comes back onto a third island is a
  measurement this milestone did not take — neither map is reached without the boat.
* **Every reach number in the prompt is forward.** This adds a second column to exactly one of
  them. The floor table's six rows have not been asked.

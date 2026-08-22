# Milestone 306: what a flag costs

The wall list is **ranked by who stands in a doorway** — a 3×3 question about a door's own square —
and it has been right about every blocked door this project ever chased. 305 broke the assumption
underneath it: `2.1 TRAINER TOWER`'s fence stands **five squares** from the door it shuts, so flag
`0x0005` is not on the wall list at all, and it is holding nine maps.

**A count of people in doorways is not a count of maps.**

## The prediction, written before the instrument ran

> * ranked by cost, the wall list is a DIFFERENT list. `0x0005` and `0x0089` are the top two.
> * the current list's top entries — `0x0013`, `0x0012`, `0x0053`, `0x0017` — cost NOUGHT maps.
> * most gating flags fence no door at all.

All three hold. Ranked by what the run loses, the list is **two flags long**:

```
      flag     doors   maps   anything moves it     in a doorway   what is behind it
      0x0005       1      9   yes                   NO — unseen    2.11 TRAINER TOWER, +8 more
      0x0089       1      8   NOTHING IN THE FILE   yes            1.102 MT. EMBER, +7 more
```

Of the 322 flags that gate something, **two fence a door**. The other 320 hold villagers on squares
nobody needs, and the four at the top of the doorway-ranked list cost nothing between them.

And the two disagree on everything that matters:

* **`0x0005` — nine maps, and the doorway test cannot see it.** A script sets it, so it is live
  content: the walk simply never runs whatever does.
* **`0x0089` — eight maps, and nothing in the file moves it.** Visible to the doorway test, which
  is why this prompt has carried it since 190, and unopenable by anything readable.

That second column is the one a shipped game has to answer, so it is printed beside the cost rather
than left to a separate reading: **a flag nothing sets is not a door that opens later, it is content
nobody can reach.**

## A person a flag hides is not a rock a move shifts

Both are walls to the walker and they open in completely different ways, so the count of each now
prints beside the other:

```
    And 200 thing(s) in the world are shifted by a FIELD MOVE rather than by a flag,
    needing 3 different move(s): 97 x move 249, 54 x move 70, 49 x move 15.
      move 249 shifts  97 thing(s) and is known at 3 of 6 setting(s), first at --play --say-yes --boat
      move  70 shifts  54 thing(s) and is known at 5 of 6 setting(s), first at --play --say-yes
      move  15 shifts  49 thing(s) and is known at 6 of 6 setting(s), first at --play
```

Two hundred rocks, three moves, and **the run learns all three** — one at every setting, one at five
of six, one at three of six. So the rocks on the mountain are not what is holding MT. EMBER: the run
can shift those. What holds it is person 3 at `(42,40)`, who talks, whose script compares variable
`0x4076` against 4, and who is hidden by a flag nothing sets. **Neither of the two fences above is a
rock**, and the reading says so out loud rather than leaving the two kinds of wall to look alike.

## The breaks

| break | predicted | killed |
|---|---|---|
| a door into a map the run reaches anyway is charged for it | 1 | **1** |
| the closure leaks into ground the run already has | 1 | **1** |
| a door shut by the GROUND is charged to a flag | 1 | **1** |
| the doorway test widened from 3×3 to 17×17 | 1 | **1** |
| **CONTROL:** the ranking's tiebreak reversed | **0** | **0** |

Five predictions, five hits. The first time in a while.

## What is left

* **`0x0089` is a decision, not a reading, from here.** Eight maps behind a person nothing in the
  file removes. The options are to leave it shut, to model an opener (MODELLED, and it would be the
  first person this project moves on its own authority), or to mark the door shut-for-ever in the
  world file so a client can treat it as scenery. **The derived version of "block it off" is the
  third**: the rule is *a fence held by a flag nothing moves*, which is a sentence about the file
  rather than a hand-written list.
* **`0x0005` is a `--play` question**: what sets it, and why does no run get there? Nine maps.
* **The CABLE CLUB (`0.1`, `0.4`) is the cartridge's own two-player room** — two chairs facing a
  link machine. It needs nothing blocked (305: nothing in the file lands anybody in its pocket) and
  one thing written: an attendant, MODELLED, because the cartridge enters it through a routine.
  `PeopleAreSilent` has been holding that place open since 288.
* The cost reading asks only about **doors**. The same question could be asked of every pocket, and
  of the 21 people who never arrive at all.

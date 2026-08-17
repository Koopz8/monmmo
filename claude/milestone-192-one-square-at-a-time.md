# Milestone 192: one square at a time

A scene that walks somebody aside had its steps summed and the total applied in one jump, from
wherever that person already was. The run is a fixpoint, so it plays the scene again on every
pass, and the jump is applied again.

On the floor run, **364 of 426 of those landed off the edge of the map.** 41 landed on a square
somebody could stand on.

```
  3.2  person 5 at (-29,26) on a 48x40 map
  3.2  person 7 at (46,95)  on a 48x40 map     — walked thirty times
```

*Somebody is standing in the way* and *a person removed is a person not in a doorway* are both
computed against those squares. Five walks in six were being answered against a square that
does not exist.

---

## What the cartridge does

`applymovement` walks somebody one step at a time and they stop when they cannot go on. The
collision grid is already here, and it is the same oracle the step bytes were derived against in
the first place — `MovementLists`' own comment says so: *a direction mapping that is wrong sends
somebody through a wall, and sends them through a wall repeatedly.*

So `PlayedScript.Walked` carries the **steps** now rather than their sum, and the walk does the
walking: one square at a time, stopping at the first square nobody can stand on, and the steps
after that one do not happen either.

The read at PEWTER CITY, for the person who ended at `x = -29`:

```
08165BC0  4F 05 00 [08165D83]      applymovement person 5, list 0x08165D83
08165D83  10 12 12 12 12 12 12 12 12 12 FE
          walk-Down, then nine walk-Left  ->  dx -9, dy +1
```

From (42,20) that is (33,21), which is on the map. Person 5 is named by **sixteen**
`applymovement` sites around 3.2 — the same scene written once per story state — and the run
plays all of them, because which one is live is decided by a routine it cannot answer. Nine
applications of −9 is how (42,20) becomes (−29,26).

## What moved

Nothing on the headline, anywhere:

```
--play                                      183 / 150
--play --say-yes                            243 / 225
--play --say-yes --in-order                 243 / 227
--play --say-yes --boat                     390 / 287
--play --say-yes --boat --in-order          390 / 288
--play --say-yes --boat --surf --in-order   390 / 286
```

What moved is that **nobody is anywhere impossible**:

```
  52 people were walked out of where they stood by a script it ran — the other way a doorway opens
    one square at a time, stopping at a wall — a scene's steps applied as one jump put 364 of 426
    of these off the edge of the map
  nobody in this world stands on a square that is not on their own map
```

The second line is asked of **every** person the cartridge places, not only the ones a scene
walked — because once the walk stops at a wall nothing the run does can put anybody off the map,
and a check nothing can fail is not a check. That half is about the export, and on this image
the answer is yes, everywhere. It has a decoy fixture: a world that places somebody off their
own map, so an answer of yes comes from something that could have said no.

---

## And the coupled pair this does NOT fix, measured

The boat run's passes read `264, 269, 302, 390, 381, 381` and the headline is 390. Same state,
two different answers — because the settle test compares **counts**:

```
PROBE top of pass 6: reach 381 ... moved 52 movedsum 1343
PROBE final walk   : reach 390 ... moved 52 movedsum 1347
```

`moved` is keyed by person. A pass that walks somebody another square changes a value and not
the count, so the loop reads it as *nothing opened* and stops — and the reported reach is the
reach of a state one pass past the last one it played. **Four squares of one person's position
is worth nine maps.**

Two changes would close it and they are coupled. Both were built and measured this session, and
neither is shipped:

| | boat run | passes | tests |
|---|---|---|---|
| today | 390 / 288 | 6 | green |
| positions in the settle test | 390 / 288 | **22** (backstop is 24) | breaks `AndTheSquareHeStepsOntoIsBlockedInstead` |
| + a scene walks people once | **381** / 288 | 6 | breaks `TwoWalksCompound` |

Positions alone makes the walking *worse*: the loop keeps replaying the scene until the person
hits a wall, so one talk becomes a walk across the room. The pair together is stable — six
passes, no churn — and takes the boat run **down** nine maps, because 390 was partly produced by
walking people repeatedly out of their own doorways.

**Down is the honest direction and that is not enough to ship it.** What stops a scene running
twice on the cartridge is a flag, and nobody has read it. `--who-writes` and `--in-the-image`
are the instruments and `3.2` is the map: sixteen `applymovement person 5` sites, and one of
them is the one that happens.

The measurements are here so the next session does not have to take them again.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the sum is applied in one jump again | `NorOffTheOtherEdge`, `AWallStopsThemBeforeTheEdge`, `NobodyIsWalkedOffTheEdge…` |
| the edge bounds them but walls do not | `AWallStopsThemBeforeTheEdge` |
| the bound eats everything and nobody moves | `SomebodyWalkedOntoOpenGroundActuallyMoves`, `NobodyIsWalkedOffTheEdge…` |
| the export check only asks the ones a scene walked | `SomebodyTheWorldItselfPlacesOffTheMapIsReported` |

The last one came back **green** first time round: the export half had nothing that could fail
it, because the run's own half can no longer produce one. That is the standing rule — *a guard
nothing can fail needs a decoy fixture or needs removing* — and the decoy is the fourth test.

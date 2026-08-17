# Milestone 194: the map is half the key

193 shipped an hour before this one and it had a fault in it. This is that fault, found by the
instrument 193's own next-task line asked for.

---

## What 193 got wrong

193 keyed a movement on the address of the `applymovement` command that produced it: *the same
command is the same movement.* That is true on one map and false across the cartridge.

**One nurse's script is attached to person 1 on nineteen Pokémon Centres.** One shopkeeper's is
on nineteen marts, and one gym guide's is on eight. A block reached from eight maps is eight
scenes, and a player talks to all eight.

```
walk sites reached from more than one map: 3 of 83
  0x081C50D4 on 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8
  0x081C50E3 on 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8
  0x081C5514 on 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8
```

Three of eighty-three, each losing seven of its eight. Every test 193 shipped used one map, so
not one of them could see it.

The key is `(map, command)`. The reach did not move — 183, 243, 381 at every lever setting — and
the counts did:

```
--play --say-yes --boat --in-order    83 commands -> 104
--play --say-yes --boat               99 commands -> 142
```

This is the shape this project keeps finding, one milestone later than usual: **right at every
step and quietly wrong at the end**, and the number that would have shown it was not being
printed.

---

## `--entries`, which is what found it

193's next-task line asked for a count of the entry-stub shape. Written, it answered a different
question first — and the first answer was wrong in exactly the way 193 was:

```
  the biggest room, grouped by target address alone:
    0x081A6578 — 20 door(s)
      2.10 person 1, 5.4 person 1, 6.5 person 3, 7.3 person 1, 8.0 person 1, ...
```

Twenty doors into one room, and it is twenty different nurses in twenty different towns.

Grouped properly:

```
  2915 script(s) the map scan opens, 227 of which do nothing but hand over to another block
  24 block(s) are entered by more than one door ON THE SAME MAP, by 68 doors between them
    22 of those are ONE SCENE ENTERED SEVERAL WAYS — every door says a different number
    2 are several scripts that happen to share a block
  and 12 block(s) are reached from more than one MAP — shared routines, a different thing
```

### And the number they say is the discriminator

Same map, same target, and two opposite findings:

```
3.14 -> 0x08167A59 — 5 door(s), saying 0, 1, 2, 3, 4
  trigger (7,26) (8,26) (9,26) (10,26) (11,26)        five squares of one line, crossed once

3.14 -> 0x0816786F — 6 door(s), all saying 2
  person 3, person 4, person 5, person 6, person 7, person 8    six people sharing a script
```

A stub that announces which door it came in by says a **different** number per door. Six people
all saying `2` are not announcing anything. That is the whole distinction and it is in the
bytes — which is what `0x4001` is for, and the other half of milestone 173's ruling that it is
scratch rather than a story counter.

The scenes it finds are lines you cross: five squares outside the CELADON gym, four at PEWTER's
corner, three at the lab door saying `0x4002 = 1, 2, 3`.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the map drops out of the key again | `OneScriptSharedByTwoMapsIsTwoScenes` |
| a room is grouped without its map | `TheSameBlockOnTwoMapsIsASharedRoutineAndNotARoom` |
| doors saying the same number count as announcing one | `DoorsSayingTheSameNumberAreACrowd` |
| a block that does something of its own counts as a door | `ABlockThatDoesSomethingOfItsOwnIsNot`, and one more |
| a handover that writes the story's own memory counts | `AHandoverThatWritesTheStorysOwnMemoryIsNotADoor` |

Every fixture here has two maps in it or two numbers in it, because one of each is what 193 had
and one of each is what could not see this.

---

## What is still owed

* **38 of the run's script executions are a scene it has already played.** The walking is
  handled. The routines it counts, the questions it counts, the stopped reads it counts and the
  items it is refused are all still counted per script, and 38 of those are the same scene
  arriving again by another door. Nobody has looked at what that does to the error bars this
  project quotes.
* **12 blocks are reached from more than one map.** Anything else in this repository keyed on a
  script address alone is wrong about them. That is a grep worth doing rather than a guess.
* `--entries` reads only the scripts the map scan opens, which is 0.6% of the file. The same
  sweep asked of the whole image is `--in-the-image`'s question and has never been asked here.

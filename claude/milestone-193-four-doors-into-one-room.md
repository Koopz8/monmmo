# Milestone 193: four doors into one room

192 left a question and told the next session to read a flag before touching it: *what stops a
scene running twice on the cartridge?*

It is not a flag. There was only ever one scene.

---

## The bytes

PEWTER CITY, `3.2`. Four consecutive twelve-byte blocks:

```
08165D8E  69  16 01 40 00 00  05 [08165DBE]  02      lockall; 0x4001 <- 0; goto the scene
08165D9A  69  16 01 40 01 00  05 [08165DBE]  02      ...              1
08165DA6  69  16 01 40 02 00  05 [08165DBE]  02      ...              2
08165DB2  69  16 01 40 03 00  05 [08165DBE]  02      ...              3
```

Four trigger squares, one per way of crossing the line outside the gym, each announcing **which
door it came in by** in `0x4001` and jumping to the same block.

Person 7 on the same map is written the same way, with the person's own script as the fourth
entry:

```
081662A9  6A 5A  16 01 40 00 00  04 [081662DE]  6C 02   talk to them: 0x4001 <- 0, call the scene
081662B7  69     16 01 40 01 00  04 [081662DE]  6B 02   cross a square: 1
081662C4  69     16 01 40 02 00  04 [081662DE]  6B 02                    2
081662D1  69     16 01 40 03 00  04 [081662DE]  6B 02                    3
```

This is the other half of a ruling this project already made and made correctly:
`0x4001` is scratch, not a story counter — 285 scripts write it. **This is what they write it
for.** It is not a precondition; it is a return address in a variable.

A player crosses one square, or talks to the person. A fixpoint stands on every square and
talks to everybody, so it plays the scene once per door — and every entry executes **the same
commands at the same addresses**.

## The measurement

```
--play                              416 walk applications, 61 distinct applymovement commands
--play --say-yes --in-order          91 applications, 46 commands
--play --say-yes --boat --in-order  206 applications, 83 commands
```

Sixty-one commands, asked for four hundred and sixteen times.

## The change

**The same command is the same movement, and it applies once.** `SceneBeat.Walk` carries the
address of the `applymovement` it came from, and the walk applies each address once per run.

That is identity, not a decision. Nothing here is marked MODELLED because nothing was chosen:
two entries into one block run one command, and one command is one movement.

## What moved

| run | before | after |
|---|---|---|
| `--play` | 183 / 150 | 183 / 150 |
| `--play --say-yes` | 243 / 225 | 243 / 225 |
| `--play --say-yes --in-order` | 243 / 227 | 243 / 227 |
| `--play --say-yes --boat` | 390 / 287 | **381** / 287 |
| `--play --say-yes --boat --in-order` | 390 / 288 | **381** / 288 |
| `--play --say-yes --boat --surf --in-order` | 390 / 286 | **381** / 286 |

Down nine, and down is the honest direction: **390 was reached by walking people repeatedly out
of their own doorways.** No flag count moved, and no run below the boat moved at all.

## And it closed 192's other half for free

192 measured a contradiction it could not resolve: the boat run stopped at pass 6 with its own
reach at 381 and reported **390**, because the settle test compares counts and `moved` is keyed
by person — a pass that walks somebody further changes a value, not the count.

```
before   top of pass 6: reach 381  moved 52  movedsum 1343
         final walk   : reach 390  moved 52  movedsum 1347

after    top of pass 6: reach 381  moved 37  movedsum 490473
         final walk   : reach 381  moved 37  movedsum 490473
```

**Identical.** The state stops changing when the loop stops, so the reported reach is the reach
of a pass the run actually played. 192 sketched two changes to force that — positions in the
settle test, and a modelled once-per-scene rule — and costed both. Neither is needed. Reading
the bytes made both unnecessary, which is the fourth time in this project that a measurement has
retired a design.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the same command applies every time it is reached | `TwoEntriesIntoOneSceneMoveSomebodyOnce` |
| the key is the person rather than the command | `TwoDifferentCommandsAreTwoMovements`, `TwoWalksCompound` |

The first came back **green** the first time it was tried. The test asserted `WalkSites` — the
counter this milestone adds — and the break leaves the counter alone while removing the
behaviour. Rewritten against two doors one above the other, so that *one step* and *two steps*
are different answers, it caught it.

That is the fifth shape of forgiving fixture and the plainest one yet: **a test that reads the
instrument instead of the world.** The counter and the effect are two different claims, and only
one of them is the thing.

# Milestone 305: two people and seventeen maps

304 asked what the run did at each of the forty-three doors into an unreached map and got one
answer forty-three times: **it never got near it.** Then it stopped at *"they are inside 287's
pockets"*, which names the ground and not the fence.

288 had sorted fences into three kinds seventeen milestones earlier and had never once been asked
about a door.

## The prediction, written before the instrument ran

> * all 43 door squares come back SEALED — 0 same-ground and 0 behind a ledge.
> * the pockets are small — 287's counter pocket is twelve squares, so median under 20.
> * every pocket holds at least one OTHER warp, which is the way in the run does not use.

**Two of the three were wrong, and the first one was wrong in the only way that matters.**

```
      what fences the door's own square         the 43   the doors it went through
      nothing — the walk stood on it                 0                        1165   <- known
      SOMEBODY IS STANDING IN THE WAY                2                           0
      steps reach it from where the walk stood       0                           0   <- MUST BE NOUGHT (288)
      a ledge hop reaches it and no step does        0                           0
      SEALED — neither steps nor hops reach it      41                           0
```

The first run of it said **2 same ground** — a count 288 says cannot happen, because steps are
symmetric over walkable ground and a door the walk could have walked to and did not is a walk that
stopped early. **A count that must be nought is the best check an instrument can carry** (240), and
this is the first time in this project that one has actually fired.

## And a union of six runs is not a run

Before chasing it, the question was re-asked of each setting on its own — the union puts one run's
square beside another run's grid, which is 283's rule, and the must-be-nought count is exactly the
kind of sentence that breaks under it:

```
      setting                                    stood on   in the way   same ground   behind a ledge   sealed
      --play                                            0            0             0                0       43
      --play --say-yes                                  0            0             0                0       43
      --play --say-yes --in-order                       0            0             0                0       43
      --play --say-yes --boat                           0            2             0                0       41
      --play --say-yes --boat --in-order                0            2             0                0       41
      --play --say-yes --boat --surf --in-order         0            2             0                0       41
```

Not an artefact: it is 2 in three separate runs and 0 in the three without the boat, which is only
because those three never reach the maps at all.

## The fourth fence is a person, and it is one person each

288's three kinds are all about GROUND. The walker has always had a fourth answer and never lent it
to anybody: squares it refused because somebody was standing on them.

```
1.97 MT. EMBER (42,39) -> 1.103 is fenced by person 3 at (42,40), graphics 49, movement 9,
     hidden by flag 0x0089 — beside the door, where the doorway reading can see them
2.1  TRAINER TOWER (15,6) -> 2.2 is fenced by person 5 at (10,10), graphics 243, movement 8,
     hidden by flag 0x0005 — 5 squares from the door, so the doorway reading cannot see them
```

Each refused square is opened on its own and the flood asked again, so the answer is not "some
people are about" but **which one**: in both cases exactly one person, and stepping aside would open
the way. 303 priced these two doors at **eight maps and nine**. Seventeen of the twenty-six maps
behind roots are behind two people.

**A FENCE IS NOT A DOORWAY**, and that is why one of the two has never been named. The reading that
names whoever is in a doorway asks a 3×3 question about the door's own square: it can see MT.
EMBER's boulder — which is the `0x0089` wall this prompt has carried since 190 — and it cannot see
TRAINER TOWER's, five squares up a corridor. Asking about paths instead of adjacency finds both.

The rule about who counts as a wall is **not re-derived here**. It lives in the walker, and a second
copy of it would be a second walker to keep honest (223), so the walk's own two lists are carried
out and handed over: `Blocked` for frontiers a MOVE would open, and the new `PeopleInTheWay` for
somebody rooted to a square. They are different lists and only the second is a person.

## The other forty-one, and the nineteen doors that are exits

```
      from     to        square      pocket   ways in   landed in from
      10.13    0.1       (  9,  1)        4         1   NOTHING
      10.13    0.4       (  5,  1)        5         1   NOTHING
      1.97     1.103     ( 42, 39)      653         6   1.100 MT. EMBER, 1.101, 1.103, and 2 more
      1.59     1.62      ( 25, 27)      122         3   1.61 SECTION 47, 1.62 SECTION 47
      1.75     1.76      (  6,  2)       79         1   1.76 SECTION 49
      2.10     2.11      ( 17,  8)        4         1   NOTHING
      2.1      2.2       ( 15,  6)      115         2   2.10 TRAINER TOWER, 2.2 TRAINER TOWER
```

**39 of the 43 sit in a pocket nothing in the whole world lands anybody in.** The ways in are the
warps inside the pocket — steps and hops cannot leave one, so nothing else can be a way in — and for
thirty-nine of them the only warp in the pocket is the door itself, with no warp anywhere naming it.

That is what 303's *"nineteen doors into a room, all of them behind a shop counter"* actually is:
**the nineteen doors are exits, not entrances.** A four-square pocket, one door in it, and nothing
in the file that puts anybody there. Whatever opens `0.1` and `0.4` is not a warp.

One more is landed in only from maps that are themselves unreached, which is 303's closure again
rather than a reason of its own. That leaves **three** doors whose pocket the run could be put down
in and never is.

## The breaks

| break | predicted | killed |
|---|---|---|
| the fourth fence asked after same-ground instead of before | 2 | **2** |
| the pocket flooded from the walk instead of from the door | 2 | **2** |
| an unspecified destination read as an index | 1 | **1** |
| nobody ever tried on their own, so no fence is named | 1 | **1** |
| the walker stops recording who it refused | 3 | **10** |
| **CONTROL:** the two floods computed in the other order | **0** | **0** |

The last real one over-predicted by seven. The standing list turns out to carry two whole test
classes written milestones ago — the prediction was wrong about the fixtures, not about the code
(32), and finding that out is the reason for predicting first.

## What is left

* **Two people, seventeen maps.** Flags `0x0089` and `0x0005`: what sets them is the question, and
  `--flags` already ranks who moves each one. If nothing readable sets either, they belong on the
  wall list beside SAFFRON's three.
* **`0.1` and `0.4` are opened by something that is not a warp.** Nineteen doors out and no door in.
  The candidates are a script warp and the lift sentinel, and both are readable.
* **Three doors whose pocket the run could be put down in and never is** — `1.62`, `1.76`, `2.2`.
  Each is now a single `--play` question rather than a map-shaped one.
* The fence reading only asks about doors. **Every pocket 287 counted could be asked the same
  question**, and "which person is the whole reason for this pocket" is a ranking nobody has.

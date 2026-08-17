# Milestone 198: `0x80`, and the square a shop is talked across

197 measured that the playthrough stands in front of at most one shop counter in the whole game,
and that every counter it misses is exactly two squares from the nearest floor it stood on —
11 of 11, 14 of 14, 19 of 19, every lever setting, no exceptions and no tail. It stopped there
on purpose: the rule about who may be spoken to is the most load-bearing thing in the project.

This reads what is on the square in between.

---

## `0x80`, read twice

Not taken from another game's behaviour table. That table has already been wrong here once —
three of the four ledge names it supplied did not survive contact with this cartridge, and
`0x3A` turned out to be on no square of the world at all.

**By what it stands beside.** `--counters` asks every shopkeeper in the file, not the ones a run
reached, and prints a control beside the answer:

```
  the behaviour byte of the 37 unwalkable square(s) beside a shopkeeper,
  against the same byte beside ANY of the file's people (1923 square(s)):
    0x80    34 beside a shop ( 91.9%)     171 beside anybody (  8.9%)
    0x00     2 beside a shop (  5.4%)    1305 beside anybody ( 67.9%)   ordinary ground
    0x9A     1 beside a shop (  2.7%)       8 beside anybody (  0.4%)
```

Ten-fold, and the control is the whole point: a wall stands beside everybody.

**By its own shape.** A counter is a square with somebody on one side and floor a player can
stand on *directly opposite*. Nothing else in a building looks like that — a wall has wall
behind it, and a person against a wall has no floor on the far side. Asked of every square in
the file:

```
    0x80     728 unwalkable square(s) in the world,   164 sandwiched  ( 22.5%)
    0x00   92566 unwalkable square(s) in the world,   278 sandwiched  (  0.3%)   <- the control
```

Seventy-five-fold.

**22.5% and not most, and that is the shape too.** A counter is a *run* of squares — three or
four tiles long — and only the one the clerk stands behind has anybody behind it. The rest have
wall behind them and are still counter. A number that came back at 90% here would have been the
suspicious one.

Two independent readings, two controls. The evidence is written onto the constant rather than
into a commit message.

---

## What it cost

The walk may now be spoken to from across **exactly one** counter square. Measured at all six
lever settings, before and after:

| | reach | flags before | flags after |
|---|---|---|---|
| `--play` | 183 | 150 | 150 |
| `--play --say-yes` | 243 | 225 | **227** |
| `--play --say-yes --in-order` | 243 | 227 | **229** |
| `--play --say-yes --boat` | 381 | 287 | **289** |
| `--play --say-yes --boat --in-order` | 381 | 288 | **290** |
| `--play --say-yes --boat --surf --in-order` | 381 | 286 | **288** |

**Reach did not move at any setting**, which is right and worth saying: a shopkeeper opens no
doors. Two flags at five of the six, and nothing at the floor — whatever they are behind needs
`--say-yes` first.

And every counter is now reached, exactly the ones that were two away and no others:

```
--play                                11 of 11
--play --say-yes                      14 of 14
--play --say-yes --boat --in-order    20 of 20
```

The shopping list's fourth entry arrives where it belongs:

```
    did NOT buy POKé DOLL (0x050) at 10.3: cannot afford it — 1000 against 0 left
```

1000 is READ. 197 had that entry filed as a reach problem and it is a money problem after all —
the reverse of the correction 197 itself made, and it took the rule change to tell.

## What else opened

| | before | after |
|---|---|---|
| distinct blocks reached, boat run | 1212 | **1281** |
| places calling routines it cannot answer | 601 | **763** |
| routine `0x187` asked | 78 | **178** |
| routine `0x188` asked | 31 | **88** |
| places stopped at a command with no width | 12 | **17** |
| distinct such commands | 6 | **7** |

`0x187` and `0x188` more than doubling is the shop menu itself: talking to a clerk calls a
routine this project cannot execute, so the error bars grow because there is more of the game
being touched. That is the honest direction.

**And the seventh command is new.** `0xC1` had never been reached by anything:

```
before   0x92 5, 0xB3 3, 0xA4 1, 0x7E 1, 0x36 1, 0x95 1
after    0xB3 7, 0x92 5, 0xA4 1, 0xC1 1, 0x7E 1, 0x36 1, 0x95 1
```

A command with no width that existed on the far side of a counter. Nothing could have found it
except standing where the player stands.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the counter clause removed — back to plain adjacency | `SomebodyBehindACounterIsSpokenToAcrossIt` |
| anything two away can be talked to, counter or wall | `SomebodyBehindAnOrdinaryWallIsStillOutOfReach` |
| plain adjacency dropped, only counters reach | `SomebodyStandingOnOpenFloorIsStillSpokenToNormally` |

Three breaks, three *different* tests, none green. The fixture is a room with **a counter and a
plain wall two columns apart** and identical in every other respect — same row, same solidity,
same person behind them — because a rule that reaches two squares through anything would pass
every test that only had the counter in it. The third person stands on open floor and is the
ordinary case asserted in advance.

A fourth test says the rule reaches across one counter square and not along a run of them. Those
two rules agree about every clerk in this game and disagree in the fixture, and the narrower one
is what was measured: the run stood two away, never three.

2779 → 2783, all green.

---

## What is still owed

* **`0xC1`.** One place, newly reachable, no width. The job that found `0x1F` and `0x6F`.
* **`0xB3` went from 3 places to 7.** More evidence for a command that was already on the list.
* The two flags are counted and not named — `--play` prints the number, not the set. The same
  gap as the five wall flags that "look moved and are not".
* Money is now the top of the shopping thread for real: three drinks at 200/300/350 and a POKé
  DOLL at 1000, all READ, all at counters the run now reaches, against a purse of nought. The
  payout table has still never been located.
* `--counters` reads the exported world rather than the whole image. `0x80` appears on 728
  squares of 425 maps; whether it appears anywhere the map scan does not open is
  `--in-the-image`'s question and has not been asked.

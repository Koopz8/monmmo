# Milestone 297: the copy is commoner behind the call

296 named its own first leftover: *`copyvar` into a slot is a WRITE this does not see. `Before`
records a `setvar` and nothing else, so a value copied into `0x8004` is invisible as an argument
and its source is read as spending a slot. Both halves of that are wrong in opposite directions
and neither is measured.*

**A caveat you can state you can usually measure** (42), and until you do you do not know whether
it is a footnote or a fifth of the answer. Measured, it is 26 places and 12 routines — and the
floor refuses it.

---

## The floor is the same walk run forward

Nothing behind a call can be an argument to it. So the same backward walk, run forward under the
same two rules, is the floor under itself: a kind of write that IS an argument has to be commoner
in front of a call than behind one. And the plain `setvar` — the thing this project already reads
as an argument — goes in the table as the row that says what that looks like (68).

The copies split three ways by the band the source word falls in, which is READ off the two
namespaces 264 measured rather than asserted: a variable id here is `0x4000` upwards or `0x8000`
upwards, so a second word below `0x4000` is not a variable at all and can only be a number.

```
      what is written into the slot     in front   behind   ratio   routines   NEW
      a SETVAR                               244       99    2.46         37     -   <- known
      a copy of a literal                      6       12    0.50          4     3
      a copy out of the save                   8        6    1.33          3     0
      a copy out of another slot              13       45    0.29         10    10
```

> **None of the three behaves like an argument.** The kind that supplies ten of the twelve new
> routines is the one that scores worst, and 33 of its 45 behind-a-call places copy `0x800D` — a
> script moving a routine's reply about, which is the opposite of handing something over.

Taken out of both columns — both, because out of one only the ratio moves for a reason about the
arithmetic rather than about the cartridge — that row is 11 against 14, **0.79**. Still under one,
and a third of the row whose answer is known.

**296's 37 / 29 / 8 stands, and it stands for a measured reason now rather than because nobody
looked.** Four readings of that number in five milestones and this is the first that did not move
it.

The other half of 296's caveat cannot be wrong: a slot the destination of a copy names is marked
spent, which reads a WRITE as a read — and a write kills an earlier `setvar` exactly as a read
does, so the two cannot differ. 57's shape with nothing to fix. **Ask whether the thing a break
would change can affect the answer at all before writing a fixture for it** (64).

## One walk, not two

`SpecialCalls.Around` is the run of commands between a call and its neighbour, and it now runs in
either direction and is shared with the reading that tests it. Writing a second loop is the fault
this repository has fixed at 220, 224, 251 and 258 — a second copy means the suite guards one of
them and every break lands on the other (53). The refactor moves nothing: 37 / 29 / 8, 2185
arguments, converging at twelve and identical at 4096, exactly as 296 printed them.

## And what the one kind above its floor found

A copy out of the save scores 1.33 and adds **no routine at all** to what 296 already reads. What
it points at instead is `0x403A`.

It is written on four maps and named on no other. It is handed to `special 0x0132` at **four of
that routine's four places**. Its whole-image site count is 24 against a same-high-byte unused-id
floor of most-24 — no discrimination — but **21 of them read as script against that floor's median
of 1 and most of 3**, which is.

```
      map      name              values   maps in   warps in   the values
      1.46     ROCKET HIDEOUT         3         3          7   0,2,3
      1.58     SILPH CO.             11        11         11   4,5,6,7,8,9,10,11,12,13,14
      10.6     CELADON CITY           5         5          5   4,5,6,7,8
      2.11     TRAINER TOWER          1         9          9   3   <- and this one does not
```

**Three of the four take exactly one value per map that can warp there.** All four are lift
cabins: `1.46`, `1.58` and `10.6` are three of the four maps `--the-way-back` calls sentinel-only
rooms the walk enters and never leaves (74), and `2.11` TRAINER TOWER is the fourth. Two structures
built for different questions, one list.

The floor is the same question asked of every (variable, map) pair a `setvar` in the map scan
writes — **and it has to have the one-door pairs counted out**. A map with one way in is matched by
any variable written once, which is the blank entry of 264's item table in another shape (71):

```
      maps in at least   pairs   match   share   0x403A at this size
                     1     252     114    45.2%   3 of 4
                     2     137      28    20.4%   3 of 4
                     3      56       5     8.9%   3 of 4
                     5      34       2     5.9%   2 of 3
```

The whole ladder is printed rather than one cut of it, because a cut chosen after seeing the answer
is a cut chosen by the answer (79). And the list of every variable that matches on EVERY map it is
written on is printed too: there are five, `0x403A` is **not** among them because TRAINER TOWER
does not match, and the widest any of the five manages is **2 doors** where `0x403A` manages 11, 5
and 3.

**What is READ**: a variable confined to four lift maps, taking one value per way in on three of
them, handed to one routine at every place that routine is called. **What is NOT**: what `0x0132`
does with it. Nothing branches on `0x0132` — it is one of the 111 routines called for their effect.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the walk does not stop at the neighbouring call | 3 | **3** |
| the adjacency check is the backward-only form | 1 | **11** |
| the save band starts one above where it does | 1 | **1** |
| records counted instead of byte positions | 1 | **1** |
| a slot something nearer already took is counted anyway | 1 | **1** |
| the cut is on the values rather than on the doors | 3 | **0**, then **2** |
| the answer is taken out of ONE column | 1 | **1** |
| **CONTROL:** the empty test written as an inequality | **0** | **0** |

Two misses, and they are the two different kinds.

**The adjacency break killed eleven where one was predicted, and the prediction was wrong about
the code** (61). The forward walk runs at every call in every fixture in the class, so a break to
it takes the class with it — which is what sharing one loop is for, seen from the other side.

**The door cut came back GREEN against all three of its fixtures, including the one named for
exactly that discrimination.** Every row in them had `values == doors`, and on such a row a cut on
the values and a cut on the doors are the same function. Fixture-lie 5 in a new place. And the
MATCH column can never see that break at all — a match has as many values as doors by definition —
so only the PAIRS column moves, which is what the fixture asserts now. Re-run: predicted 2, killed
2.

## What is left

* **The forward window is still four** and still chosen (294, 295, 296). Everything "compared
  against" in 291-297 rests on it and it has never been swept. Third milestone owing it.
* **`All`'s threading is still unguarded** (294).
* **What `0x0132` does.** Compiled code, the sixth wall of that kind.
* **Whether `0x403A`'s value is the DOOR or the FLOOR.** `1.58`'s values are 4..14 and its doors
  are `1.47`..`1.57` — map number less 43, exactly, for all eleven. `10.6` is 10.0..10.4 against
  4..8, map number plus four. `1.46` is 1.42, 1.43, 1.45 against 0, 2, 3, and no offset fits. Two
  of three is not a reading.
* **TRAINER TOWER.** One value, nine doors, and it is the row this reading does not get to drop.

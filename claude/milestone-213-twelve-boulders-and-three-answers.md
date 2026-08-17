# Milestone 213: twelve boulders, and three different answers

212 found fifteen gates holding CUT trees and ROCK SMASH rocks, and twelve more holding
something asked about a move and never taken off the map. It kept the twelve out of the
obstacle bucket on principle — *whatever clears a boulder is a different mechanism, and
widening the rule to catch them would be picking a shape to fit an answer* — and left them on
the owed list.

They have been read. Not folding them in was right, and there is now a measurement that says so.

---

## Which move, and where

`--coins` names its commands; the obstacle sweep said only "asked about a move". It says which
now, off the cartridge's own move table:

```
  15 gating flag(s) hold things asked about a move AND then taken off the map — CUT (15), ROCK SMASH (249)
  and 12 hold something asked about a move and NEVER taken off it — STRENGTH (70)
      0x0040  1.83 p1     0x0042  1.84 p1     0x0048  1.86 p6     0x0058  1.40 p12
      0x0041  1.83 p2     0x0043  1.84 p2     0x0049  1.86 p5     0x0059  1.41 p8
                          0x0044  1.85 p1     0x004A  1.86 p3
                          0x0045  1.85 p2     0x004B  1.86 p4
```

Six maps, and the cartridge names them: **SEAFOAM ISLANDS** (`1.83`–`1.86`, ten boulders across
four floors) and **VICTORY ROAD** (`1.40`, `1.41`, one each). All twelve run one script,
`0x081BE11D`.

So three script addresses account for **twenty-seven gating flags and a hundred and fifty-eight
objects**:

```
  0x081BDF13   checkflag 0x0821 ; findmove  15 ; ... ; 0x53 removeobject      CUT
  0x081BE00C   checkflag 0x0825 ; findmove 249 ; ... ; 0x53 removeobject      ROCK SMASH
  0x081BE11D   checkflag 0x0823 ; findmove  70 ; ... ; setflag 0x0805         STRENGTH
```

The third does not remove anything. What it sets is `0x0805` — one shared flag, not the
boulder's own — so the script that runs on a boulder never touches the flag that hides it.

## And the twelve are not one thing

That is where folding them in would have gone wrong. `--in-the-image` on their flags:

```
  0x0042   16 site(s), 2 reading as script, 1 opened by the map scan
             0x16825A  setflag  <- opened by 3.38 ROUTE 20, on load
  0x0058    4 site(s), 1 reading as script, 1 opened
             0x1684F4  setflag  <- opened by 3.42 ROUTE 23, on load, reached from 3.41 ROUTE 22's trigger
```

**Two of the twelve are set by an ordinary arrival script on another map**, which makes them a
reach problem and nothing else. `0x0048` and `0x0049` have setters the map scan never opened.
`0x0040`, `0x0041`, `0x004A`, `0x004B` and `0x0059` have nothing anywhere.

Twelve identical objects, one script between them, and their flags split **three ways**. A rule
that had called all twelve "an obstacle, cleared by knowing the move" would have been wrong
about at least two of them in a direction no later measurement would have questioned — the
bucket would have been named for a cause and the cause would have been false, which is trap 5
and the thing 211 and 212 were each caught by.

The five-bucket split is unchanged: **35 / 31 / 17 / 15 / 12** at the widest lever setting, 35
and 15 at every setting.

## What changed

`GatesThatAreObstacles` returns records now — flag, which moves, which scripts, and whether the
objects are removed — instead of two anonymous lists. Which move is the whole difference between
a tree and a boulder and the first version could not say it.

Five breaks, five catches, and one green first time: "every one of them is asked" was tested
against a fixture whose mixed gate also disagreed about removal, so the removal rule caught the
break by accident and the asking rule was untested. Re-broken against a gate holding a tree and
something silent that is *also* removed, caught.

That is the second milestone running where a fixture passed because a **different** rule in the
same function happened to catch the break. Worth carrying: when two conditions guard one thing,
a fixture that violates both cannot test either.

2848 → 2849 tests, all green.

---

## What is still owed

* The seven boulder flags with no setter are still in the 35. Whatever drops a boulder into a
  hole is not in this file's scripts, and `special 0x0187` at the head of all three obstacle
  scripts has never been read.
* `0x0805` is set by the STRENGTH script and shared by all twelve boulders. What it gates —
  if anything — has not been asked.
* Two boulder flags are set on ROUTE 20 and ROUTE 23 arrival scripts. Why an arrival script on a
  different map sets a boulder's flag is a question this milestone raises and does not answer.
* `0x0053` and the 31 people on the SILPH CO. floors — unchanged from 212.

# Milestone 212: fifteen of the gates are trees

211 cut the run's 110 shut gates into four and said the 44-strong "no opener anywhere" bucket
had not been looked at one by one. It has now, and the bucket shrank again — for the same
reason, one class further out.

---

## Who is behind them

`FlagGates` knew what each flag gates and never who. It does now, and the answer is that a count
of gates is not a count of anything (trap 3, again):

```
  44 gates with no opener            145 people behind them
  18 gates past the code boundary     95 people behind them
```

Two hundred and forty people behind sixty-two gates, and they are wildly uneven. `0x0053` holds
**31** on the SILPH CO. floors; `0x0012` holds **32** across thirty-two maps; `0x0013` holds 25.
Four gates hold a third of everybody.

Two of the 44 hold **nobody at all** — `0x084A` and `0x084B`, the ferry. Zero `setflag` sites in
sixteen megabytes. **That is the first evidence that `--boat` has to be MODELLED** rather than
merely being convenient: nothing in this cartridge's scripts opens the boat.

## And a hundred and forty-six of them are trees

The big families collapse. Every person behind `0x0011`–`0x001F` runs one of **two** scripts:

```
  0x081BDF13   special 0x0187 ; lockall ; checkflag 0x0821 ; findmove 15  ; ... ; 0x53 removeobject
  0x081BE00C   special 0x0187 ; lockall ; checkflag 0x0825 ; findmove 249 ; ... ; 0x53 removeobject
```

Move 15 and move 249. **They are the CUT trees and the ROCK SMASH rocks** — one per map across
thirty-odd maps, each map's copy behind its own flag of one fifteen-flag family. `--script-map
1.82` shows nine of them on ROCK TUNNEL sharing a single script address.

They read as the code boundary because nothing sets their flags — and that is true and
misleading in **exactly** the way the pickups were at 211. The script asks who knows the move,
takes the object off the map with `0x53`, and the flag that keeps it off is set by the routine
rather than by any `setflag`. Same mechanism, one class out. Two milestones running, a bucket
called "the boundary" was holding things that open.

### Found by shape, and the shape is two halves

`GatesThatAreObstacles` looks for a gate whose objects are **all** asked about a move **and**
taken off the map. Both halves are load-bearing and both are broken and caught: something
removed with no question is not an obstacle, and something asked and never removed is not one
either.

That second case is real and it is **kept apart rather than folded in**. Twelve gates hold
something whose script asks about a move and never removes anything — the STRENGTH boulders are
among them, asking about move 70 and staying exactly where they are. Whatever clears those is a
different mechanism, and widening the rule to catch them would be picking a shape to fit an
answer. They stay in the boundary bucket with a question against them.

The two addresses this cartridge uses are **printed by the instrument, not written down**.

## Five buckets

```
--play                     101 picked up   38 reach   35 boundary   15 obstacles   12 past = 201
--play --say-yes            60 picked up   36 reach   35 boundary   15 obstacles   13 past = 159
--play --say-yes --boat
        --in-order          17 picked up   31 reach   35 boundary   15 obstacles   12 past = 110
```

**35 and 15 at every lever setting** — the two numbers that are facts about the cartridge do not
move when the levers do, which is 211's trap 13 applied on purpose rather than discovered. The
past-the-boundary column jitters by one, because which gates a run happens to open changes which
are left to classify; that is a fact about the run and it is allowed to move.

So the honest boundary is **35 flags**, not 110 and not 44 — and 12 of the 35 are asked about a
move and might not belong there either.

Six breaks, six catches. One came back green first time: the "asked and never removed" rule was
tested against a fixture with no gate holding *both* a tree and a boulder, so a break that
dropped the *never removed* half had nothing to fail against. Re-broken with that gate in place,
caught.

2841 → 2848 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The 35.** Thirty-five flags, and 12 of them hold something asked about a move. That dozen is
  the next thing to read, and `--in-the-image` will climb any of them.
* `0x084A` and `0x084B` gate the ferry and nothing sets them. Whether that makes `--boat`
  necessary or merely unavoidable is a sentence somebody should write in the floor section.
* `0x0053` holds 31 people across the SILPH CO. floors with no setter. The doors are open (176,
  181) and the people are still held — those are different facts and the second has never been
  looked at.
* What `special 0x0187` at the head of both obstacle scripts is. It is asked before anything
  else and its answer decides whether the obstacle runs at all.

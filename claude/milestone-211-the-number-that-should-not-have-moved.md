# Milestone 211: the number that should not have moved

210 printed **"110 gating flags it never set"** and nothing about any of them, and said so on its
own owed list. This is that list, sorted by why — and the first version of the sort was wrong in
a way its own output caught inside one run.

---

## Three buckets, and a number that moved when it could not

The first cut asked one question of each shut gate: does anything in the sixteen megabytes set
it, and did the map scan ever open the place that does. Three answers, and the third was labelled
**"NOTHING IN THE FILE SETS IT — the true boundary"**.

```
--play                                134  nothing in the file sets it
--play --say-yes --boat --in-order     56  nothing in the file sets it
```

**That is impossible.** Whether anything in the file sets a flag is a property of the FILE. It
cannot depend on which levers the run was given. Either the sweep was lying or the label was, and
the drop from 134 to 56 said seventy-eight gates in that bucket had been opened by a run — by a
run, into a bucket named for being unopenable.

## Sixty-five flags nothing names

The run sets 153 flags at the floor. **Sixty-five of them have no `setflag` site anywhere in the
image**, and they come in runs — `0x0154`–`0x017D`, `0x018D`–`0x0199`, `0x01BE`–`0x01D2`.

The first guess was the sweep's own filter: `EveryFlagMoved` keeps a site only if a read starting
*at that site* ends properly, and a `setflag` deep inside a long block might fail that while the
run, entering from the block's start, executes it fine. Measured, that is not it:

```
  0x0154 — 0 site(s) in the file, 0 of which read as script, 0 of which the map scan opened
    NOT ONE SETFLAG OR CLEARFLAG OF IT EXISTS IN THE FILE.
```

Zero raw sites, not zero surviving sites. Nothing named those flags at all.

What sets them is in this repository, written down and forgotten:

```csharp
// And a thing that is picked up is gone from the floor. The cartridge sets that flag
// inside the standard routine that does the handing over — code this project cannot
// follow, which is why only 7 of the 575 objects carrying a hide flag have a script
// that sets it. The object's own record says which flag, so the bookkeeping is
// readable even though the routine is not.
if (what.TakenAway != 0) flags.Add(what.TakenAway);
```

**Picking a thing up sets the flag that hides it, and the cartridge does that in compiled code.**
The run has known since it was written. The classifier did not, so it filed every one of those
gates under a name that meant "this will never open" about gates the run was opening in front of
it.

That is trap 5 for the third time in this project — *a fallback that names a cause is worse than
one that says nothing* — and this time the fallback was written the same session it was caught.

---

## Four buckets

```
--play                     101 picked up   44 boundary   38 reach   18 past the boundary   = 201
--play --say-yes            60 picked up   44 boundary   36 reach   19 past the boundary   = 159
--play --say-yes --boat
        --in-order          17 picked up   44 boundary   31 reach   18 past the boundary   = 110
```

**Forty-four at every lever setting.** The number that is a property of the file no longer moves
when the levers do, which is the check the three-bucket version failed and the reason to believe
this one. Nothing about the fix guaranteed it — the buckets could have come out 44, 43, 41 and
the classification would still have been wrong somewhere.

So 210's headline decomposes:

* **48 of the 110** a longer walk would open — 31 scripts never run, 17 things never picked up.
* **62 of the 110** it would not: 44 with no setter and nothing on the floor behind them, and 18
  set only where the map scan cannot see.

The five wall flags land where 205 and 206 put them: `0x0013` and `0x0017` in the boundary
bucket, `0x0012` in past-the-boundary with one setter.

### The order is a decision

A gate can be several of these at once, and the two overlaps go opposite ways:

* an **opened setter beats being on the floor** — a walk can reach it and prove it;
* **being on the floor beats an unopened setter** — the run demonstrably opens those, and calling
  one "past the boundary" would be false about a gate that is already opening.

Both directions are asserted, and breaking either one is caught.

Five breaks, five catches — the two orderings, the floor set going empty, a clearer counting as a
setter, and gates the run opened staying on the list.

2835 → 2841 tests, all green. Nothing the run does changed; this is four numbers where there was
one.

---

## What is still owed

* **The 44 have not been looked at one by one.** They are the only gates in the game with no
  readable opener at all, and `--in-the-image` will climb any of them.
* `onTheFloor` is `CanBeTakenAway` — an object that hands something over AND has a hide flag.
  Whether every one of those is really a thing on the ground rather than a person who leaves is
  not checked here.
* The 17 pickups the widest run never reaches are a shorter and more concrete list than the 31
  scripts, and nobody has printed which items they are.
* `--flags` still says "233 are the code boundary" from the ROM alone. That number and this 44
  are answering different questions and neither says so.

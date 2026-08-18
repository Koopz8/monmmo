# Milestone 216: ranked by the wrong number

215 left the routine ceiling as six routines in the mixed bucket — 61 places at the widest lever
setting, 44 of their 68 branches taken by nought. Reading them started with the list `--play`
prints, which is ordered by how often the run asked, and the order turned out to be nearly
backwards.

---

## The two lists are opposite

```
  routine 0x083 asked    1 time(s)  — nought takes 20 of its 22 branch(es)
  routine 0x084 asked    2 time(s)  — nought takes 19 of its 21 branch(es)
  routine 0x17C asked    2 time(s)  — nought takes  2 of its  3 branch(es)
  routine 0x194 asked   54 time(s)  — nought takes  1 of its 18 branch(es)
  routine 0x179 asked    1 time(s)  — nought takes  1 of its  2 branch(es)
  routine 0x189 asked    1 time(s)  — nought takes  1 of its  2 branch(es)
```

**`0x083` and `0x084` are asked once and twice between them and account for thirty-nine of the
forty-four.** `0x194` is asked fifty-four times and accounts for one.

`--play` ranked by asks and printed eight. Sorted that way, the two routines that carry nearly
the whole of the remaining ceiling sit below a routine that carries a fortieth of it — and the
truncation to eight was hiding them until 214 started naming the buckets' members explicitly.

That is trap 3, in the same block trap 3 was already applied once: *a count is not a ranking;
rank by the thing you actually care about.* The block exists to say where the run's silence
could matter, and how often the run bumped into a routine is not that.

The list is ordered by branches-taken-by-nought now, with the asked-count still printed beside
it so the two can be read against each other. The fixture that guards it makes the two orders
disagree on purpose: the routine asked most is the one whose silence decides least.

## What the two of them look like

Both are the same shape, and it is why nought takes their branches without nought being a value
either of them is tested against:

```
  0x1BB79C   26 0D 80 83 00        specialvar 0x800D, 0x0083
  0x1BB7A1   21 0D 80 02 00        compare 0x800D, 2
  0x1BB7A6   06 00 C2 B7 1B 08     if LESS goto 0x081BB7C2
  0x1BB7AC   26 0D 80 53 01        specialvar 0x800D, 0x0153
  0x1BB7B1   21 0D 80 01 00        compare 0x800D, 1
  0x1BB7B6   06 01 D0 B7 1B 08     if EQUAL goto 0x081BB7D0
  0x1BB7BC   16 0D 80 01 00        setvar 0x800D, 1
  0x1BB7C1   03                    return
```

**Ask, compare against two, and take the LESS arm** — which nought is. The arm says something and
returns nought; the fall-through returns one. `0x084` at `0x1BBB1E` is the same seven commands
with a different routine number and a different thing to say.

So these are subroutines that answer nought or one, and what they answer is decided by a routine
this project cannot run. The run's silence does not stop at the branch: it becomes the
subroutine's own return value, and whatever called it reads that.

**Which is where this stops.** Nothing here follows a call to attribute an answer — 214 added
the barrier that stops the scan doing it wrongly, and following one properly is an instrument
nobody has built. Both scripts are shared across many maps (`5.5`, `6.6`, `7.4` and more name
the same two addresses), so the callers are many and the reading is not one hexdump.

2867 → 2867 tests, all green — the two the order test replaced are the same two, rewritten.
Two breaks, two catches. Nothing the run does changed.

---

## What is still owed

* **Who calls `0x081BB79C` and `0x081BBB1E`, and what they do with the nought.** That is the
  whole of the remaining ceiling and it needs the call-following instrument, not another sweep.
* `0x0153` sits inside both of them, asked immediately after the LESS arm is not taken. It is a
  second unanswerable routine inside the first, and it has not been counted anywhere.
* `--play` still prints eight of the list and says how many more. Now that the order is by what
  nought decides, the truncation is at least cutting the right end — but a filter that keeps
  output readable has hidden the thing that mattered once already in this same block.
* Everything owed at 215 stands: `--who-reads` counts four commands, and the 650
  written-and-never-read in the save's band are individually askable and unasked.

# Milestone 175 — Reading the file, not the world

Delivered as `claude-211.bundle` on `9dfaa914f`. 2664 tests green from a clean clone at the
base — 24 of them new.

**This milestone contains no measurement.** It is an instrument and its guards, and it has
never been pointed at the cartridge. Every number in milestones 173 and 174 came from an
instrument written that same turn and run in the same turn; this one could not be, because
the image is on the other machine. So the finding, if there is one, belongs to the next
session, and the honest summary of this one is: *here is the reading the last three rounds
kept needing and did not have.*

## The fault, stated once

Every scan in this project starts at a map.

`MapLibrary.All()` → `EveryScriptOn` → `Reachable` → follow the calls and gotos. That is the
right shape for almost every question this project asks, and it is silently the wrong shape
for exactly one:

> Is there anything in this file that the maps do not point at?

A map-first scan cannot answer that. Worse, it does not fail when asked — it returns the same
`NOTHING IN THE WORLD SETS IT` that a scan which really looked everywhere would return. The
two are byte-identical, and this thread has now spent three rounds of one session and part of
another inside that gap:

| round | what was believed | what it was |
|---|---|---|
| 173 #2 | the flag is in the blocker's script | it is on the object's own record |
| 173 #8 | nothing sets `0x003E` | the scan never read on-entry scripts |
| 173 #10 | nothing sets `0x003E` | the scan never read the fifth list's own scripts |

Milestone 174's answer was to print what the scan opened, by kind — 2915 scripts, 1584 person,
519 sign, 350 on arrival, 234 on load, 228 trigger. That is a good line and it is still a
statement about the enumeration, not about the file. **A kind nobody has thought of yet has no
line, and no line is exactly what "there are none" looks like.**

## What was built

### `--in-the-image 0xNNNN[,0xNNNN]`

Does not start anywhere. Scans all sixteen megabytes for the three bytes that turn a flag on
and the three that turn it off, and asks of every hit the only question that matters:

**did the map scan ever decode this byte?**

`Opened` answers it properly — not "how many scripts were opened", which cannot be compared
with anything, but *which bytes*, as a flag per byte of the image. So any address at all can be
asked whether it was inside or outside, and "the scan never looked here" stops being a
suspicion and becomes a lookup.

Then it climbs. What names this address, or anything in the hundred and ninety-two bytes above
it; what names that; until it reaches something a map opens — a way in — or reaches a
**literal**, which is four aligned bytes holding the address with no command in front of them.
A literal is not a failure. It is the code boundary with an offset on it: nothing but compiled
code reads one.

Two flags at once is the SAFFRON question. `0x003E` holds eight people in place on `3.10`;
`0x003F` keeps seven off the same map; one scene does both halves and only one half has ever
been visible. Two lists of sites cannot say *one piece of script does both*. Sites within a
hundred and twenty-eight bytes of each other can.

### `--flags`, with the same question put to all 248

The boundary — 322 gating flags, 74 movable, 248 moved by nothing — has always been a sentence
about the scripts the maps reach. Asked of every byte instead, it splits into two jobs that had
been one:

* moved by script somewhere nothing leads to → **an entry point to find**
* moved by no script anywhere in the file → **compiled code, and unreachable by reading
  scripts however many are opened**

And of the first group, which are *jumped into* by something. That is the promotion from
candidate to job, and it needed to exist because —

## The filter is weak, and the control says how weak

"Reads as script" sounds strong. On sixteen megabytes of random bytes the sweep still comes
back with **2658 sites across 2617 flags**, because a `setflag` followed by three or four bytes
that happen to decode and end is not rare at that size. A count printed without that next to it
is a claim, not a measurement.

So the sweep runs a second time on **the same image reversed**. Reversing keeps every byte and
every byte's frequency exactly as it is and destroys every command boundary in it. Whatever
comes back is what this filter finds in a file with these statistics and no scripts at all, and
it is printed beside the real count.

That is the number to read `--flags`' new section against, and it is the reason to distrust any
flag on the list that nothing jumps into.

## The guards

**Twenty breaks, twenty caught.** Every rule in the new class was broken on purpose with
`tools/break-guard.sh`:

`Opened` stopping at the handoff instead of following jumps · marking only opcodes and not
arguments · `Moves` looking only for `setflag` · `ReadsAsAScript` not checking the read
finishes · `OpcodeFor` reading one byte in front instead of two · `loadpointer` counted as a
way in · conditional jumps not counted as one · a literal not required to be aligned ·
`Together` pairing everything with everything · `WhoNames` asking for the exact address only ·
the noise figure ignoring the pattern length · **the control not reversing** · the sweep
forgetting what was opened · reading the flag one byte along · keeping hits that do not read as
script · `PointerIndex` keeping non-addresses · `PastTheBoundary` keeping flags every site of
which was opened · promoting anything that names a site rather than anything that jumps to one.

One break came back green and it was **the break that was wrong, not a missing guard**:
replacing `Array.Reverse` with `Array.Sort(bytes, (a, b) => 0)` looks like a no-op and is not —
introsort is unstable, so an all-equal comparator still shuffles the image, which is a control
by accident. Re-broken as `Array.Reverse(backwards, 0, 0)`, it failed as it should. Worth
writing down: **a break that passes is a claim about the break as well as about the guard.**

And the lesson from 174's third unfailable guard was applied before it could repeat.
`PastTheBoundary` — the rule deciding which flags are news — started as three lines in
`Program.cs`, which has no tests. That is precisely where the set-or-clear split lived in 174,
and where the fifth-list filter lived in 173. It is in the library now, with a fixture holding
a flag moved only where nothing looks, a flag moved only in the open, and a flag moved nowhere,
so that all three answers can be told apart.

## The fixture

One image, built as the shape that hid: two scenes moving the same pair of flags, one reachable
from a map and one reachable from nothing; a third scene nothing opens that a `goto` really
does jump into; a `loadpointer` naming a scene, which is text and not a way in; four aligned
bytes naming it, which is a literal; four unaligned bytes naming it, which is the sixty
thousand coincidences a real image contains; and a hit on the flag pattern sitting in the
middle of somebody else's argument, which is reported and marked rather than swallowed.

## What to run next, and in what order

```
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --flags
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --in-the-image 0x003E,0x003F
```

`--flags` first, because its new closing section says how many of the 248 are worth asking
about at all — and if that number is zero, the whole idea in this milestone is wrong and it
will say so in one line. Then the pair, which either finds one piece of script moving both
flags or prints that nowhere in the file does anything move both, which is equally an answer.

`0x009D` — nineteen people who never arrive, across eleven maps — is the next argument to hand
it, and nothing has ever looked at it.

Expect a few hundred milliseconds each: on sixteen megabytes of random bytes the pointer index
takes 195 ms, the sweep 244 ms.

## Still open, unchanged

Held items; signs never run; `--say-yes` costing party members; the nine `ARRIVED ON AN
ISLAND`s; eleven maps with no way in; shortest-chain ways in; `Bag.PocketCapacity` in shipped
saves; money modelled; `SpecialContracts.ComparedAfter`; co-op step 4; `StoryClosure` as the
no-bag control; `MapScripts` with no coverage at all; milestone docs for `StoryClosure`,
`Autoplayer` and `SpecialContracts`; sound; and whether `Reachable` should honour a trigger's
own condition.

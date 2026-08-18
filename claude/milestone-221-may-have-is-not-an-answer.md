# Milestone 221: "may have" is not an answer

220 gave `SpecialContracts` the barrier and 145 sites moved into *the compare is past something
that **may** have answered instead*. May have is a statement about the reading. Leaving it there
would have been the fifth time in eight milestones that "nothing found" stood in for "did not
look" — so this reads what was actually in the way.

---

## Three answers, and one of them is "not said"

```
  78 site(s) whose compare is only past a barrier, by what was in the way:
      38  the thing in the way CANNOT have answered — the compare is the routine's after all
            38 — a call whose block touches nothing
      30  somebody else answered, and the compare was never this routine's
            26 — another routine
             3 — a command that answers on its own account
             1 — a call whose block leaves an answer
      10  not said — the thing in the way goes somewhere this reading does not follow
             7 — a standard routine, never read here
             3 — a call whose block jumps away
```

**The seventeen routines 220 could not vouch for resolve into ten, five and two.** Ten are
genuinely never branched on — the compare belonged to whatever came after them. Five are behind
a `callstd`, which is in the barrier list precisely because a standard routine answers and this
project has never read one. Two come back: `0x01C` and `0x01D`, the pair 219 read by hand.

`callstd` is why the third verdict exists. Folding those five into "somebody else answered" would
have been a bucket named for a cause with the cause unchecked, and the break for that rule is the
one that matters most in the file.

## And 78 is not 145

220's own headline said *145 sites branch on the answer **only** past a barrier*. It does not.
145 have a compare past one; **78 have nothing else**. The other 67 already have a clean compare
of their own and the barrier only adds extra values to them.

That is a number printed with a word it had not earned, in the milestone whose whole subject was
a reading claiming more than it had checked. Both numbers are printed now, and the rule for which
is which lives in one place that both readings ask.

## `0x01C` is nineteen sites at one address

```
  0x01C   19 site(s),  19 across a barrier at   1 place(s)
      19 of them past a call whose block touches nothing
```

**One byte position.** `0x081BB567` is a department-store script shared by nineteen maps, and
219 called those "nineteen places". They are nineteen reads of one place — 220's own
places-not-times correction landing on 219's numbers three days later. The pair is 38 sites at
**two** addresses.

## What changed

* `WhoTheCompareBelongsTo` — what stood between the routine and the compare, and whether it can
  have answered. Three verdicts, one of which is that the reading does not know.
* `MapLibrary.EveryScript` — **one list of every script the maps hang off anything.** Five copies
  of the same twelve lines were in the repository, in `SpecialCalls` (twice), `SpecialContracts`,
  `SoundCues`, `BattleMusicLocator` and `ItemMentions`, and a sixth was about to be written for
  this milestone. A scan that reads fewer scripts than another comes back with a smaller number
  and nothing says why.
* `SpecialContracts.NothingCleanHere` — which sites this reading is even about, asked rather than
  repeated.

Five breaks, five catches. One went green first: `NothingCleanHere` did not exist yet and the
rule was two lines inside `Derive`, which needs a whole cartridge to run — a rule no test can
reach. Extracted, the break fails one test and nothing else. That is the second milestone running
where a green break meant the rule was in the wrong place rather than the guard being weak.

2894 → 2903 tests, all green. Nothing the run does changed — `--play` reads `SpecialCalls`.

---

## What is still owed

* **The five behind a `callstd`.** `0x0BF`, `0x0DE`, `0x11D`, `0x177`, `0x1A0`, one site each.
  Reading a standard routine is a thing this project has never done and it would settle all five.
* **`0x0188`'s ten.** Three past a command that answers, seven past a standard routine, three
  past a block that jumps away — the routine 215 called the last of the run's ceiling, and ten of
  its branch sites still have no owner.
* **The 411 places.** Every routine sentence in this project quoted sites. `0x01C` at nineteen
  sites and one address is the first of those to be corrected; the rest have not been.
* **The `--play` ceiling list has not been re-read against any of this.** It ranks by branches
  taken by nought, and those branch counts come from `SpecialCalls`, which has always had the
  barrier — but nobody has checked the two tables against each other since 220 made them
  comparable.
* `0x081A77B0`, `0x0153`, and everything owed at 215, 216, 219 and 220 stands.

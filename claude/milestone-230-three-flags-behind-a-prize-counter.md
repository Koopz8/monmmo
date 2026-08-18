# Milestone 230: three flags behind a prize counter, and a table that reads itself

207 re-ran all six rows of the floor table and found five of them stale, and bisected exactly
one: `--play --say-yes`. It left the floor row's 150 → 153 unchased and said so. This chases it,
and then stops the class of fault rather than the instance.

---

## Where the floor row went 150 → 153

Every commit on main's own first-parent chain from where milestone 193's work was merged to
milestone 207 — forty-seven of them — built and run at `--play`:

```
  78884c577  discord: pinned copy                       179 maps, 139 flags
  d2e9a8bd5  merging milestone 181                      183 maps, 151 flags
  5ffc25e22  merging milestone 189                      183 maps, 149 flags   <- down
  d1cf995b2  merging milestone 190                      183 maps, 150 flags
  40b589d13  Milestone 199: three widths behind a counter    183 maps, 153 flags
  ... and nothing else in forty-seven commits moves it
```

**One commit.** Not three like the `--say-yes` row: milestone 199, alone.

And 199 **said so at the time**. Its own commit message ends:

> 3783 -> 3803 blocks read to a proper end, 53 -> 49 stopped, and **+3 flags at every one of the
> six lever settings with reach unmoved.**

Measured here at all six, it is exactly right:

```
                                            198     199     200
  --play                                    150     153     153      <- only 199 reaches the floor
  --play --say-yes                          227     230     231
  --play --say-yes --in-order               229     232     233
  --play --say-yes --boat                   289     292     293
  --play --say-yes --boat --in-order        290     293     294
  --play --say-yes --boat --surf --in-order 288     291     292
```

So the floor row is not the `--say-yes` row's story with a different date on it. **198's +2 and
200's +1 never reach the floor at all** — 200 is the money milestone, and the floor is the run
that is asked for money in one place and gets nothing out of it (201), so the money work leaves
it alone. The one milestone of the three whose change reaches a run with no levers is 199.

**The number was announced in the commit that made it, at all six settings, and the table was
still not updated.** A table maintained by hand does not need anybody to be wrong.

## Which three flags, and which width

`+3 flags` is a number with no list, and a number with no list cannot come back surprising. The
three are consecutive:

```
  0x026E  0x026F  0x0270      the same three at every lever setting
```

`--in-the-image` climbs all three to **`10.14`**, opened by persons 5 through 10 — the GAME
CORNER's prize counter, which is what 199's title means by *behind a counter*.

199 adopted three widths. Removing each in turn from that commit and re-running:

```
  milestone 199 as shipped        153 flags       3852 blocks
  minus [0xB3] = 2                150            3836
  minus [0xB4] = 2                150            3852
  minus [0xC1] = 2                153            3852
```

`0xB3` and `0xB4` are **in series** — the block only reads on past both, so removing either
loses all three flags and neither is worth one apiece. And `0xC1` **opens nothing**: no flag, at
any of the six settings.

That is the width 199 adopted on **two** sites, below this project's stated bar of five, and said
so out loud in the comment rather than leaving it in a commit message. It was right to say so and
right to be uneasy: its blast radius on the run is nought. Trap 9 from the other side again — how
wrong something is and how many places care are different counts, and only the second one is
about the world.

## The six rows today

Re-run at this milestone, all six, against the block at the top of the prompt:

```
  --play                                     183 / 153 in 6, party of 6 at 52, 11 of 103 twice
  --play --say-yes                           243 / 231 in 5, party of 4 at 67, 10 of 155 twice
  --play --say-yes --in-order                243 / 233 in 5, party of 5 at 67,  0 of 152 twice
  --play --say-yes --boat                    381 / 293 in 6, party of 4 at 77, 11 of 204 twice
  --play --say-yes --boat --in-order         381 / 294 in 6, party of 5 at 77,  0 of 200 twice
  --play --say-yes --boat --surf --in-order  381 / 292 in 4, party of 5 at 75,  0 of 200 twice
```

**All six agree.** 207's re-measurement has held for twenty-three milestones. That is the answer
this milestone wanted and it is not the interesting half.

---

## The block that has NEVER been re-run, and it is thirty-nine milestones old

The floor table is not the stale thing in that file. It is the only part of it anybody has ever
re-measured. Running `--flags` and `--scripts` and reading them **against** the block called
*Where the reading stands* rather than past it:

```
  the prompt                                    the instrument, today
  258 are moved by a script somewhere           264
  2915 scripts, reaching 3836 blocks            2915 scripts, reaching 3888 blocks
  3783 read to a proper end, 53 stopped         3856 read to a proper end, 32 stopped
```

Bisected the same way, at every `Milestone N written down` commit from 196 to 229, the flag one
moves like this:

```
  196, 197, 198     258
  199               261      <- the same three flags, again
  200               262
  201, 202          262
  203               264
  204 ... 229       264      <- twenty-six milestones
```

And `3783 / 53` is the **pre-199** reading: 199's own commit message says it moved them to
`3803 / 49`, in the same sentence as the `+3 flags` nobody applied either.

**All of these entered the prompt in one commit**: `f8d4f15fe`, *"The next session's prompt, with
190 folded in"*. Thirty-nine milestones ago. Not one of them has been re-read since.

The two task-list items that rest on those numbers have gone the same way:

* **Item 8** names the remaining stops as `0xB3, 0xCA, 0xC3, 0xC4, 0x43, 0x73, 0xE6` — but `0xB3`
  got a width at **199** and `0x43` got one at **203**, and `0xE6` stops nothing now. The real
  list is nineteen codes stopping thirty-two reads, of which fifteen have something behind them
  at every width: `0xCA (3), 0xC4 (3), 0xC3 (3), 0xA4 (2), 0x36, 0xC6, 0x98, 0xA6, 0x57, 0x61,
  0x7A, 0x59`. Eight of those twelve are not named anywhere in the prompt.
* **Item 9** — "the four that no width reads on from, `0x92`, `0x9B`, `0xD3`, `0x62`" — is
  **two**. `[0x92] = 5` and `[0xD3] = 4` are both in `ScriptReader` today.

So the task list has been sending sessions after two commands that already have widths, and not
mentioning eight that do not.

And the sharpest part of all: **milestone 228 wrote "`--flags` has always read all five kinds and
its 264 was right" in its own document, and the prompt line stayed at 258.** Writing the true
number down in a milestone is not the same act as correcting the block the next session reads
first.

Trap 12 said a table maintained by deltas drifts and stays self-consistent. This is worse and
simpler: **a block nobody ever re-runs does not even need a delta.**

---

## So the table stops being a copy

`--the-floor` runs all six settings in one process and prints the rows **and** the differences
between them, out of one list of six runs:

```
  THE ROWS

    --play                                     183 / 153 in 6, party of 6 at 52, 11 of 103 handed twice
    --play --say-yes                           243 / 231 in 5, party of 4 at 67, 10 of 155 handed twice
    ...

  AND THE DIFFERENCES, SUBTRACTED FROM THE ROWS ABOVE rather than remembered

    --say-yes (MODELLED):   +60 map(s),  +78 flag(s), -1 pass(es), -2 in the party
    --in-order (stricter):   +0 map(s),   +2 flag(s), +0 pass(es), +1 in the party
    --boat (MODELLED):     +138 map(s),  +62 flag(s), +1 pass(es), +0 in the party
    --boat (MODELLED):     +138 map(s),  +61 flag(s), +1 pass(es), +0 in the party
    --in-order (stricter):   +0 map(s),   +1 flag(s), +0 pass(es), +1 in the party
    --surf  (override):      +0 map(s),   -2 flag(s), -2 pass(es), +0 in the party
```

**This is the fix and the other half is why.** Every sentence this project quotes the table for —
`--surf` costs two flags, `--in-order` adds two on the walking thread and one on the boat thread
and a party member — is in the second block, computed by subtracting two rows of the first. They
cannot drift apart, because there is nothing to keep up to date: if the absolutes go stale the
sentences go stale with them, out loud. That property is exactly what the last thirteen
milestones did not have.

A difference is reported only for a pair of rows **exactly one lever apart**, and it names both.
A pair two levers apart also produces a number and that number is not about either lever; a table
maintained by hand is made of those.

Two things it prints that this project has never said: `--say-yes` costs **two party members** and
a pass (the six were four duplicate gifts — already closed, and now visible rather than
remembered), and `--surf` costs **two passes** as well as its two flags.

Six runs share one export and one reading of which scripts are doors, so the whole block is
twelve seconds rather than six commands and six round trips.

## The breaks

Eight, each run against the whole suite of 2963 rather than against its own test, so the greens
are part of the result (207's rule):

| break | what went red |
|---|---|
| `Render` reads the flag column off the first row | `EveryColumnOfARowComesOffThatRow` |
| one lever apart becomes *at most* one | `OneLeverApart…`, `APairTwoLeversApartIsNotADifference` |
| a lever coming off counts as a lever going on | `ALeverComingOffIsNotALeverGoingOn`, +2 others |
| every delta subtracted from the first row handed in | `EachDifferenceIsSubtractedFromTheTwoRowsItNames` |
| a lever that costs nothing is not reported | `ALeverThatCostsNothingIsStillADifference` |
| handed twice read off the whole hand-over list | `ARowIsReadOffTheRun…` |
| the lever swimming anyway reported as READ | `CrossingWaterSaysWhichOfTheThreeItWas` |
| a lever setting drops out of the table | `TheSixSettingsAreSixAndNoneOfThemIsOrphaned` |

Eight breaks, eight catches, **no green ones** — which is itself worth writing down after four
milestones running where a green break meant the rule was in the wrong place. It is not a
coincidence: nothing in `TheFloorTable` needs a cartridge, because the rule was split out from
the sweep before it was written rather than after a break came back green.

Two of the eight caught more than one test. Both are honest — `ADifferenceNamesBothRowsItCameFrom`
and `ALeverThatCostsNothingIsStillADifference` build a pair one lever apart and assert a single
difference, so a reading that reports every pair twice fails them too. They are second guards on
the same rule and not guards on their own, and saying so is the point of running every break
against everything.

2952 → 2963 tests, all green. **Nothing the run does changed** — all six rows are identical
before and after.

---

## What is still owed

* **The rest of the "Where the reading stands" block.** Three of its lines and two of the task
  list's have now been caught stale, all of them thirty-nine milestones after the fact, all by
  reading output against the block. Eight lines were checked; five were right (`--fights` 729 on
  104 with 27 second exits, `--coins` five sites and four pairs summing to 10000 with nought in
  the reversal, `--entries` 227 and 22, `--arrivals` 350/69/28, `--the-scan` 90624 at 24491 and
  11 of 108) and three were wrong. **The other forty lines have not been looked at.** That is
  now the top of the list: it is one run of each instrument and one careful read, and on this
  evidence it will find more.
* `--the-floor` makes six of those lines unable to go stale. Nothing does that for the rest, and
  the same trick would: an instrument that prints a block, rather than a person who maintains
  one.
* **`0xC1`.** Adopted at two sites, opens no flag at any lever setting, and 199's own comment says
  it was the width that *led to* the other two. Whether it should stay is a decision nobody has
  made with this number in front of them.
* **`0x026E`–`0x0270`.** Named and located at `10.14`; what the GAME CORNER does with them is not
  read, and `0x8009` still picks which arm of the coin counter runs from past the code boundary.
* **`0x406F`** and the other 27 unsatisfiable arrival conditions (229).
* **The eleven routines and eleven flags the arrival scripts have to themselves** (227, 228).
* **What `0x63` and `0x65` do** (226 read what they take).
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.

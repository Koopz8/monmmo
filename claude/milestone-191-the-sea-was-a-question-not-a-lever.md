# Milestone 191: the sea was a question, not a lever

`--surf` was the largest modelled number this project had. It is gone.

---

## First, what 190 owed

190 settled where a beaten trainer resumes off a column of eight — every gym leader, kind 1 —
and changed kind 2 on a column of two out of nineteen without anybody reading their bytes. Read
now, by hand, both of them:

**`1.114` person 6, ROCKET WAREHOUSE, trainer `0x0221`** — `trainerbattle` kind 2, 18 bytes,
ending at `0x08163FA5`:

```
0816 3FA5  2B 44 08                 checkflag 0x0844
0816 3FA8  06 01 [08163FB7]         if SET -> 0x08163FB7
0816 3FAE  0F 00 [0817BCA3] 09 06 02   otherwise a line, and end
0816 3FB7  16 04 80 0F 00           setvar 0x8004, 15
           16 05 80 05 00           setvar 0x8005, 5
           25 73 01                 special 0x0173
```

and the fight's own script at `0x08163FCD`:

```
0F 00 [0817BC6C] 09 04     a line
29 DC 02                   setflag 0x02DC
1A 00 80 76 01             0x8000 <- 0x0176
1A 01 80 01 00             0x8001 <- 1
09 00                      callstd 0 — the handover
6C 02                      release, end
```

`0x0176` is the **SAPPHIRE**, and `32.0` ONE ISLAND person 3 is the only place in the image that
takes one away. That is the thread that sets `0x005C` and opens CERULEAN CAVE. Under the old
reading the SAPPHIRE was handed over again on every pass and the `checkflag 0x0844` after the
command was never reached. Under 190's it is handed over once, on the pass the fight is won,
and the guard is live. CERULEAN CAVE is now inside the run's reach.

**`14.2` person 5, SAFFRON CITY, trainer `0x013D`** — same kind, same shape: `checkflag 0x0278`
then one line or the other; the fight's own script is `setvar 0x4081, 1` and nothing else.
Nothing changes hands either way, so this one is a confirmation rather than a repair.

Both have a guard in the fall-through. Kind 2 stands.

---

## The instrument: `--who-knows`

`ObstacleMoves` reads which moves shift something out of the way by asking **the maps**: two
hundred objects across forty-seven maps open by naming a move. That is where CUT, STRENGTH and
ROCK SMASH came from. **The maps are 0.6% of this file.** A scan that only opens maps says
exactly what it would say if the move that crosses water did not exist — trap one, whole.

```
dotnet run -c Release --project src/Tools/RomDump -- firered.gba --who-knows
```

```
600 site(s) read as "who knows this move", 96 read on to a proper end, 7 of those jumped into
the same sweep on this file REVERSED finds 787, 90 reading on, 0 jumped into
```

**600 against 787 is below the floor. 7 against 0 is not.** The raw sweep is noise, exactly as
the whole-file flag sweep is; the jumped-into subset is the finding.

Of the seven, four go on to offer something — a yes-or-no, then a field effect — and the
cartridge says in its own words what for:

```
move  57 SURF        field effect  9  "The water is dyed a deep blue… Would you like to SURF?"
move 291 DIVE        field effect 44  "The sea is deep here. Would you like to use DIVE?"
move  70 STRENGTH    field effect 40  "It's a big boulder… Would you like to use STRENGTH?"
move 249 ROCK SMASH  field effect 37  "This rock appears to be breakable…"
```

The other three — KARATE CHOP, FLAMETHROWER, and a second DIVE — offer nothing. **The offer is
the discrimination**, and it is what separates a scene from three bytes that happen to look
like one.

STRENGTH and ROCK SMASH are opened by the map scan. SURF and DIVE are not: nothing on any map
asks who knows them, which is why the obstacle list never had them.

## And the move id, read twice

`GameRules.SurfMove` was already read — off the move-name table, by matching the string
`"SURF"`. That is the cartridge's own word, but it is a word this project wrote down.

`--who-knows` reads it a second way, off the only block in the image that offers to cross
water, and hardcodes nothing: find the shape, print what was found. **The two readings agree —
move 57.** Where they would disagree is now visible instead of silent.

---

## The change

The walk crosses water **when the party knows that move**. That is the cartridge's own
condition: the block opens by asking who knows it and stops when the answer is nobody.

`--surf` stays, and is what is left when the answer is no: swim anyway, MODELLED, a ceiling.

## What moved

| run | before | after |
|---|---|---|
| `--play` | 183 / 150 | 183 / 150 — *every sea was a wall, and now it says why* |
| `--play --say-yes` | 215 / 195 | **243 / 225** |
| `--play --say-yes --in-order` | 215 / 196 | **243 / 227** |
| `--play --say-yes --boat` | 306 / 223 | **390 / 287** |
| `--play --say-yes --boat --in-order` | 306 / 223 | **390 / 288** |
| `--play --say-yes --boat --surf --in-order` | 390 / 286 | 390 / 286 |

**390 of 425 no longer needs `--surf`.** It is reached by a run that swims because it learned
how, on pass 3, and says so:

```
    crossing water: READ — the party knew move 57 from pass 3, so it swam
```

The floor run says the other thing, and it is a finding rather than a setting:

```
    crossing water: nobody ever knew move 57, so every sea was a wall
```

Two modelled levers are left: `--say-yes` and `--boat`.

And `--surf` now **costs two flags** — 286 against 288 — because a run that swims from pass one
takes several scenes in a different order. It is a ceiling on reach and no longer a ceiling on
anything else. That is trap seven again and it is the fourth time a fix has moved a number the
wrong way in this project.

---

## And a fault this opened, measured and left alone

The boat run's passes now read `264, 269, 302, 390, 381, 381` — and the headline is 390. The
loop's termination test compares **counts**, so a pass that changes membership without changing
any count stops it. Pass 6 added no flags and removed none; the final walk still differs from
pass 6's, by nine maps.

The difference is `moved`. A scene that walks somebody aside is applied as a **displacement from
wherever they already are**, and the fixpoint plays the scene again on every pass:

```
  55 people were walked out of where they stood by a script it ran
    21 of them ended up on a square THAT IS NOT ON THE MAP
      1.6  person 1 at (32,38) on a 34x16 map
      3.2  person 5 at (-29,26) on a 48x40 map
      3.2  person 7 at (46,95) on a 48x40 map
```

`3.2 person 7` is walked **thirty times**. Somebody at `x = -29` is not anywhere, and
*somebody is standing in the way* and *a person removed is a person not in a doorway* are both
computed against these positions.

**Reported and not repaired.** Applying each scene's walk once takes the boat run from 390 to
381 — the honest direction is *down* — but what stops the scene running twice on the cartridge
is a flag nobody has read, and clamping a coordinate turns a wrong position into a plausible
one, which is the harder fault to find. The number is printed instead. It is the next job and
it now has a measurement in front of it.

---

## Guards broken on purpose

Seven more, all caught, `tools/break-guard.sh` each time:

| break | caught by |
|---|---|
| the walk swims without asking the party | `APartyThatDoesNotKnowItFindsTheSeaAWall`, `TheLeverSwimsAnywayAndSaysSo` |
| the pass it learned on is not recorded | `APartyThatLearnsTheMoveCrossesWater` |
| a block that only talks counts as an offer | `ABlockThatOnlyTalksIsNotOfferingAnything` |
| a move id past the cartridge's own table is accepted | `AMoveIdPastTheTableIsNotOne` |
| the field effect is not read | `ABlockThatAsksAndThenOffersIsFound` |
| a square off the map counts as on it | `SomebodyWalkedEveryPassEndsUpOffTheMap` |
| the walk is not cumulative | `SomebodyWalkedEveryPassEndsUpOffTheMap` |

Two of the new fixtures exist to come back empty: the reversed-image sweep, and somebody walked
once who is still on the map. *Nobody was walked* and *nobody was walked off* have to be
different answers.

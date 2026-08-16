# Finishing FireRed

Mason's direction, at milestone 145: **complete FireRed fully before touching a second
cartridge.** This is the list, measured with the tools in this repository rather than
remembered from the last roadmap — which was five revisions old and whose whole ordered list
had since been finished.

Every number came out of `--export-rules`, `--export-world` or `--move-effects` on this image.

---

## What is done

```
425 maps exported, 124 with encounters, 227 encounter tables
412 species, 355 moves, 412 learnsets, 742 trainers, 308 items, 184 evolutions
411 species have a catch rate; 216 moves do damage
20 maps heal a party; 2 maps mind creatures; 186 items lie on the ground across 93 maps
228 squares run a script; 350 things run on arriving somewhere
```

Story, gyms, trading, duels, instancing, the daycare and the egg, the market with items and
a screen, chat, friends, guilds with a screen, tiers, a ladder, cosmetics and a counter,
weather, IVs, EVs, natures, PP, held items, abilities.

**And, at 167, the battle engine's silent half.**

## What is left, in order of what it costs a player

### 1. ~~The battle engine's silent half~~ — DONE at 167

```
at 145:  224 of 354 understood   —   115 groups / 124 moves silent
at 146:  249 of 354 understood   —    93 groups /  99 moves silent
at 164:  283 of 354 understood   —    62 groups silent
at 167:  every group on this list does something
```

**Finished at milestone 167.** What is left of the battle engine is one move — LOW KICK —
which is waiting on a locator rather than on engine machinery, and PURSUIT's ordering, which
needs the switch moved inside the turn.

The list below is kept as written rather than deleted, because the *predictions* in it are
the useful part in hindsight. Five times running, a family named for the machinery it appeared
to need turned out to need something much smaller. "Needs to act out of turn" needed no
ordering at all — priority was already read off the record and already obeyed, and two int
fields covered the whole family.

**Needed nothing new — a line each:**
`0x76` SWAGGER, `0xA6` FLATTER, `0x99` TELEPORT, `0xC5` SECRET POWER, `0xAD` NATURE POWER.

**Needed a formula, and the numbers live in code:**
`0x63` FLAIL/REVERSAL, `0xBE` ERUPTION/WATER SPOUT, `0x58` PSYWAVE, `0x7E` MAGNITUDE,
`0x79` RETURN / `0x7B` FRUSTRATION, `0x87` HIDDEN POWER, `0x9A` BEAT UP, `0x29` DRAGON RAGE,
`0x82` SONICBOOM. **`0xC4` LOW KICK is the one still open** — it needs species weight, which
is on the dex table rather than the base-stat record.

**Needed an end-of-turn hook:** `0x54` LEECH SEED, `0x6B` NIGHTMARE, `0x72` PERISH SONG,
`0xB3` WISH, `0xB5` INGRAIN, `0xBB` YAWN.

**Needed a side-wide, multi-turn state:** `0x23` LIGHT SCREEN, `0x41` REFLECT, `0x70` SPIKES.

**Needed a lock or a counter:** `0x75` ROLLOUT/ICE BALL, `0x77` FURY CUTTER, `0x68` TRIPLE
KICK, `0x9F` UPROAR, `0xA0`–`0xA2` STOCKPILE/SPIT UP/SWALLOW.

**Needed to act out of turn — and did not:** `0x59` COUNTER, `0x90` MIRROR COAT, `0xB9`
REVENGE, `0x9E` FAKE OUT, `0xAA` FOCUS PUNCH, `0x4E` VITAL THROW. The ordering was already
there. What was missing was memory of what had happened this turn.

**Needed a copy of something:** `0x52` MIMIC, `0x39` TRANSFORM, `0x5F` SKETCH, `0x8F` PSYCH
UP, `0x53` METRONOME, `0x09` MIRROR MOVE, `0x61` SLEEP TALK, `0xB4` ASSIST, `0xB2` ROLE PLAY,
`0xBF` SKILL SWAP.

**Needed mutable type or ability:** `0x1E` CONVERSION, `0x5D` CONVERSION 2, `0xD5`
CAMOUFLAGE, `0xCB` WEATHER BALL.

**Needed the rest of the party:** `0x66` HEAL BELL, `0x7F` BATON PASS, `0x80` PURSUIT.

**Genuinely large on their own:** `0x4F` SUBSTITUTE, `0x1A` BIDE, `0x94` FUTURE SIGHT. These
three were the only ones on the whole list whose names matched their cost.

### 2. Nineteen warps lead to maps that are not here

`19 warps and 0 connections lead to maps that are not here.` Either genuinely unused in the
cartridge or something is not being exported, and which has not been established. **One
measurement, not yet taken.**

### 3. Two hundred obstacles, three moves

`200 things in the way across 47 maps: 97 × move 249, 54 × move 70, 49 × move 15.` The moves
exist. Whether a player can *obtain* all three by playing is what the reach measurement
answers, and the last one on record predates several milestones.

### 4. The cartridge font

Four mechanical searches defeated. The client draws with its own. Cosmetic, and the oldest
open question in the project.

### 5. Ten held-item effects and thirty-two abilities

```
66 held-item effects: 56 do something, 10 are carried and silent
76 abilities fielded: 44 do something, 32 are carried and silent
```

Every one of the ten items is about something outside a fight. The abilities need mutable
ability or type, end-of-turn hooks, or turn-order interference — **and the first three of
those now exist**, built for the move families at 165 and 167.

### 6. PURSUIT's ordering

It doubles against somebody leaving, and it should also go before they go. It cannot, because
a switch is resolved by the server *before* the battle is called. Needs the switch moved
inside the turn, which is a change to how a duel is run.

## Not FireRed

The sound and animation work at 166 is not on this list — it is client-side and shared with
every other GBA cartridge — but the readers, the mixer and the animation registry are in place
and the sprite-template registry has a stepped-over count of its own to drive down.

The thousand-player measurement needs a second machine. Four more regions need cartridges this
project does not have. Neither belongs on this list.

---

## Progress

- **145** — `0x8B`, `0x8C`, `0xCC`: three groups whose subject is the creature using the
  move. 224 → **230**. Also found a defect that had been there since the effect kinds were
  split: `EffectKind.Nothing` was missing from the list of kinds settled elsewhere, so all
  twenty-three effect-0 moves fell through to the stage code.

- **146** — eighteen groups. 230 → **249**, and 112 silent groups → 93. Almost none of it was
  new machinery: four moves were silent for want of one line each pointing them at the
  winding-up this engine has done since FLY.

- **165** — six about doing the same thing again: FURY CUTTER, ROLLOUT, TRIPLE KICK, PSYCH
  UP, MUD SPORT, WATER SPORT. Also the milestone where a revert script restored three
  uncommitted files to HEAD and printed exactly the output it was designed to print.

- **166** — sound and animation, which is not this list but shares its discipline. Seven
  guards found that nothing could fail, one of which was hiding every cry on the cartridge.

- **167** — **the rest of it.** Twenty-three groups in five batches. Fourteen of the
  twenty-three needed no new machinery at all.

**The pattern worth carrying forward:** five times now, the largest part of a silent list has
been groups whose machinery already existed and which nobody had connected. Before building
anything for a group, check what it needs against what is already there. A family named for
the machinery it appears to need is usually named wrong — the naming comes from how the moves
feel to a player, and how a move feels and what it costs to implement are unrelated.

**And a second pattern, which is newer:** breaking every new guard on purpose has now found
nine rules that no test could fail, plus one it stopped from being written. Every one looked
fine. The shape is nearly always the same — a rule about telling two cases apart, with only
one case present in the fixture. **A fixture built only from correct data cannot test a rule
whose job is rejection.**

# Sound, and what a move looks like

*Research. No code has been written for any of this yet.*

Two questions, and they turn out to be the same question wearing different clothes:
where does the music come from, and what is the fire you see when something uses
FLAMETHROWER?

Both are on the cartridge. Neither is on the cartridge in the way a base-stat record
is on the cartridge. That difference is the whole of this document.

---

## The rule this has to live under

Nothing changes about the standing rule. The client ships no cartridge data; every
player supplies their own file and extraction happens locally. Audio makes that rule
matter *more*, not less — a soundtrack is the single most recognisable thing a
Nintendo lawyer could point at, and it is also the easiest thing in the world to
accidentally commit as a folder of `.ogg` files.

So: **no audio file derived from a cartridge is ever written to this repository, ever
shipped, and ever sent over the wire.** The server never learns what anything sounds
like. This is entirely a client-side concern, which is a pleasant surprise — it means
none of it touches the part of the project that has to be correct for other people.

It also means the `.gitignore` needs a line before the first byte of this is written,
not after. Extraction writes to the player's own machine and nowhere else.

---

## Part one: the music

### What FireRed actually holds

The GBA's standard sound driver is the one everybody calls **Sappy** and Nintendo
called **MusicPlayer2000 / M4A**. Every Game Freak GBA cartridge uses it, which is
worth noting early: solving this once solves it for Emerald too, and that matters for
the roadmap.

The shape of it is four nested tables, and all four are plain data:

**The song table.** One entry per song, eight bytes each: a four-byte pointer to the
song header, then a track-group number, a zero, the group number again, and another
zero. The doubled group number and the two padding bytes are a strong signature in
their own right — a run of eight-byte records where bytes 5 and 7 are always zero and
bytes 4 and 6 always agree is not a pattern noise produces.

**The song header.** Four bytes and then a run of pointers:

| Offset | Size | What |
|---|---|---|
| 0 | 1 | number of tracks |
| 1 | 1 | unknown (block count) |
| 2 | 1 | priority |
| 3 | 1 | echo feedback; bit 7 is global reverb |
| 4 | 4 | pointer to the instrument bank |
| 8 | 4×n | one pointer per track |

**The instrument bank (voicegroup).** Twelve bytes per instrument, and the first byte
says which of four kinds it is. A sampled instrument carries a MIDI key, panning, a
pointer to its sample, and four bytes of attack/decay/sustain/release. A PSG
instrument carries a duty cycle or a noise period instead of a sample pointer. A
key-split instrument points at a 128-byte table of sub-instruments. A percussion
instrument points at a table of its own. This is a tree, and it is a tree of pointers
into the same ROM, which means it can be walked and verified rather than guessed at.

**The samples.** Sixteen bytes of header and then signed 8-bit PCM:

| Offset | Size | What |
|---|---|---|
| 0 | 3 | zero, zero, zero |
| 3 | 1 | 0x00 unlooped, 0x40 looped |
| 4 | 4 | pitch — 1024 × the sample rate at middle C |
| 8 | 4 | loop start, minus one |
| 12 | 4 | length, minus one |
| 16 | … | signed 8-bit PCM |

Three zero bytes, then a byte that is one of exactly two values, then a pitch that is
one of a known dozen values — that is a locator with almost no false-positive surface.
This is the easiest thing in this entire document to find.

**The sequences.** A byte command language, and the only Sappy data that is not
four-byte aligned. `0x80` waits zero time, `0x81`–`0xB0` are delta-time waits, `0xB1`
ends a track, `0xB2` jumps, `0xB3` calls a subsection, `0xBB` sets tempo as BPM
halved, `0xBD` sets the instrument, `0xBE` volume, `0xBF` panning, `0xCE` note off,
and `0xD0`–`0xFF` are notes with an automatic timeout. Bytes below `0x80` are
arguments, or repeat whatever the last command was — which is a compression trick and
also the one part of the format that cannot be parsed without state.

Everything above is **read**. Every number in those tables comes off the file.

### What has to be modelled

The synthesis. The tables say "play sample 42 at this pitch with this envelope"; they
do not say what the mixer does with that. MP2K resamples with no interpolation at a
fixed rate, applies a linear envelope in fixed steps, and mixes into a small ring
buffer. None of that is data — it is code, and this project does not read code.

That is a bounded and honest model: a resampler, an ADSR envelope, and a mixer. It is
perhaps three hundred lines, all of it testable against itself, and it is the same
three hundred lines for every GBA cartridge this project will ever open.

The PSG channels are a second, smaller model — square waves with duty cycles, a
programmable wave channel, and an LFSR noise channel. Also code, also modelled, also
shared with every other GBA game.

### Finding it without hardcoding it

Two routes, and the project should have both, because a locator with one route is a
locator that fails silently on the day the offset moves.

The first route is the one the format hands you: **find the samples.** Scan for the
sixteen-byte header shape above, confirm the pitch is one of the known values, confirm
the length is sane, and confirm the bytes after it look like audio rather than like
pointers. A cartridge has hundreds of these. Then walk *backwards*: an instrument
entry is twelve bytes with a pointer to a sample you have already found, a voicegroup
is a run of those, and a song header is a thing with a pointer to a voicegroup you
have already found. Every step up the tree is confirmed by the step below it.

The second route is the one every existing tool uses: **find the code.** The
`SelectSong` function has a stable thirty-byte prologue, and the song table's address
is an argument baked into it. This works, and it is worth implementing as a
cross-check, but it sits closer to reading code than this project usually goes — so it
should be the corroborating witness rather than the primary one. If the two routes
disagree, that is a finding, and it gets printed rather than resolved by picking a
favourite.

### Which song goes with which place

This part is already half done and nobody noticed. `MapHeaderRecord` has read a
`ushort Music` off every map header since the map work, at offset 12 of the
twenty-eight byte record — and it has gone precisely nowhere. `RomDump` prints it.
`MapData` does not carry it, `WorldData` does not carry it, and no client has ever
asked.

So the first piece of code here is not audio code at all. It is carrying a number that
is already being read four layers further than it currently travels. That is a small,
well-guarded change with an obvious test, and it can land before a single sample is
decoded.

### What PokeMMO does with it

I could not verify this. PokeMMO's forums, wiki and support site all sit behind bot
protection and returned 403 to every fetch. So this section is what I believe rather
than what I checked, and it is flagged as such:

PokeMMO requires the player to supply their own cartridge files for each region and
extracts assets from them locally, which is the same arrangement this project already
has. Its client is understood to play the original music by driving the sequence data
out of the supplied file rather than by shipping audio, and to expose an add-on system
that lets players substitute their own tracks — which is why "music remaster" mods
exist for it at all. A mod that *replaces* the soundtrack is fairly strong evidence
that the base soundtrack is not a folder of files the client owns; there would be
nothing to replace.

If that reading is right, PokeMMO reached the same conclusion this document reaches
for the same reason, and the interesting part is not the conclusion but that the
add-on system is the thing that makes it obvious from the outside.

Worth taking from it either way: **substitution should be designed in from the start,
not retrofitted.** If a track is looked up by id through one function, a player
pointing that id at their own file costs nothing. If it is looked up in fourteen
places, it costs a rewrite.

---

## Part two: the fire

This half is more interesting, and it has a boundary in it that the music half does
not.

### A move's animation is a script

FireRed has a pointer table indexed by move — the decompilation calls it
`gBattleAnims_Moves` — and each entry points at a script in a byte language of
forty-eight commands. Here is EMBER, entire:

```
loadspritegfx ANIM_TAG_SMALL_EMBER
loopsewithpan SE_M_EMBER, SOUND_PAN_ATTACKER, 5, 2
createsprite gEmberSpriteTemplate, ANIM_TARGET, 2, 20, 0, -16, 24, 20, 1
delay 4
createsprite gEmberSpriteTemplate, ANIM_TARGET, 2, 20, 0, 0, 24, 20, 1
delay 4
createsprite gEmberSpriteTemplate, ANIM_TARGET, 2, 20, 0, 16, 24, 20, 1
delay 16
playsewithpan SE_M_FLAME_WHEEL, SOUND_PAN_TARGET
call EmberFireHit
call EmberFireHit
call EmberFireHit
end
```

That is a program. It loads a graphic, loops a sound panned to the attacker's side,
throws three sprites at three vertical offsets four frames apart, waits, plays a
second sound panned to the target, and calls a subroutine three times.

The command set is forty-eight opcodes and the useful ones are unsurprising:
`loadspritegfx` (0x00), `createsprite` (0x02), `createvisualtask` (0x03), `delay`
(0x04), `waitforvisualfinish` (0x05), `end` (0x08), `playse` (0x09), `call` (0x0E),
`return` (0x0F), `goto` (0x13), `playsewithpan` (0x19), `loopsewithpan` (0x1C),
`invisible` (0x2B), `visible` (0x2C), plus background fades, blending and screen
priority. All of it is data. All of it is readable. The timing — every `delay` and
every argument — is read, which means an animation's *rhythm* is not something this
project has to invent.

### And here is the boundary

`createsprite gEmberSpriteTemplate` is a pointer to a struct that is a pointer to a
**callback function**. The script says which sprite to make and where to put it; what
that sprite then *does* — arcs, spirals, fades, tracks the target, wobbles — is
compiled ARM code.

You cannot read a callback off a cartridge. This project does not read code.

So the honest split is:

**Read.** The pointer table indexed by move. The script bytes for all 354 moves. Every
delay, every coordinate, every repeat count, every sound-effect id, every panning
argument. Which graphic tag each animation loads, and the graphic and palette behind
that tag. Which sprite template each `createsprite` names — as an *identity*, so two
moves using the same template are known to be using the same one.

**Modelled.** What each distinct sprite template does over time. There are on the
order of a few hundred of them across the game, but they collapse hard: an enormous
number of moves are a sprite that travels from attacker to target, or a sprite that
appears on the target and fades, or the screen flashing a colour. A dozen or so
behaviours probably cover most of the move list, and the rest is a long tail that can
be filled in over time — or left as a plain, honest fallback.

**And that fallback is the important design decision.** This engine already has a
concept for "this move has a part nobody has written": `MoveEffects.IsSilent`, and the
`_steppedOver` list that records it rather than announcing it mid-fight. The animation
side should have exactly the same thing from day one. A move whose sprite template is
not modelled yet gets a generic hit animation with the *correct timing and the correct
sounds* — because both of those are read — and gets counted. Then the same measurement
discipline that took the battle engine from 138 silent moves to 56 silent groups works
here too, and "how much of the game is animated" becomes a number rather than an
impression.

That is the whole reason this is worth doing properly rather than quickly. A generic
flash for every move is a day's work and can never improve, because nothing is
measuring it. A script interpreter with a template registry and a stepped-over count
is a fortnight's work and gets better every time somebody spends an evening on it.

### Cries

A cry is a sample like any other, except the ones that are compressed — Gen 3 stores
most cries in a delta-coded scheme rather than as flat PCM, marked in the sample
header. I did not manage to verify the exact block structure; the page documenting it
was among the ones that returned 403. It needs its own look before anything is
written, and it is small enough to sit at the end of the queue rather than the front.

---

## What order to do it in

Nothing here is committed to. This is the order that keeps each step provable.

1. **Carry the map's music id.** No audio at all. `MapHeaderRecord.Music` already
   exists; make it reach `MapData` and `WorldData`, and test that a map's music id
   survives the trip. Small, guarded, and it makes the next four steps possible.
2. **Find the samples.** The sixteen-byte header locator, printing what it found and
   hardcoding nothing. Its output is a count and a list, and a count is a finding
   whether it is large or zero.
3. **Walk up the tree.** Instruments, voicegroups, song headers, song table — each
   confirmed by the pointer below it. Cross-check against the `SelectSong` prologue
   and print it when the two disagree.
4. **The sequence reader.** A parser for the byte language, tested against itself: a
   sequence that parses, reaches `0xB1`, and never runs off the end of the ROM.
5. **The mixer.** Modelled, and named as modelled. Resampler, envelope, PSG channels.
   This is where sound first happens.
6. **The animation script reader.** The pointer table and the forty-eight opcodes.
   Parsed and counted before anything is drawn, exactly as the move-effect table was.
7. **The template registry, with a stepped-over count.** The generic fallback first,
   so every move animates *something* with the right timing and the right sounds from
   the first day, and the number that is not modelled yet is visible.
8. **Cries**, once the compression is actually understood rather than assumed.

Steps 1 through 4 produce no sound and are all testable without a cartridge on this
machine, which is the usual and correct shape for this project.

---

## Sources

Format documentation and decompiled data, all consulted for this note:

- [Summary of GBA Standard Sound Driver (MusicPlayer2000) — VGMDocs](https://loveemu.github.io/vgmdocs/Summary_of_GBA_Standard_Sound_Driver_MusicPlayer2000.html)
- [Sappy engine documentation by Bregalad (via ipatix/m4a2s)](https://github.com/ipatix/m4a2s/blob/master/sappy%20(by%20Bregalad).txt)
- [Sappy Engine Detection — gba_explorer](https://deepwiki.com/attilathedud/gba_explorer/6.1-sappy-engine-detection)
- [GBAMusRiper — Battle of the Bits Lyceum](https://battleofthebits.com/lyceum/View/GBAMusRiper)
- [pret/pokefirered — data/battle_anim_scripts.s](https://github.com/pret/pokefirered/blob/master/data/battle_anim_scripts.s)
- [pret/pokefirered — asm/macros/battle_anim_script.inc](https://github.com/pret/pokefirered/blob/master/asm/macros/battle_anim_script.inc)
- [Gen III Animation Scripting: Tasks and Templates — PokéCommunity](https://www.pokecommunity.com/threads/gen-iii-animation-scripting-tasks-and-templates.465265/)

Not consulted, and it should be said plainly: PokeMMO's
[forums](https://forums.pokemmo.com/), [wiki](https://pokemmo.shoutwiki.com/wiki/Add-ons)
and [support site](https://support.pokemmo.com/knowledgebase/article/installing-the-game)
all returned 403 to automated fetching. The section on what PokeMMO does is reasoning
from the existence of its music mods, not something I read.

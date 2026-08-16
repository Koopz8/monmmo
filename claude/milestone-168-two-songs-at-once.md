# Milestone 168 — Two songs at once, and the phrase that was played once

Four things were on the list. The first was a mechanism that did not exist, and building it
turned up the third one on the way — because a sound effect is a song, and asking why a song
would not play twice led straight to the reason tracks were stopping where the music repeats.

## The one line that was two rules

`SequenceReader` kept one set of places-already-been, shared by `goto` and `call`:

```csharp
if (!seen.Add(target)) return new TrackRead(offset, events, true, unknown);
```

The comment above it said *a track that jumps back to a place it has already been is looping*.
That is right for a jump and wrong for a call, **because a call comes back.**

A phrase played twice is one subsection called twice. It is how this format compresses a
repeated bar, which is to say it is how most music on the cartridge is written. Read with one
shared set, the second call looked exactly like a loop:

```
ended properly : True
events         : 4
  0x01000  B3  Call -> 0x01100
  0x01100  D4  NoteOn
  0x01103  B4  Return
  0x01005  B3  Call -> 0x01100
last command   : Call
```

`EndedProperly: True`, with a call as the last command. The loader kept it, the performer
walked off the end of the event list, and `Finished` went true — **a track ending where the
music repeats.** That is `song 291`, and it is not only `song 291`.

### And the same set read every looping track twice

`seen` held jump *targets*. A track's own beginning is not a jump target, so a `goto` back to
the top was followed once and the whole body read a second time before the check caught it:

```
four notes written; the reader found 8
events 10, ended properly True
NoteOn@2000 NoteOn@2003 NoteOn@2006 NoteOn@2009 Goto@200C
NoteOn@2000 NoteOn@2003 NoteOn@2006 NoteOn@2009 Goto@200C
```

Every note count this project has ever printed for a looping track was double, and the
20,000-command budget was spent at twice the rate — so long tracks were being dropped for
running out of budget they should have had.

Neither of these was visible from the audio. A song missing its second phrase sounds like a
song. A note count nobody knew beforehand cannot be wrong.

### A third, found while fixing them

A `return` with nothing to return to also counted as ending properly. Same wrong answer,
arrived at from the other side: the performer gets a track whose last command does nothing,
steps past it, and reports a track that has run out. Now it is a read that did not end,
because it did not.

## What separated them

Two sets instead of one, and they are not symmetrical:

- **`read`** — every offset a command was read at. A `goto` backwards into it is the loop
  point. This is what the track's own beginning was missing from.
- **`inside`** — the subsections currently being expanded. A call into one of *those* is
  recursion, which genuinely has no bottom. A call into a place merely visited before is a
  repeated phrase and is followed.

`TrackRead` gained `Loops` and `Calls`, and `Track` carries `Loops` into the performer. That
is the number that was missing: **a track that has run out and a track that was written to
repeat were one answer**, so `Ran` could not distinguish a song that had finished from a song
being cut short. `SongPlayer.LoopedAndStopped` is the count whose only correct value is nought,
and the client's `sound:` line now shouts when it is not.

## One-off songs over the music

A faint, a door, a healing machine, a menu beep — every one is a song number in the same table
as the town themes. The jukebox performed one song at a time, so there was no way to sound one
without stopping the music. Cries only ever worked because they are not songs: they go onto the
mixer as a raw recording and bypass the performer entirely.

The obstacle was smaller than it looked and in an unexpected place. `SongPlayer.Render` did two
things: advanced its own clock, and turned the mixer. Exactly right for the only performer
there was, and wrong the moment there are two — they share a mixer, and two performers each
turning it would step every envelope twice per sample and read every recording at twice its
rate. The music would decay faster for as long as an effect sounded, and the effect would play
at the wrong pitch.

So `Advance()` was split out and the mixer belongs to whoever holds the performers. A performer
moves its own clock and puts notes on; the jukebox turns the mixer, once, however many there
are. That is the whole mechanism. The rest is bookkeeping: a finished effect is **let go of**
rather than cut off, so its last note fades at the rate its own instrument says instead of
clicking.

### How many may overlap is read, not decided

The eight-byte song table entry carries a small number written twice — read since the table
walk was built, used for nothing. The driver has several performers rather than one, and that
number is understood to say which. **That reading is modelled**; nothing in the data says what
the number means, only that it is there and that it repeats.

What follows from it is the useful part: two songs naming one performer cannot both be on, so a
second replaces the first. A door opened twice quickly is one noise; holding a direction does
not turn a menu beep into a drone. And how many effects may overlap is therefore not a constant
anybody invented — it is however many performers the cartridge names.

The dump prints the distribution, and says plainly what it would mean if it is wrong: a handful
of values covering many songs is a performer index; nearly one value per song is not, and the
whole arrangement would be built on a misreading.

## Which songs the scripts fire

The widths were already there, and they were derived years of milestones ago without knowing
what any of the commands did — `0x2F` two bytes, `0x30` nothing, `0x31` two, `0x32` nothing,
`0x33` three, `0x34` two. Two of them settle each other: **0x31 takes a word and is followed
immediately by 0x32 at all three of its sites.** A command that names something and a command
that waits for that same something to finish is the shape of a fanfare and its wait.

`SoundCues` walks every script on every map and prints the numbers those commands carry, with
counts and first sites, plus the pairing rate as corroboration. The identification of the
family is modelled; **the numbers are read either way** — if `0x2F` turns out to be something
else, this prints the numbers that something else carries, which is still a fact about the
file.

## The 31 headers, and the six faults behind one word

"The table names a header this walk did not confirm" covered six rejection rules across three
layers. `SongRejection` carries out which one fired, and the dump groups the rejected entries
by it with the bytes at each offset.

The interesting value is `VoicegroupNotConfirmed` — a song naming a voicegroup that *resolves*
but that the walk below did not confirm. That is a fault one layer down, in the recordings or
the instruments, not in the header at all. Song 301 is Oak's laboratory, so whatever this is,
it is not an obscure corner of the file.

## Battle music, and where the line actually falls

A map's music is two bytes at a fixed place in a record. A fight's music is not anywhere like
that: FireRed chooses it in the sound driver's caller, from constants in a switch on what sort
of opponent it is. That is compiled code.

So the split is honest and it is not an even one:

- **Read.** A fight a script sets up can name its own song, because a script is data.
  `BattleMusicLocator` finds every `trainerbattle` a script names a song within four commands
  of, and fills that slot — but only when more than one site agrees, because a run of one is
  how the song table walk used to cut its own first entry off.
- **Read, and new.** Byte two of every trainer record, between the class and the picture, has
  been read past since trainers were first read — the same shape as `MapHeaderRecord.Music`,
  which carried the map's song number for a hundred and sixty milestones before anything asked
  for it. Its low seven bits and its top bit are separated and their distributions printed. It
  is deliberately **not named** here: what the low half selects is not in any table on this
  file, and naming it would be importing a fact from elsewhere and printing it as if found.
- **Modelled, and empty.** An ordinary wild encounter and an ordinary trainer have no script.
  There is nothing to read and there never will be without reading code.

`BattleMusic` has the slot, the client uses it the moment there is a number in it, and every
number carries whether it was read or decided. A decision never replaces a reading; a reading
always replaces a decision.

**What it deliberately does not do is invent the missing ones.** Four plausible integers would
make this look finished and make the count that says otherwise report zero — and a number
nobody is watching is exactly how the sample locator missed every cry on the cartridge. The
gap is counted, the same way silent moves and unmodelled sprite behaviours are counted.

## Twelve guards, and the two nothing could fail

Every new rule was broken on purpose. Ten failed a named test immediately. Two did not:

| Guard | Why nothing was watching |
|---|---|
| an effect ending lets go of its own notes and no others | every fixture instrument had a release of 255 and a looping track — a wrongly released note never faded and came straight back |
| whether a track repeats survives into the performer | every song in the fixture had tracks that ran to an end command |

Both are the same shape as the seven in milestone 166 and the nine in 167, and by now the
shape has a name: **a rule about telling two cases apart, with only one case present.** The
fixture had one call in the whole synthetic cartridge, which is why 2425 tests passed with the
call bug in place. It had no song that loops, so "written to repeat" and "written to stop"
were the same thing. It had no instrument whose release actually falls, so releasing the wrong
note was invisible.

The decoys are a song whose first track is the phrase-called-twice track, and a music note held
on an instrument that genuinely fades. Both guards now fail a named test when removed.

Running total of guards this project has found that nothing could fail: **twenty-eight.** Every
one looked completely fine.

## What is read and what is modelled, said plainly

**Read.** The two rules for following a jump and a call. Whether a track repeats. The group
number on every song table entry. The song numbers every script names. Which song a scripted
fight plays. Byte two of every trainer record.

**Modelled.** That the group number selects a performer — and therefore that two songs sharing
one replace each other. That an effect asked for twice starts again while music asked for twice
carries on. The four-command window between a script naming a song and starting a fight, which
travels with every match so a bad choice is visible in the output rather than baked into it.
Battle music for fights no script sets up, which is empty and counted rather than filled.

**Not shipped, and never will be.** No audio derived from a cartridge is written to this
repository, sent over the wire, or shipped in any form. The server still never learns what
anything sounds like.

## What the next cartridge run answers

Nothing in here has met a real file. Five numbers are worth reading first:

1. **How the tracks stop** — how many jump backwards, how many run to an end, how many do
   neither. Before this, all three were one number.
2. **Songs with some tracks repeating and some stopping.** That mixture is the shape the
   repeated-call bug produced, and how many songs come out of it says how much of the
   soundtrack was being cut short.
3. **How many songs assemble.** It should go up. It may not: calls now expand where they used
   to stop, which spends budget, and the goto fix gives half of it back. Which way it lands is
   a measurement.
4. **The group distribution.** A few values covering many songs, or the performer reading is
   wrong.
5. **Which of the six reasons the 31 headers fail on.** If most are `VoicegroupNotConfirmed`,
   the fault is under the songs rather than in them.

2451 tests.

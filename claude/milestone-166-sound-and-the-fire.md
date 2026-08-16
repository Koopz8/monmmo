# Milestone 166 — Sound, and the fire

All eight steps of the sound and animation plan, in one stretch. What is worth writing down
is not the eight steps. It is that breaking every new guard on purpose found **seven rules
that no test could fail** — and that the last of them was hiding an entire category of the
cartridge's contents.

## What was built

**Step one: the number that said which song.** `MapHeaderRecord.Music` has read two bytes off
every map header since the first milestone that read a header at all, and it went nowhere.
The dump tool printed it; `MapData` did not carry it; the server had never heard of it. So
the first thing written for sound contained no sound — a number, carried four layers further
than it used to go, and a world file version bump. A number and never a name, like every
other id that crosses this split. Nought is a value rather than a gap: the cartridge uses it
for "carry on playing whatever was already playing", which is how walking into a house does
not restart the town's theme.

**Step two: the recordings.** A sixteen-byte header — three zero bytes, a loop flag with two
legal values, a pitch from a known dozen, a loop point, a length. Found by shape, hardcoding
nothing. This is the layer everything else is found *from*.

**Step three: up the tree.** An instrument is twelve bytes containing a pointer to a
recording already confirmed. A voicegroup is a run of confirmed instruments. A song header
points at a confirmed voicegroup. The table points at confirmed song headers. Each layer
proved by the one below it, which is why none of it needs to know where anything is on any
particular cartridge.

**Step four: the sequences.** The only part of the format that is not four-byte aligned and
the only part with control flow in it. A track that ran to an end command and a track that
stopped because the file did are two different answers.

**Step five: the mixer.** Where sound first happens, and the first thing here that is mostly
modelled rather than read. Envelope, resampler, the four shape channels.

**Step six: the animations.** A move's animation is a program in a language of forty-eight
opcodes. Every delay, coordinate, sound id and panning argument is read.

**Step seven: the registry.** Every move animates something from day one, and what is not
modelled yet is counted.

**Step eight: the cries.** Recordings stored as differences rather than as audio.

## The seven

Every milestone here ends by deliberately breaking each new guard and confirming the right
test names it. This time that pass found seven guards that nothing could fail:

| Guard | Why nothing was watching it |
|---|---|
| a sample's loop flag has two legal values | no fixture had an illegal one |
| a song must name a voicegroup that was found | no fixture had a song naming something else |
| a table entry must name a song that was found | only one table-shaped thing existed |
| a table entry's group number is written twice | same |
| the longest run of entries wins | same — nothing to choose between |
| a loop keeps its fraction across the join | every loop test played at an integer rate |
| the high half of a byte is read before the low | both cry fixtures used equal halves |

The pattern in all seven is one thing: **a rule about telling two cases apart, with only one
case present.** Five of them are about choosing between candidates and the fixture contained
a single candidate. The other two are about an ordering or a remainder that the fixture's own
tidiness made invisible — a rate that divides evenly has no fraction to lose, and a byte with
the same value in both halves has no order.

That is a generalisable lesson and it is going in writing: *a fixture built only from correct
data cannot test a rule whose job is rejection.* Every rejection needs a thing to reject, and
that thing has to be wrong in exactly one way and right in all the others — otherwise it is
rejected for the wrong reason and the guard is still unwatched.

Nine decoys and two new fixtures later, all seven fail a named test when removed.

## The one that mattered

The seventh found a bug. The others found tests that were weaker than they looked; this one
found that the sample locator was **silently missing every cry on the cartridge**.

A cry is not stored as audio. It is stored as the difference between one sample and the next,
four bits at a time, in blocks of sixty-four samples packed into thirty-three bytes. And it
carries its marker in the **first** byte of its header rather than the fourth.

The format's own documentation describes a sample header as three zero bytes followed by a
loop flag. This project implemented exactly that — faithfully, from a good source — and in
doing so rejected every packed recording on the file without ever mentioning them. The
locator reported hundreds of recordings and printed a confident count and was short by an
entire category.

Nothing was going to say so. There was no error, no warning, no zero where a number should
be. The report looked exactly as it looks now. That is the same shape as every serious
mistake this project has made: overwriting eight tests and watching the suite go green,
turning a zero into one point of damage that looks like a hit, and a revert script that
restored three files to the wrong version and printed the result it was designed to print.
**Every one of them looked fine.**

The countermeasure has never changed: count something, and check the count against a number
you knew beforehand.

## Where the line falls

Two boundaries were drawn deliberately and are worth having in one place.

**The song table, and the code this project will not read.** The standard way to find a song
table is to scan for the sound driver's `SelectSong` function by its thirty-byte prologue and
read the table's address out of the instruction stream. Every existing tool does it. It works.
It is also reading compiled code, which is the line this project does not cross — so the
corroboration here is a weaker one taken from the data side: a real table's address ought to
appear as a plain pointer somewhere in the file, and how many times it does is counted and
printed. Weaker evidence, honestly labelled, rather than pretending a disassembler is a file
reader.

**The sprite templates, and what a fire actually is.** `createsprite` names a template; a
template is a pointer to a struct pointing at a callback function. The script says which
sprite to make and where to put it — read. What that sprite then does over time, arcs and
spirals and tracking, is compiled ARM code — not read, and modelled instead. So the reader
takes the template as an *identity* and stops. Two moves naming the same one are known to be
naming the same one, which is the property the whole animation plan rests on: a few
behaviours covering many moves is the only thing that makes this tractable.

And that is why the registry counts. A generic flash for every move is a day's work that can
never improve, because nothing is measuring it. The battle engine went from 138 silent moves
to 56 silent groups only because there was a number to watch.

## Two decisions in the counting

A move counts as animated only when **every** template it names is modelled, not any — a move
that draws three things and knows what one of them does looks wrong on the screen, not
two-thirds right. And a move that draws nothing counts as not animated, because calling it a
success would inflate the number for free.

## What is modelled, said plainly

- The envelope's arithmetic. Its four numbers are read; what the driver does with them is code.
- The mixer: voice count, note stealing, clipping rather than scaling, the step rate.
- The four shape channels, which are circuits rather than data.
- The greedy three-argument rule for notes — a genuine ambiguity in the format with no marker
  in the data to resolve it. Where it is wrong it costs notes rather than crashing.
- The wrap-around when a cry's difference leaves the range a signed byte holds. Clamping is
  the obvious choice and the wrong one: it would flatten the loudest part of every cry, which
  is the part a player recognises.
- Every sprite behaviour, all of them.

## What is not portable

The sprite template registry, and only that. Templates are cartridge addresses, so it is a set
of numbers one particular game uses. Everything else in this milestone — the sample format,
the instrument tree, the sequence language, the mixer, the animation opcodes, the cry
compression — is the GBA's standard sound driver and Game Freak's standard tooling, which
means solving it once solves it for Emerald too.

## An open question, unresolved

The format documentation says a sample header's length field is "size minus one". A widely
used ripper reads it as the size. They cannot both be right, and one sample either way is
inaudible and unverifiable without a cartridge on this machine. It is recorded here rather
than decided quietly. Nothing in this milestone depends on it: blocks are counted by division
and a one-sample difference does not change the answer.

## Not shipped, and never will be

No audio derived from a cartridge is written to this repository, sent over the wire, or
shipped in any form. The server never learns what anything sounds like. Extraction happens on
the player's own machine, from the player's own file, exactly as it does for everything else.

2169 tests.

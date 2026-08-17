# Milestone 188 — The second width found wrong, and it was inventing a flag

Delivered as `claude-224.bundle` on the tip of `223`. 2721 tests green from a clean clone at
the base. Measured against the cartridge.

## `0x6F` is four, and it was one

The second argument width in this project found **wrong** rather than missing, and found the way
milestone 187 said the first one would be found: by following the drift rather than the stop.
The `0xC0` site that milestone 187 could not settle sat thirty-seven bytes downstream of this
command, and the read had been out of step ever since it.

Five sites, all after a `setvar 0x8004`:

```
16 04 80 0A 00 | 6F 14 08 3D 00 | 19 00 80 0D 80 | 21 00 80 ...
16 04 80 09 00 | 6F 14 08 3D 00 | 19 00 80 0D 80 | 21 00 80 ...
16 04 80 00 00 | 6F 13 05 39 00 | 19 00 80 0D 80 | 21 00 80 ...
16 04 80 04 00 | 6F 00 00 2B 00 | 19 00 80 0D 80 | 21 00 80 ...
16 04 80 00 00 | 6F 00 00 27 00 | 19 00 80 0D 80 | 21 00 80 ...
```

At **four** the next command is `copyvar 0x8000, 0x800D` and then a `compare` on it — this
cartridge's own idiom for reading an answer back and branching on it — at **five of five**. At
three the next byte is a nop at five of five, which is the padding signature: a width landing on
nothing but padding has landed in the tail of an argument. At one, which is what it was, the
read is out of step from here to the end of the block.

## And it was not hiding a flag. It was making one up

**58 → 53 stopped blocks. 3806 → 3836 blocks reached.** Both the expected direction.

And then the number that was not expected:

```
flags a script somewhere moves     259 -> 258
flags the playthrough itself sets  286 -> 284
```

**Down.** Read one byte short, this command's own arguments decode as a `setflag`, and the run
came back holding flags no script on the cartridge ever sets. A wrong width does not only hide
things — it invents them, and what it invents is exactly the same kind of object as what it
hides, arriving through the same door, counted by the same counter.

Every flag figure this project has published was inflated by a misalignment. That is the
opposite of the failure this session has spent eleven findings chasing, and **from outside it
reads identically**: a number, with no way to tell which side of the truth it fell on.

Two flags is not much. The point is not the size, it is that the sign was wrong and nothing in
the instrument could have said so. A count is not a thing — milestone 135 said that about people
and it is just as true about flags.

## The guard, and what it can and cannot say

`AWrongWidthInventsAFlagOutOfItsOwnArguments`, with a fixture where the command's own arguments
contain `29 5A 00`, so a one-byte read decodes a `setflag 0x005A` that is not there:

```csharp
Put(image, 0x780, SetVar,      0x04, 0x80, 0x00, 0x00);
Put(image, 0x785, WasWrongToo, 0x00, SetFlag, 0x5A, 0x00);
Put(image, 0x78A, CopyVar,     0x00, 0x80, 0x0D, 0x80);
Put(image, 0x78F, End);
```

The assertion is `DoesNotContain` — the first guard in this project that catches a read for
holding something rather than for missing something.

**Two breaks. Four caught, three caught.** Put back to one, the invention guard fails and the
width pin fails. Put to **three**, only the width pin fails — and that is worth writing down
rather than glossing, because it is a limitation with a reason:

> At three the last argument byte is a `0x00`, the reader calls it a nop, and the read rejoins
> at the next command. Three and four behave identically on this fixture and no assertion on it
> can tell them apart. That is not an oversight in the fixture — on the cartridge the same nop
> absorbs the same difference. The only thing separating three from four anywhere is the column,
> five of five.

So the width pin, which restates the table so that changing it has to be deliberate, is doing
the whole job for that half. It is a pin, not a proof, and the proof is the bytes in the table's
comment. Milestone 186 renamed a test for claiming a discrimination it did not make; this one
says so in the fixture instead of waiting to be caught.

## What is next

* **An ordered playthrough** — still the largest piece, and still the thing between this project
  and the first creature a player chooses. `--play` is a fixpoint; a story counter is what a
  fixpoint cannot hold.
* **Which move crosses water, READ rather than assumed.** `--surf` stands in for it and turns
  390 of 425 from a ceiling into a floor.
* **The rest of the 53 stops**, now that two of the list were symptoms of drift rather than
  commands. `0xE6`, `0xC0`'s remaining sites, `0xB3`, `0xCA`, `0xC3`, `0xC4`, `0x43`, `0x73`.
* **The four that no width reads on from** — `0x92`, `0x9B`, `0xD3`, `0x62`. Misreads, so those
  blocks are wrong earlier and finding where is the same job that found this one.
* **The five wall flags** — `0x0013`, `0x0012`, `0x0089`, `0x0053`, `0x0017` — and the ~28
  hand-rolled map walks left in `Program.cs`.

## Where the numbers stand

```
2915 scripts on 425 maps, reaching 3836 blocks
3783 read to a proper end, 53 stopped
322 flags gate something; 258 are moved by a script somewhere; 233 are the code boundary
9 people on or beside a door behind 5 flags — the wall list
21 people never arrive at all
--play --say-yes --boat --surf: 390 of 425 maps, 284 flags, party of 3 at level 75
```

## Still open, unchanged

Held items; signs never run; eleven maps with no way in; shortest-chain ways in;
`Bag.PocketCapacity` in shipped saves; money modelled; `SpecialContracts.ComparedAfter`; co-op
step 4; `StoryClosure` as the no-bag control; `MapScripts` with no coverage at all; milestone
docs for `StoryClosure`, `Autoplayer` and `SpecialContracts`; sound; and
`ServerIntegrationTests.OnePlayerWalkingIsVisibleToAnother`, which is timing-dependent.

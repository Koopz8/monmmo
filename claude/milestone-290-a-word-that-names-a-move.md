# Milestone 290: `0x82`'s word is a move id, and the floor 238 asked for

238 read `0x82`'s seven words, noticed that two of them are CUT's and ROCK SMASH's own move ids,
and wrote the honest thing: **"two of two is not a column, do not build on it."** It said that
without a floor. Here is the floor, and with it, two of two is a column.

---

## Seven words, seven moves

```
    0x82 — 7 byte position(s), the first byte is 1 at every one, the word is 7 distinct values

      0x0816CEAF   58  ICE BEAM
      0x0816CEC3  231  IRON TAIL
      0x0816CED7   85  THUNDERBOLT
      0x0816CEEB  247  SHADOW BALL
      0x0816CEFF   53  FLAMETHROWER
      0x081BDF41   15  CUT
      0x081BE03A  249  ROCK SMASH
```

Seven of seven inside a 355-entry table is worth very little on its own — most operands in this
game name small numbers, and a range that wide catches almost anything.

**The five are one run**: twenty bytes apart, and every one of them hands over to the same block at
`0x0816CF09`, which puts up a yes-or-no and branches on the answer. Five entries naming five
different moves into one shared continuation is the shape of a counter with five things on it.

## Two of two, against thirty-two

The other two sit in obstacle scripts, and an obstacle script says which move it is about — it
opens with `0x7C`, the command that takes a move id and hands back a party slot. So the question
has a floor: **inside those scripts, does anything ELSE name the move the script asked about?**

```
      operand      matches   places
      0x7C arg0         3        3   <- this is the command that ASKS, so it is itself by construction
      0x82 arg1         2        2

      32 operand(s) appear in those scripts and 1 of them names the script's own move at all
```

**One operand position of thirty-two, twice out of twice, for two different moves.** A word that
lands on its own script's move by accident is one in 355 — and the thirty-one other positions in
the same three scripts, holding every text id, coordinate, flag and item those scripts use, do it
nought times between them.

And the third obstacle script — STRENGTH's, at `0x081BE11D` — **has no `0x82` in it at all**. The
command appears in exactly the two scripts whose move it names.

What `0x82` DOES with the move is not read and is not guessed. 67's rule: what an operand takes is
read; what the command does is a different question.

## Three scripts, not two hundred

Worth writing down because it caught me: only **3 of the map scan's 1959 scripts** contain a
`0x7C`. `ObstacleMoves` reports two hundred objects across forty-seven maps, and those two hundred
objects sit on **three script addresses** — 224's sharing, in the place where it decides a
denominator. A version of this that counted per object would have reported the same two matches
two hundred times and called it overwhelming.

## And the reading that agreed with 238 for the wrong reason

The first version of this instrument stepped operand positions in **halfwords**, which is what
every operand sweep in this project does (244). `0x82` is a byte then a word, so its word starts at
byte **one** — and the aligned reading took bytes 0 and 1 together and got `0x0F01`, which is 3841.

It reported **nought matches**. Which is exactly what "two of two is not a column" predicts.

> A wrong reading that confirms the standing guess is the hardest kind to catch, because nothing
> about the output looks wrong.

288 met the same shape from the other side — a dead branch reporting the right answer — and the
cure is the same one: a fixture that makes the two readings disagree. Here it is
`TheWordIsFoundAtByteOneAndNotAtByteNought`, which asserts both that byte one matches and that byte
nought does not, and it is the first break in the table.

**238's own warning was the diagnosis**: *the same width is not the same reading — `0x9D`'s byte
counts and `0x82`'s is 1 at every one of its places, and asking them together is how that gets
missed.* Asking them with the same STRIDE is the other way to miss it.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| operand positions stepped in halfwords — the first version | 4 | **4** |
| a script asking two moves is credited to the first | 1 | **1** |
| the same script counted once per object that uses it | 1 | **1** |
| any move-shaped value counts as a match | 1 | **1** |
| **CONTROL:** the two halves of the word assembled in the other order | **0** | **0** |

Four predictions, four matches.

## What is left

* **What `0x82` does.** Its word is a move id and its byte is 1 at all seven places, which is
  238's "unanswerable, not yes" and stays that way — a byte with one value counts nothing.
* **Where the five are.** They are one run handing over to one block; which map's script list
  reaches that block is a `--script-map` away and was not asked.
* **Why STRENGTH has no `0x82`** when CUT and ROCK SMASH both do. Two of three is a difference
  between scripts that this does not explain.
* **The floor is three scripts wide.** It is the only population the cartridge affords for this
  question, and thirty-two operand positions is not many. The reading rests on the two matches
  being 1-in-355 each, not on the size of the sample.

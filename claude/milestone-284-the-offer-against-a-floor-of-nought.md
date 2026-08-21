# Milestone 284: the offer, against a floor of nought

269 left two things and this prompt has carried both for nine milestones: a nudge for
`AsksWhoKnows`, which takes a bound rather than an id and so could not be given one, and **the
seam** — a rotation joins the end of the file to the beginning and four bytes of every control were
never adjacent, *unmeasured*.

Both are closed. The first one deflates a number this project has quoted since 191 and then
promotes the number underneath it to the best-supported reading in the file.

---

## A bound's nudge is a window

272 gave the flag and variable sweeps a nudge: **ask the same sweep for a number the cartridge does
not use**. `AsksWhoKnows` filters on `1..355`, and every id in that range is a move — there is no
unused id to ask for. What moves is the range.

```
    AND 16 UNMATCHED WINDOWS of the same width above it
      the real 0x0001..0x0163: 600 site(s), 101 reading on
      the windows: 25..587 site(s), median 58; 1..253 reading on, median 5
      0 of 16 find as many sites, and 1 as many reading on
```

Ten-fold on sites and twenty on reading on. **And that reading is wrong**, which is the milestone.

## The high byte is the whole game

The pattern is `7C LL HH`. This file is **10.5% nought** and `0x08` sits in every pointer, so how
often `7C LL HH` turns up by accident depends on `HH` at least as much as on `LL`. A window
somewhere else on the number line is a floor for a *different pattern*.

The only matched floor this cartridge affords is the rest of the bound's own page — every id below
`0x0100` is a move, so `0x01` is the one high byte with both a used part and an unused one:

```
                              ids   sites  per id   read on  per id   opened   OFFERS
    USED   0x0100..0x0163   100     145   1.450        11   0.110        1        2
    UNUSED 0x0164..0x01FF   156      75   0.481         9   0.058        0        0
```

> **3.0x on sites and 1.9x on reading on**, where the unmatched windows said ten and twenty.

The gap between those two answers is the size of the high-byte effect and nothing about moves. And
the window that beats the real range on reading on — `0x06F0..0x0852`, 253 — spans high byte `0x08`,
which is the top byte of every ROM pointer in the file. It is a floor made of pointer tables.

**So `--who-knows`'s 600 and 101 are close to noise**, and the "about 1.0 time by accident" this
command has printed since 191 is wrong by two orders of magnitude — 272's fault, one sweep over.

## And the thing underneath them is not

This project has said since 191 that the OFFER is the shape: a yes-or-no put on the screen and then
a field effect, read straight on from the site. It had never had a floor.

```
    the real range: 7 site(s) that read on AND offer, and 5 that the map scan opens
    EVERY id this cartridge has no move for, all 65180 of them in one sweep:
      14153 site(s), 1157 reading on, and 0 OFFER(S). The real 355 ids give 7.
```

**Fourteen thousand sites naming a number no move has, eleven hundred of them reading on to a proper
end, and not one of them offers anything.** Every one of the sixteen windows: 0. The matched floor:
0.

The raw counts were nearly noise and the offer is nought-against-seven. That is the strongest floor
this project has put under anything, and it went under the claim the whole command rests on.

## The seam is worth nothing, and the first two floors said otherwise

`Rotated` copies `from[(i + shift) % length]`, so the join is at `length - shift`. A band of 4096
bytes either side of it is wider than any block this reader makes, so an artefact of the join has to
be inside it.

**First floor — what would land there if blocks fell anywhere:** one rotation put 4 blocks in its
band against 0.144 expected. Twenty-eight fold. But blocks CLUMP, which this project has known since
205 and quotes in this very command, so the independence expectation is the wrong floor.

**Second floor — every other band of the same width:**

```
    rotation          join     blocks   in the band   bands as full   median   most
    ROTATED 0x400000  0xC00000     2433            0           2048        0     66
    ROTATED 0x800000  0x800000      295            4               6        0     10
    ROTATED 0xC00000  0x400000     2475            1             392        0     82
```

Better, and still not an answer: 4 is a band that only 6 of 2048 manage — the top 0.3%.

**Third, and it is a yes or a no:** read each of those blocks and ask whether it crosses.

```
      0x800422 reads 25 byte(s) and stops on its own side
      0x800838 reads  8 byte(s) and stops on its own side
      0x80083E reads  2 byte(s) and stops on its own side
      0x800F16 reads  2 byte(s) and stops on its own side
      0x400420 reads 27 byte(s) and stops on its own side
```

**0 of 5203 blocks cross a join.** And all five sit just PAST it — they are the file's own opening
kilobyte, which decodes into a handful of very short blocks wherever it is put. The full band is the
head of the cartridge, not the join.

Three floors, three different answers, and only the last one is about the question. **A control's
known defect with a size on it is a control; one without is a caveat**, and this one had been a
caveat for nine milestones.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the matched floor spans two pages instead of one | 2 | **2** |
| `WindowsAbove` returns a narrow last window | 1 | **1** |
| `WindowsAbove` starts on the real range instead of above it | 2 | **2** |
| `Seam` returns the shift rather than `length - shift` | 1 | **1** |
| `CrossesTheSeam` counts a read that ends exactly on the join | 1 | **1** |
| the range overload ignores its lower bound | 1 | **1** |
| **CONTROL:** `Last - First + 1` written `(Last + 1) - First` | **0** | **0** |

Six predictions, six matches.

## What is left

* **`--who-knows`'s prose still leads with 600 and 101.** They are on the page above the floor that
  deflates them, and a reader who stops early gets the old reading. The numbers are right and the
  order is wrong.
* **The matched floor is 100 ids against 156.** It is the only one this cartridge affords and it is
  small; 3.0x on n=145 is a fold, not a proof. The offer's nought-against-seven does not depend on
  it.
* **Why 2 of the 7 offers are in the `0x01` page and 5 are not** is unasked. The page split is an
  accident of where the move table ends.
* **`0x02` is absent** and **the lifts** (265) and **`9.6`'s puzzle** are still open, and now the
  longest-owed thing in the file is 265's, not 269's.

# Milestone 270: "jumped into" was a fact about the region

269 built a floor that keeps the region — the same pointers, in the same file, aimed a few bytes
off — and applied it to one sweep. It left owed the readings whose floor was the reversal AND
which are about an address. This is those, and the first thing it found is that the prompt's list
of them was half wrong.

---

## Which readings are about an address, measured rather than read

The prompt named three: `--in-the-image`'s jumped-into sites, the coin-chain floor and the
field-effect floor. 269 gave the test for whether a reading is address-shaped — a rotation of the
file is a no-op for anything content-relative — so `--the-control` runs it:

```
  6. THE COIN-CHAIN AND FIELD-EFFECT SWEEPS (208, 233) — content-relative, or not?
      control              coin chains   sums   |  dofieldeffect sites   read on   words
      the file itself              5      1   |                11446       757    6408
      BACKWARDS                    0      0   |                11446       834    6397
      ROTATED 0x400000             5      1   |                11446       757    6408
      ROTATED 0x800000             5      1   |                11446       757    6408
      ROTATED 0xC00000             5      1   |                11446       757    6408
```

**Identical at every rotation.** The coin chain walks a fall-through and the field-effect sweep is
a one-byte pattern; neither follows a pointer, so the reversal was and remains their control. Two
of the three owed re-runs were owed to nobody. The one that is address-shaped is the jumped-into
test — on flags (175) and on who-knows sites (191) — and it is asked here with the reading's own
population and the reading's own window.

## Three predicates, because only the control can say which is evidence

"Jumped into" has meant, since 175, that a pointer with a jump opcode in front of it lands within
192 bytes BEFORE the site. That is the reading as it has stood. Two stricter ones are put beside
it: the jump's own target, read from its boundary, reaches the site **as a command**; and the same
with an aligned literal allowed to name the block as well as a jump — code, or a table, and never
four loose bytes, which are the accident being measured.

```
                           WITHIN THE WINDOW          ON A JUMP'S BLOCK          ON A JUMP'S OR A LITERAL'S BLOCK
      control              sites      gated  flags  |  sites      gated  flags  |  sites      gated  flags
      as named             277  7.5 %   10/125   8/60  |     16  0.4 %    1/125   1/60  |    136  3.7 %   22/125  22/60
      BACKWARDS             53  1.3 %  (the one column --flags prints)
      +256 bytes           239  6.5 %    5/125   5/60  |     23  0.6 %    0/125   0/60  |     98  2.7 %    1/125   1/60
      +1024 bytes          260  7.1 %    7/125   6/60  |     18  0.5 %    0/125   0/60  |    104  2.8 %    1/125   1/60
      +4096 bytes          193  5.3 %    4/125   4/60  |     15  0.4 %    0/125   0/60  |     83  2.3 %    2/125   2/60

      within the window, sites           as named  277   floor  193..260   above by 17, INSIDE the floor's own spread of 67
      within the window, boundary flags  as named    8   floor    4..6     above by 2, INSIDE the floor's own spread of 2
      on a jump's block, sites           as named   16   floor   15..23    ON THE FLOOR
      on a jump's block, boundary flags  as named    1   floor    0..0     ABOVE THE FLOOR by 1, against a spread of 0
      or a literal's, sites              as named  136   floor   83..104   ABOVE THE FLOOR by 32, against a spread of 21
      or a literal's, boundary flags     as named   22   floor    1..2     ABOVE THE FLOOR by 20, against a spread of 1
```

**The reading as it stood is on its floor.** 7.5% of unopened flag sites against the reversal's
1.3% looked like the one part of `--flags` clearly above anything, and the prompt has said so for
ninety milestones. Against the same pointers aimed 256 to 4096 bytes past the window it is 5.3% to
7.1% — a margin of 17 sites inside a floor whose own rows spread by 67. **A jump pointer landing
within 192 bytes of a site says the site is in a region full of script. It does not say a script
names the block the site is on.** The reversal under-counted the accidents because reversing
script-land turns its jump pointers into junk, which is exactly the density the window was
measuring.

The strict test, on the 125 boundary sites, finds **one**: `0x0014` at `0x081C0D45`, reached by
`call 0x081C0D40`, against nought at every nudge. One against nought is a name, not a rate, and
`0x0014` is a CUT tree — the class the prompt already files as set by the routine that removes
the object.

## The 22 are the opening, read from the other side

The literal column puts 22 of the 60 boundary flags on a block a literal names, against a floor of
one or two. That is real and it is not news: **21 of the 22 are commands of the new-game script
at `0x081A6481`**, which `NewGameLocator` locates for a different question — where a new game begins —
and which sets 49 flags before the first frame — every one of them in `FlagsAtStart`. The
command says so in its own output (263's rule: the cross-check you did not ask for is the one
worth having), and lists only the one that is not.

## Who knows a move: seven against nought was the right sites for the wrong reason

```
      control              within the window   on a jump's block   on a jump's or a literal's block
      as named                7 of 101           0 of 101              5 of 101
      BACKWARDS               0
      +256 bytes              5 of 101           0 of 101              4 of 101
      +1024 bytes             8 of 101           0 of 101              4 of 101
      +4096 bytes             3 of 101           0 of 101              1 of 101
```

`--who-knows`'s headline — "7 jumped into, 0 in the reversal" — is on the floor by the window test
and **nought** of the seven is a command of the block a jump names. Every one of the seven sits
some tens of bytes after a neighbouring block that a jump names and that ends before it. What
names the blocks they ARE on is a literal in compiled code — `0x06D5A0` for SURF's block at
`0x081A6AC8`, and so on — which is what the climb has printed by hand, per site, since 191
("NOTHING JUMPS HERE. What names it is a literal"). The blocks are real: their text was read.
The mechanism the count credited them with was not. **Trap 57, on the instrument's own headline.**

## What changed in the readings that quote it

`--flags` and `--who-knows` both print the nudged floor beside the reversal now, in the same
paragraph that has quoted the reversal since 175 and 191, and say out loud that "jumped into"
means within 192 bytes. 247's rule: correct every reading of the same shape in the same commit.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| on-a-block is only the window | 3 | **2** — the break kept the literal path, the prediction did not |
| the nudge is applied to the lookup and not to the read | 1 | 1 — needed an assertion added first |
| a literal always counts | 1 | **refused** — the rule was in TWO places (258, again) |
| ... after sharing the loop | 1 | 1 |
| four loose bytes count | 1 | 1 |
| inside-the-window is strict | 1 | 1 |
| the window lookup is not nudged | 1 | 1 |
| groups counted as sites, keyed on site | 1 | **0** — the fixture's sites and groups coincide |
| groups counted as sites, no grouping | 1 | 1 |
| on-a-block is only the window, re-run | 2 | **3** — the listing test caught it too |
| **CONTROL:** the read carries on past the site | **0** | **0** |

The refused break is the one worth keeping. The first draft had the jump-or-literal rule in the
count and again in the listing, one line each, and `break-guard.sh` would not break a line that
occurs twice — which is the fault 258 named and the cheapest way it has ever been caught. One
function answers both now.

## What is left

* **Slack itself.** 192 is a number nothing derived; it was chosen at 175 to catch a site some
  way into a block. A window of nought would ask the strict question directly and the ladder
  would then be a control at every width. Not tried.
* **The one.** `0x0014` at `0x081C0D45` is reached by a `call` at `0x1E2BF7`. What calls that is
  one climb.
* **The far side of the new-game script.** 21 of the boundary's 60 are set before the first
  frame; the list `--flags` prints as "moved by something reading as script that the maps never
  open" could say which of its 60 are those, and then the real count of the unexplained is 39.
* **A nudge for the three-byte sweeps** and **the seam** — still owed from 269, not touched.

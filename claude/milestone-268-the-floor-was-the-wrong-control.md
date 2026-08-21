# Milestone 268: the floor was the wrong control

267 counted **6621 script blocks no map leads to** against a reversed-image floor of 456, called
it twenty-two to one, and said every sweep this project has run over "the scripts" has run over
about a third of them.

**They are not scripts.** The number was right and the sentence around it was wrong, and the
control that let it through is one this project uses everywhere.

---

## The maps' scripts at one end, the reversal at the other

267's calibration row said the outside population was not behaving like the map scan's, and left
two explanations open: scripts using variables only compiled code writes, or bytes that are not
scripts at all. The command mix decides it, and needs nothing from outside the file.

```
    code  the maps' own scripts   outside, named ALONE   outside, IN A TABLE   the reversed image
    0x09        11.4 %                    1.3 %                 0.9 %                0.2 %
    0x0F        10.7 %                    1.2 %                 0.2 %                0.5 %
    0x21         7.8 %                    1.6 %                 1.4 %                0.5 %
    0x5C         3.0 %                    0.1 %                 0.0 %                0.0 %

    every pair, as a distance (0 = the same mix, 1 = nothing shared)
      the maps' own scripts      vs outside, named ALONE       0.690
      the maps' own scripts      vs outside, named IN A TABLE  0.698
      the maps' own scripts      vs the reversed image         0.711
      outside, named ALONE       vs outside, named IN A TABLE  0.240
      outside, named ALONE       vs the reversed image         0.287
      outside, named IN A TABLE  vs the reversed image         0.321
```

**The outside population clusters with the reversal.** It is 0.29 from random bytes and 0.69 from
the cartridge's own scripts, and the three "not the maps' scripts" numbers are within two per cent
of each other.

## A bound, not an impression

Total variation is linear in a mixture: a population that is a share `f` of real script and the
rest junk sits exactly `(1 - f)` of the way from the real thing to the junk. So the share is
arithmetic on two distances already printed — no fitting, no threshold:

```
      outside, named ALONE       at most  3.1 % — about  82 of 2671 block(s)
      outside, named IN A TABLE  at most  1.8 % — about  39 of 2154 block(s)
```

**At most about 121 of those 4825 blocks are script.** And the bound is only as good as its junk
model, which is why the whole distance matrix is printed rather than one column: the two outside
populations really do sit next to the reversal, so the reversal is a fair stand-in for what their
junk looks like.

## Which makes the floor the finding

`--in-the-image`, `--who-knows`, the operand sweep, the word sweep, the flag sweep — this project
puts a reversed-image floor next to almost everything, and the reasoning written down for it is
sound: reversing keeps every byte and every byte's frequency and destroys every command boundary.

**It also keeps every table.** A reversed table of text pointers is still a table of pointers, and
every entry in it is still four bytes with `0x08` on top pointing into the image. What reversal
destroys is the *scripts*; what it leaves untouched is the *structure that produces the
accidents*.

So the floor measures the accident rate of a file with these byte frequencies **and no structure**,
and the accidents in this file come from its structure. Here the gap is about fourteen-fold: 456
predicted, around 6300 actual.

`HowClustered`'s own comment says the same thing about clumping — *"a table reversed is still a
table and still clumps exactly as hard"* — and that sentence has been in this repository since 205
without anybody applying it to the floor itself.

## What 267 keeps

**The run split, which is a real reading either way.** How many consecutive aligned words holding a
ROM address a pointer site sits in tells a literal pool from a pointer table:

```
    in a run of   entries   the maps lead to   they do not
    1 (alone)        4765               2296          2469
    2 to 4           1948                 32          1916
    5 to 16           828                  9            819
    17 to 64          460                  0            460
    more than 64      859                  0            859
```

**2296 of the 2337 entries the maps lead to are named alone** — 98%. Being in a pointer table is
strong evidence of not being a script, and this cartridge's own scripts almost never are in one.

It does not rescue the operand test: the outside-alone entries still score 37% on the calibration
row, because they are junk too. But it is the sharpest single discriminator this milestone found
and it costs one pass.

## And what it loses

**"6621 blocks no map leads to" is withdrawn as a count of scripts.** The blocks are there and
decode; what they are is text, tables and graphics that read as commands by luck. The honest
version is that the maps lead to very nearly all the script this cartridge has, which is the
opposite of what 267 said one milestone ago and is better news for every sweep in this repository.

252's question is not merely unanswered — **it does not have the population it was waiting for.**
There is no large body of scripts outside the maps for a third operand to hide in.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the distance is over the intersection only | 5 | 5 |
| the distance is over counts rather than shares | 3 | 3 |
| the mixture bound is not clamped | 1 | 1 |
| the length is blocks per command | 1 | 1 |
| a block is counted once per command | 1 | 1 |
| the shortest run wins rather than the longest | 1 | 1 |
| **CONTROL:** the blocks are tallied in the other order | **0** | **0** |

## What is left

* **A floor that keeps the file's structure.** Reversing is not the only shuffle available:
  rotating the image by a byte keeps every table and every alignment and destroys every command
  boundary, which is nearer to what these controls claim to be. Nothing here tried it, and every
  "against a reversed floor of N" in this project is waiting on the answer.
* **The 121.** The bound says at most that many outside blocks are real. Which ones is a question
  the mix cannot answer — it is a property of the population and not of a block.
* **`0x09`, `0x0F` and `0x21` are 30% of the maps' scripts between them** and under 4% of anything
  else. A per-block score against that profile would name individual blocks rather than
  populations, and would be the way to find the 121.
* **267's other leftovers are void.** "What the 6621 are" is answered; "whether the outside half's
  variables are one band or many" is a question about noise.

# Milestone 256: three is odd, which is why the run goes round

239 put signs into the walk and the settle test never fired again. 240 named the two flags that
make it go round and explained them like this:

> *It is `0x026C` and `0x0807` — scratch flags set on one map and cleared on another, whose value
> at the end of a pass depends on which map the walk reached last.*

That describes oscillation. It does not cause it, and nothing has said what does since.

---

## One block, three signs, and an odd number

```
    EVERY MOVE OF FLAG 0x026C: 5 set(s), 4 clear(s)
        pass 4  1.59  0x08162212  set 0x026C  (ASign)
        pass 4  1.60  0x08162264  CLEARED 0x026C  (ASign)
        pass 4  1.61  0x081622B1  set 0x026C  (ASign)
        pass 5  1.59  0x08162212  CLEARED 0x026C  (ASign)
        …
```

The same three signs, alternating. All three `call 0x081A7AE2`, and that block is:

```
    0x1A7AFD  2B 6C 02                  checkflag 0x026C
    0x1A7B00  06 01 3B 7B 1A 08         if set, goto 0x081A7B3B
    0x1A7B06  29 6C 02                  setflag 0x026C
      …
    0x1A7B3B  2A 6C 02                  clearflag 0x026C
```

**A toggle.** Read the flag, write the opposite. The walk reads all three signs every pass, three
is odd, and the flag ends every pass the other way round — forever.

That is the cause, and it is a counting argument rather than a story about which map came last.

## The floor table is its own control

```
  --play                                     nothing was moved an ODD number of times on the
                                             pass the run stopped on
  --play --say-yes                           1 flag: 0x026C, 3 moves at 3 addresses on pass 6
  --play --say-yes --in-order                1 flag: 0x026C, pass 6
  --play --say-yes --boat                    1 flag: 0x026C, pass 7
  --play --say-yes --boat --in-order         1 flag: 0x026C, pass 7
  --play --say-yes --boat --surf --in-order  1 flag: 0x026C, pass 5
```

`--play` alone is the one setting that stops on a fixed point, and it is the one setting where
this reports **nothing** — at the floor the run never reaches `1.59`, `1.60` or `1.61` at all, so
`0x026C` is never moved. Five runs cycle, five report one odd flag; one settles, and it reports
none. The correlation is exact and nothing was arranged to make it so.

## 240's second name is wrong

`0x0807` moves both ways — seven sets and seven clears at the widest setting — and it is **not**
why anything goes round:

```
    EVERY MOVE OF FLAG 0x0807: 7 set(s), 7 clear(s)
        pass 1  2.38  0x08165134  set 0x0807  (APerson)
        pass 1  2.38  0x08165134  CLEARED 0x0807  (APerson)
```

**Twice a pass, at one address.** It ends every pass exactly as it began. 240's criterion was
"moves both ways within one pass", which is necessary and is not sufficient — parity is the part
that decides, and a flag that flips and flips back has settled.

## And the first version of this reading was wrong the other way

It asked each flag about the last pass **it** moved in, and reported `0x002E` as a second cause.
`0x002E` is set once on pass one, cleared once on pass two, and never touched again — odd on the
last pass it took part in, and settled from pass three onward, because it stopped.

Asking about the pass the **run** stopped on tells them apart. A flag that is not moving is not
oscillating, and the only way to see that is to ask it about a pass it took no part in.

## The breaks

| break | predicted | went red |
|---|---|---|
| the parity check goes | 2 | 1 — `AFlagMovedEvenlyEndsThePassAsItBegan` |
| each flag is asked about its own last pass | 1 | 1 |
| a flag moved only one way is counted | 1 | 1 |
| the addresses are not distinct | 1 | 1 |

The first was low: replacing the parity with "moved at all" leaves the odd case still reporting
odd, so only the even fixture can notice. Four caught, and the prediction that missed was mine.

3136 → 3141 tests. **The floor table did not move.**

---

## What is still owed

* **What `0x026C` is for.** It holds nothing, gates nothing, and a new game does not set it. The
  block that toggles it asks a yes-or-no first and then branches on `0x8004`, which each of the
  three signs sets to a different number — a shared scene with three doors, and the flag is how it
  remembers something between two halves of itself. Reading the four arms is one `--read-from`.
* **Whether the walk should read a sign twice.** A player reads one sign, not three; the walk
  reads all of them every pass, and that is what makes the count odd. Whether the cycle is a fact
  about the cartridge or about the walk is a real question and this does not answer it.
* **The 192 conditions behind an unreadable copy** (255), 10 distinct; **the 93 that survive**, 67
  distinct.
* **Whose square `0x42` leaves** (254); `0x42 arg2`; the whole-image operand sweep (252).
* `0x405F` (250); the base (248); the eight unused indices and the spare bit (248); collecting the
  buried items (249, a decision); `0x8013` and `0x4025` (251).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x194`'s nineteen doors (236); `0x82`'s seven words (238); the three numbers
  nothing computes (231); `9.6`'s puzzle.

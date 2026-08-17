# Milestone 197: the run cannot stand where the player stands

196's next-task line said the buying report was silent because the run has no money, and that
four shopping-list entries were one purchase away.

Half of that was right. The half that was wrong is the whole finding.

---

## The silence

```csharp
if (money > 0 || played.Bought.Count > 0)
```

The default run passes no `--money`, so this whole section printed **nothing**. Not "it bought
nothing" — nothing at all. A report that says nothing and a run with nothing to say are
indistinguishable from outside, which is the trap written down four times in the brief that
contained this line, sitting in this project's own output since there has been a bag to fill.

Unhidden, it says this:

```
  it was handed NOTHING to spend, which is the default and is why it buys nothing.
  20 shop counter(s) stand on ground it reached; it stood in front of 1 of them
     — bought 0, could not buy 3
    did NOT buy LEMONADE (0x01C) at 3.13: cannot afford it — 350 against 0 left
    did NOT buy SODA POP  (0x01B) at 3.13: cannot afford it — 300 against 0 left
    did NOT buy FRESH WATER (0x01A) at 3.13: cannot afford it — 200 against 0 left
```

Three entries are a money problem, and the prices are READ. **The fourth — `14.1`'s POKé DOLL,
which the shopping list says is sold "on ground it reached" — is not on that list at all.**

## Ground it reached is a MAP

The shopping list has always said *sold at 1 place, 1 of them on ground it reached*. Ground it
reached is the map. Standing beside the person selling it is a second thing, and this project
has known since 184 that a trigger fires only for somebody standing exactly on it and had never
once asked the same question of a counter.

Asked:

```
                                    counters on      stood     never stood
                                  reached ground     at         beside
--play                                    11          0            11
--play --say-yes                          14          0            14
--play --say-yes --in-order               14          0            14
--play --say-yes --boat                   20          1            19
--play --say-yes --boat --in-order        20          1            19
--play --say-yes --boat --surf --in-order 20          1            19
```

**It stands in front of at most one shop counter in the entire game.** None hidden behind a
flag. And the nineteen:

```
  of those 19: 19 are exactly 2 away — ACROSS A COUNTER
               0 stood on no square of that map
               0 are some other distance
```

Eleven of eleven, fourteen of fourteen, nineteen of nineteen. Every lever setting. No
exceptions and no tail. Five of them stand at `(2, 3)` on five different maps, which is the
mart layout this cartridge draws over and over.

A clerk stands **behind** a counter and the player talks **across** it. This walk requires
orthogonal adjacency, so it can never buy anything anywhere, and the one counter it does reach
(`3.13`) is the one seller in the game standing on open floor.

---

## And the proof of it came back the other way

The obvious next reading: if no square beside a clerk can be stood on, then no walk of any
quality reaches them, adjacency is the **wrong** rule rather than a missing one, and the same
fact has been read twice — once off this run, once off the map.

Measured:

```
  and only 0 of them are WALLED IN — the rest have 2 or 3 walkable squares beside them
```

**Zero.** Every clerk has two or three squares beside them that the map's own collision says
can be stood on, and the run stood on none of them.

Walkable is not reachable. Those squares are the clerk's side of the counter and nothing joins
them to the shop floor. So the conclusion survives and the proof of it does not — and the
instrument was written to confirm a claim and killed it instead, which is the only reason it was
worth writing.

Trap 2, exactly: two readings disagreed and **the stricter-looking one was wrong about the
question.** The collision byte follows one edge and answers "could anybody ever stand here"; the
distance follows the walk and answers "did anything reachable get near". Only the second is the
question. The line in the output now says so rather than claiming a proof it does not have.

---

## Guards broken on purpose

| break | caught by |
|---|---|
| the report goes silent when there is nothing to say | `CountersReachedAndCountersStoodAtAreDifferentNumbers` |
| reached-ground and stood-at collapse into one number | `CountersReachedAndCountersStoodAtAreDifferentNumbers` |
| the distance is dropped, so a counter and a continent read alike | `HowFarOffTheMissedCounterWasIsRecorded` |

None came back green. The fixture is **two shops on two maps**, one reachable and one walled
off, because one shop makes "on reached ground" and "stood at" the same number whatever the code
does. The third test is the ordinary case asserted in advance — a run that found no shop at all
is a different nought — so that "report every run as nought" cannot pass.

2776 → 2779, all green.

---

## What this changes

Nothing the run reaches. What changes is that a whole class of finding has been reading as the
wrong class: **the shopping list is not a money list.** Of six entries at
`--play --say-yes --boat --in-order`:

* **3** are money — `10.5`'s drinks, at a counter it reached, priced 200/300/350 against 0.
* **1** is this — `14.1`'s POKé DOLL, sold at `10.3` object 3, two squares from the nearest
  floor the run stood on.
* **2** are the code boundary — `33.1`'s TINYMUSHROOM and BIG MUSHROOM, which nothing on any map
  hands over.

And the same "two squares away" applies to every other counter in the game, so anything else
gated behind a purchase has never been reachable either.

## What is still owed

* **Talking across a counter.** Nineteen of nineteen say the rule is one square too strict. It
  is a change to how the walk decides who can be spoken to, which is the most load-bearing
  thing in the project — it wants its own milestone, its own break, and a measurement of what it
  does to reach at all six lever settings before anybody believes the direction it moves in.
  Down would not be a regression.
* Money stays MODELLED and the payout table is still unlocated. It is no longer the top of this
  thread: the run cannot get to the till.
* `--entries` over the whole image, still unasked.

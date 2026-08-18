# Milestone 250: the bucket that was empty on one list

229 built `--arrivals` to ask a question with a good shape: *a map runs a script when a variable
holds a value — can anything ever make it hold that?* It reported three buckets, and the first
was empty:

```
       0 condition(s),   0 distinct — a variable NOTHING in the scan writes at all
```

247 established that a trigger's condition is the same two halfwords on a different list. Nobody
had pointed this at it.

---

## Nought on one list, forty-three on the other

```
  ON ARRIVAL:  350 condition(s) —  69 distinct — on  58 script(s), 61 map(s), 27 variable(s)
       0 condition(s),   0 distinct — a variable NOTHING in the scan writes at all
     282 condition(s),  28 distinct — a variable something writes, but nobody writes THAT VALUE
      68 condition(s),  41 distinct — a setvar in the scan can satisfy it

  ON A SQUARE: 228 condition(s) — 128 distinct — on 128 script(s), 52 map(s), 42 variable(s)
      43 condition(s),   8 distinct — a variable NOTHING in the scan writes at all
      82 condition(s),  56 distinct — a variable something writes, but nobody writes THAT VALUE
     103 condition(s),  64 distinct — a setvar in the scan can satisfy it
```

229's empty bucket was a fact about the list it was asked of. On the other list it holds
**forty-three**.

(And the two lists are shaped differently in a second way nobody had noticed: 69 arrival
conditions sit on 58 scripts because the tables are shared between maps, while **128 trigger
conditions sit on 128 scripts** — every square has its own.)

## All forty-three are one variable, and nothing anywhere writes it

```
  the 43 square(s) waiting on a variable NO setvar in the scan writes — 1 variable(s):
      of those, 1 want NOUGHT — armed from the start — and 42 want something else and can
      never fire: 1x5 2x4 3x4 4x10 5x7 6x4 7x8
    0x405F — wanted 0/1/2/3/4/5/6/7, 43 square(s) on 2 map(s): 3.42, 28.0  (8 script(s))
      in the WHOLE IMAGE: 0 place(s) write it (2 raw), and the game's own code loads it as an
      aligned literal 0 time(s)
```

**`0x405F` is written by nothing.** Not by a `setvar` in the map scan, not by any place in
sixteen megabytes, and the game's own code does not hold its number as a literal either — so
246's instrument cannot see whatever moves it. It is that milestone's blind spot with an address
on it.

A variable nothing writes holds nought, and zero and absent are the same thing in this game's
variable space. So the forty-three split into **one square armed from the beginning and
forty-two that can never fire at all**. Reporting forty-three as one number would have been a
bucket that is not an operation (236) — the two halves are opposite findings.

The two maps are `3.42` ROUTE 23 and `28.0` ROUTE 22, and the run reaches both. It walks over
forty-two squares it will never trigger, and `--the-floor` says `--in-order` — the lever that
makes the walk respect these conditions at all — costs **+0 maps at every setting**. Nothing is
behind them.

## What reads it

`--who-reads 0x405F` finds three sites that read as script, two of which the map scan opened, and
both of those are the same command:

```
      0x1A7803  0x22  other operand 0x4001  the map scan opened this
      0x1A786C  0x22  other operand 0x4001  the map scan opened this
```

`comparevars 0x405F, 0x4001` — the never-written variable against **the scratch pad 285 scripts
write**, which this project settled at 244 as the one number used as both a flag and a variable.
The bytes after it are a jump and then a switch on `0x4001`'s own value, one arm per square.

That is the shape of a progress gate whose counter lives on the far side of the code boundary:
the script stubs say which square you stood on, and the thing that decides whether it matters is
not in the data. What the routine does with it is not claimed here.

## One reading, not two

`WhenAMapRunsSomething.On` takes a map's header entries and its triggers and puts both through
the same `For`, and every condition carries which list asked. There is no second copy. Five
private copies of "every script on a map" is how 221, 222 and 223 all ran on four fifths of the
cartridge, and a sixth copy of *this* reading would be the same fault in a new place — this
milestone exists because one list got asked a question and the other did not.

It also had to be split out of the sweep before a break could reach it. The first version of the
"both lists are read" test asserted that a record's `with` expression changes a property, which
is the language and not a rule; the break aimed at it did not compile.

## The breaks

| break | predicted | went red |
|---|---|---|
| the value half drops out of the condition | 2 | `AConditionWhoseValueSomebodyWrites…`, `AVariableSomethingWritesIsNot…` |
| the trigger list is marked as an arrival | 1 | `BothListsAreReadAndEachSaysWhichAsked` |
| **the trigger list is not read at all** | **2** | **1 — `BothListsAreReadAndEachSaysWhichAsked`** |
| a record that runs nothing counts on both lists | 1 | `ARecordThatRunsNothingIsNotACondition` |

The third prediction was wrong and the guard was not. `ATriggersConditionGoesThroughTheSameReading`
names `For` rather than `On`, so it cannot notice the trigger loop going away — it guards that the
shared reading gives the right answer, not that the trigger list reaches it. **Predicting the
count tests your model of what each fixture covers as well as the code**, and here the model was
the thing that was wrong.

3101 → 3105 tests. **The floor table did not move.**

---

## What is still owed

* **`0x405F`.** Eight scripts on two routes, forty-two squares that cannot fire, and a counter
  nothing in the data writes. Reading the two `comparevars` blocks all the way through is one
  `--read-from` away; saying what moves the counter is not, and would need compiled code.
* **The 82 trigger conditions waiting on a value nobody writes** (56 distinct) — the middle
  bucket, which is much bigger on the square list than the arrival list in distinct terms and has
  not been looked at.
* **Collecting the buried items** (249) — a change to the run and a decision, and 249 showed it
  moves no reach.
* **The base** (248); the eight unused indices and the spare bit (248).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.

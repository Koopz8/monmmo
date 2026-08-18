# Milestone 251: the operand that was in neither table

`copyvar dest, src` writes its destination. It was in **neither** of this repository's two write
tables:

```csharp
// TwoNamespacesOneNumber
private static readonly (byte Code, int At)[] Writers = [(0x16, 0), (0x17, 0), (0x18, 0), (0x1A, 0)];
private static readonly (byte Code, int At)[] Readers = [(0x21, 0), (0x22, 0), (0x22, 2), (0x19, 2), (0x1A, 2)];

// EverywhereInTheImage
private static readonly byte[] Writers = [0x16, 0x17, 0x18, 0x1A];
```

`0x19 arg2` — the source — is in the reader list, correctly. `0x1A arg0` — the *other* half of the
same copying pair — is in the writer list, correctly. `0x19 arg0` is in neither, in both files, and
the two tables agreed with each other. **A shared wrong list is worse than five private ones**
(224) has a companion: two independent lists that happen to be wrong in the same place cannot
catch each other either.

---

## The guard was guarding the omission

```csharp
/// THE DISCRIMINATION: copyvar names two variables and only the SOURCE is a read.
/// Counting the destination as well makes every write a read …
Assert.Equal(1, both.Variables.GetValueOrDefault(0x4061));
Assert.Equal(0, both.Variables.GetValueOrDefault(0x4060));
```

The reasoning in that comment is right about the **reader** list and was applied to the writer
list as well, and the assertion nails the destination down as named by nothing at all. A break
aimed at the write table went red against it, correctly, on a rule that was wrong.

## What settles it is this instrument's own rule

Not an argument about what a command means — **a variable something looks at is a variable
something writes**, which is the test 244 built to decide exactly this class of question. Point it
at the change:

```
                        before                 after
  0x19 arg2      12 of 14  —  86%      13 of 14  —  93%    the rest: 0x8013
  0x1A arg2       3 of 149 —   2%       3 of 149 —   2%
  0x21 arg0      57 of 60  —  95%      59 of 60  —  98%    the rest: 0x4025
  0x22 arg0       3 of 4   —  75%       4 of 4   — 100%
  0x22 arg2       4 of 4   — 100%       4 of 4   — 100%
```

**Every reading operand rises toward a hundred and the operand that names values stays at two
per cent.** The shortfalls were variables nothing but a `copyvar` ever writes. Across the whole
map scan the shortfall is now **two numbers**, and they are printed by name rather than left as a
percentage: `0x8013`, read once by a copy's source and written nowhere, and `0x4025`, compared
once and written once by a `setvar` at `0x1A6514` that the map scan never opened — the code
boundary, correctly reported.

A percentage nobody can open is a number that can only be believed. Opening this one is how the
missing operand was found, so `--namespaces` now prints the shortfall's members whenever there are
six or fewer.

## What moved

```
  the map scan names 238 number(s) as a VARIABLE      (was 236)
    0x19 arg0: 0x4000+ 5n/13p,  0x8000+ 11n/166p      (was absent)
  106 variable(s) the map scan WRITES                 (was 90)
   7 never looked at by anything: 5 held by code, 2 read past the boundary   (was 5, all 5 held)
  the load denominator: 33 of 106 against a reversed 5                       (was 29 of 90 against 4)
```

**Sixteen more variables are written than this project thought**, and 250's answer survives the
bigger population: *nothing this cartridge writes goes unconsulted* — every one of the 106 is read
by a command, a map header, a trigger, or an instruction.

The whole-image aggregate moved too — 650 written-and-never-read in the save band becomes 757,
against a reversed floor that goes 1070 to 1298. It was below its own floor before and it is below
it now, by about the same ratio, so the instrument's verdict on that number is unchanged: it is
noise, and only the per-variable answers mean anything.

## Two printers were lying about a copy

`--who-writes` reads the second word of a write as the value written, which for a copy is another
variable's id. `VariableSite.Copies` said so for `0x1A` only, so a `copyvar` would have printed
`= 16473` as though the story had put sixteen thousand in something. It now says `= copied`.

The mirror correction: `--who-reads`' caution line asks the command rather than `Copies`, because
244's finding is about `0x1A arg2` specifically — that operand names a value at 145 of its 149
numbers, while a `copyvar`'s source is 93% written and is a real variable read. Widening `Copies`
without narrowing that line would have put the caution on genuine reads.

## Two fixtures had four of the five

`EveryWayANumberGetsIntoAVariableIsFound` asserted **4** distinct write commands, against a
fixture built with four of them. The name promises everything and the fixture supplied the same
short list the code had — fixture-lie 7 in a new shape: *if the rule is a list, the fixture needs
one of everything the list is supposed to hold, and the way to check is to name them.* It asserts
five now, by name, so "five ways" cannot be satisfied by any five commands.

## The breaks

| break | predicted | went red |
|---|---|---|
| copyvar out of the map-scan write table | 2 | `CopyVarsSourceIsAReadAndItsDestinationIsAWrite`, `BothHalvesOfTheCopyingPair…` |
| copyvar out of the operand-name list only | 2 | the same two |
| copyvar out of the whole-image write table | 3 | `TheSurveyCountsEachVariable…`, `EveryWayANumber…`, `TheCopyingOneIsNotReadAsANumber` |
| only copyvarifnotzero's second word is a variable id | 1 | `TheCopyingOneIsNotReadAsANumber` |

Four for four. 3105 → 3106 tests. **The floor table did not move**, which for a change to the
write tables is the answer to check first.

---

## What is still owed

* **`0x8013` and `0x4025`** — the whole remaining shortfall, two numbers, both now named. `0x4025`
  is written by one `setvar` outside the map scan; `0x8013` by nothing at all.
* **The other two `Writers` tables, if there are any.** Two were found by looking at one; nothing
  has grepped for a third list of write opcodes anywhere in the repository.
* **The 82 trigger conditions waiting on a value nobody writes** (250), 56 distinct — and whether
  a variable that is `addvar`'d can reach a value no `setvar` names, which is the reading's own
  stated limitation and has never been measured.
* **`0x405F`** (250); **the base** (248); the eight unused indices and the spare bit (248);
  collecting the buried items (249, a decision).
* `0x4001`'s other two flag sites (244); `10.6 (4,1)` (242); the 17 walls (242); the floor's seven
  flags (241); `0x026C` and `0x0807` (240); `0x194`'s nineteen doors (236); `0x82`'s seven words
  (238); the three numbers nothing computes (231); `0x406F` (229); `9.6`'s puzzle.

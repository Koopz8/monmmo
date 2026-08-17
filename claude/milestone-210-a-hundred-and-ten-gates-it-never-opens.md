# Milestone 210: a hundred and ten gates it never opens

209 gave the money ceiling a list where it had a count. The line directly above it was still a
bare `153 flags`, and 207 had already paid for that: finding which three flags milestone 199
added took hand-patching a print into two worktrees, because there was no way to diff two runs'
flags except by re-running old code.

This is the same fix, one line up, with a denominator on it.

---

## The number

```
--play
    153 flags, 23 field moves, 6 in the party, highest level 52
      of those, 121 gate something in this world file (of 322 that do), so 201 gating flag(s) it never set
      set: 0x002C, 0x0033, 0x0034, 0x0035, 0x0036, 0x0037, 0x003B, ... +139 more
      never set: 0x0002, 0x0003, 0x0004, 0x0005, 0x0006, 0x0011, 0x0012, 0x0013, ... +187 more

--play --say-yes --boat --in-order
    294 flags, 41 field moves, 5 in the party, highest level 77
      of those, 212 gate something in this world file (of 322 that do), so 110 gating flag(s) it never set
```

**Two hundred and ninety-four flags, and a hundred and ten gates still shut.** The count and the
denominator are different sentences: a run that set a hundred and fifty marks on a character and
a run that opened a hundred and fifty doors printed the same line until now.

The wall flags are visible in the "never set" column at both settings — `0x0012`, `0x0013`,
`0x0017` are all there, which is what the code boundary should look like from the runner's side.

`110` is a new headline and it is a **wider** question than the shut-door list: that list names
doors, and most of what a flag holds up in this game is a person.

## Where the rule lives

`FlagGates.HowManyOf` and `FlagGates.NotIn`, in `Core.World`, beside the class that already owns
"does this flag gate anything". Not in the printer — that is the seventh time a line of this
kind has moved out of `Program.cs`, and the reason is always the same: a rule about the world in
a file no test can reach is a rule nothing can fail.

`NotIn` **can come back empty**, and there is a test that makes it. "110 gating flags it never
set" is a number with only one direction otherwise, and a run that had opened every gate would
print a sentence this project has never printed.

Three breaks, three catches:

| break | caught by |
|---|---|
| the count is of flags handed in, not of gates | the fixture's one flag that gates nothing |
| what is missing is every gate, set or not | both halves of the missing list |
| what is missing is only the gates it DID set | the same two |

The fixture has **two kinds of gate** in it — a hidden person and the boat — because a world
whose only gates are people cannot tell "it asked what gates something" from "it counted the
people", and this cartridge has both.

2831 → 2835 tests, all green. Nothing the run does changed.

---

## What is still owed

* The set list is truncated at fourteen and `+N more`. Diffing two runs still means reading two
  truncated lists; whether that wants a file in `out/` is a decision and it is not made.
* `HowManyOf` counts against `FlagGates`, which knows about people and the boat and nothing
  else. Anything this project has not extracted yet reads as gating nothing, so `322` is a
  floor on the denominator rather than the denominator.
* 201 gating flags unset at the floor and 110 at the widest lever setting — nobody has looked at
  what the difference is made of.

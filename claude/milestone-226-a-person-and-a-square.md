# Milestone 226: a person and a square

`0x63` and `0x65` have had widths since 187 and no meanings. 187 said so in the table: *"Not
named here: what it does is a guess, what it takes is not."* 225 found the fan club's on-load
chain calling both once per fan, on the arm a run's silence takes — so what they take is on the
path of something the playthrough decides, and it is time to measure it.

---

## What they take

```
  0x63 — 126 read(s) at 67 place(s) on 26 map(s)
       126 of them name a person who is really on that map
        26 would if the SECOND word were read as the person   <- the control
       123 have their other two words inside that map's bounds
      how far those words are from where the cartridge put that person:
           26 exactly there, 51 within three squares, 49 further
      0.45 exactly-there would be expected by chance                <- the floor
```

**Twenty-six exact hits against a floor of nought point four five.** Fifty-eight times chance.
The two words after the person are a square in the same coordinate system that person's own
placement is written in, and a quarter of the sites restate exactly where the cartridge already
put them.

```
  0x65 — 105 read(s) at 43 place(s) on 21 map(s)
       105 of them name a person who is really on that map
        26 would if the SECOND word were read as the person   <- the control
      the byte after the person: 9 at 39 sites, 8 at 38, 7 at 20, 10 at 4, 1 at 4
      54 are the named person's own movement type; 22.7 would be somebody else's   <- the floor
```

The byte is drawn from the same small set the map data uses for movement types, and it is the
**named** person's own at 54 of 105 against a floor of 22.7. Two and a half times, which is real
and much weaker than the other — and honestly so: about half the sites set a movement the person
does not already have, which is what a command that changes one looks like.

## The floors are the milestone

"A hundred and twenty-six of a hundred and twenty-six name a person who is really on that map"
sounds conclusive and is worth nothing on its own. A map with fifteen people accepts any small
number, and every argument here is a small number. Read the **second** word as the person id
instead and 26 sites still agree.

The same for the squares: 26 exact hits reads as a coincidence until the chance of one is worked
out per site — *per site*, because the maps are different sizes and a hit on a sixty-by-sixty map
is nine times less likely than on a twenty-by-twenty.

Five breaks, five catches: the floor made a constant instead of the map's own size, a site naming
nobody counted as a throw, the movement floor counting the named person as one of the others, the
distance measured on one axis, and the control reading the same word it is meant to be a control
for.

## And they are still not named

What each takes is READ now, with a floor beside it. What each **does** is still a guess, and
this milestone does not make one. A command that takes a person and a square could put them
there, remember where they were, or check something about it; the arguments do not say which, and
nothing in this project has run the game.

That distinction is what separates this from 187, which had the same instinct, wrote the guess
down as a guess, and left it there for thirty-nine milestones.

2934 → 2941 tests, all green. Nothing the run does changed.

---

## What is still owed

* **`special 0x00A7`**, which opens the fan club's on-load chain and is unread.
* **What the two commands DO**, which needs something this project does not have: a reading of
  the game's own code, or a run against an emulator, or a second cartridge to diff against.
* **The 97 command codes whose reads and places differ** (`--the-scan` says which). Only the
  routine tables have been corrected.
* The standard-routine table (222), `callstd 0x05`'s 251 unwalked sites, `0x0188`'s last three,
  `0x081A77B0`, `0x0153`, and everything owed at 215 onwards.

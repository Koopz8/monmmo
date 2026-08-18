# Milestone 243: one number, two namespaces

> **CORRECTED AT 244: `27` is `1`.** Twenty-six of them are `0x1A`'s second word, which is
> a plain VALUE unless it happens to be a variable id — a literal 5 handed to a routine
> counted as a look at variable 5, and 5 is also a real flag. The one that survives is
> `0x4001`, the number that raised the question. The floor of 1.71 has not moved, so the
> corrected finding is AT the floor: this game does not reuse numbers across the two
> namespaces. Everything below about the method stands; the headline does not.

240 printed this about a flag three scripts had just cleared:

```
  nothing the run executed touched 0x003F at all — which is a different finding from it
  holding the wrong number, and the two have looked identical until now
```

Both halves of that sentence are true and it is about **the variable `0x003F`**. `--trace` watches
a variable; the flag of the same number is a different thing. The command line takes a bare
number and cannot tell them apart, so an instrument answered honestly about the wrong question and
nothing could have said so.

`0x4001` raised the same thing from the other side: the run moves a flag numbered `0x4001`, and
237 read `0x4001` as the variable 63 doors announce themselves in. **Both are real.** The
cartridge holds `29 01 40` — `setflag 0x4001` — at `0x1656AA`, and `2A 01 40` at `0x169270`.

---

## How much room there is to be wrong

```
  the map scan reads 24429 command(s) and names 238 number(s) as a FLAG and 236 as a VARIABLE
  27 number(s) are named BOTH ways, against a floor of 1.71

    0x4001    4 as a flag, 326 as a variable
    0x0001    4 as a flag, 230 as a variable
    0x0002   23 as a flag,   6 as a variable
    0x0003   12 as a flag,   3 as a variable
    0x0005   13 as a flag,   2 as a variable
    0x0004    9 as a flag,   5 as a variable
    0x0044    1 as a flag,   7 as a variable
    0x003F    1 as a flag,   3 as a variable
    ...
```

**Sixteen times the floor.** The floor is what two sets of that size would overlap by if they
landed independently in the span they occupy — and the span is narrower than the whole
sixteen-bit space, which makes a chance collision *more* likely, so 27 clears a harder floor than
a fair one.

And the number that started it is in the list. `0x003F` is named once as a flag and three times as
a variable, which is exactly why 240's line was true and useless.

## Asked of the map scan, never of the image

```
  and the same question asked of the WHOLE IMAGE, which is the noise:
    2117 as a flag, 12659 as a variable, 1182 both
```

Twelve thousand six hundred and fifty-nine variables in a game that has a few hundred. Sixteen
megabytes of graphics hold every three-byte pattern many times over, and 233 threw a raw sweep
away for the same reason. It is printed beside the real answer rather than deleted, because the
size of it is the argument for asking the other way.

## The two instruments now name each other

`--moved N` is new and is the flag half of `--trace`: every set and clear of a flag during a run,
with the script, the map, the pass and which of the map's four lists ran it. Both commands now say
when the number they were handed is used in the other namespace as well:

```
  EVERY LOOK AT AND CHANGE TO THE VARIABLE 0x003F, IN ORDER
    AND THE RUN MOVED THE FLAG 0x003F 1 time(s) — a different namespace and a different
      question. `--moved` is that one.
    nothing the run executed touched the VARIABLE 0x003F at all
```

That is the smallest fix that makes the mistake impossible to repeat without seeing it. Renaming
the numbers is not available — the cartridge shares them.

## A fixture caught the instrument before it shipped

The first version of the sweep called `ScriptReader.ReadAll` once per block. `ReadAll` walks
everything reachable from where it is pointed and keeps its own seen-set, so pointing it at every
block in turn read the target of every `goto` again for every block that jumps there. The
whole-cartridge answer looked entirely plausible — **264 flags, 238 variables, 27 shared** — and
the per-block dedup this instrument is careful about was doing nothing at all.

`ABlockReachedTwiceIsReadOnce` failed: two blocks jumping to a third counted its one `setflag`
three times. The real numbers are 238 / 236 / 27, and the command count fell from 78972 to
**24429** — which is the map scan's own 24491 byte positions, arrived at independently.

**The fixture was written to guard a rule and caught a live fault in the instrument on its first
run.** That is what they are for and it is the first time in this session one has done it before
delivery rather than after.

## The breaks

Six, six catches:

| break | what went red |
|---|---|
| `ReadAll` instead of the single-block read | `ABlockReachedTwiceIsReadOnce` |
| the per-block dedup removed | that one |
| `copyvar`'s DESTINATION counted as a read | `CopyVarNamesBothItsOperandsAndTheyAreCountedOnce` |
| `comparevars`' second operand not counted | `CompareVarsLooksAtBothOfItsOperands` |
| the floor taken over all 65536 rather than the span | `TheFloorIsHigherWhenTheNumbersAreCrowded` |
| the command count is not a count of commands | `ItSaysHowManyCommandsItRead` |

The first two are the same fault by two routes, which is the point: a dedup that is bypassed and a
dedup that is deleted are indistinguishable from the output.

3049 → 3057 tests, all green. **The six rows of the floor table did not move.**

---

## What is still owed

* **The other 26.** `0x0002` is 23 flag sites and 6 variable ones and it GATES eight objects —
  the largest genuine collision, and nothing has read either side of it.
* **Whether any of the 27 is a MISREAD** rather than a real double use. `setflag 0x4001` is odd
  enough to be worth one `--read-from` on each of its four sites; this milestone read two.
* **`10.6 (4,1)`**, the one sign nothing can stand beside (242); the 17 walls (242); why the
  floor's seven flags are what they are (241).
* **`0x026C` and `0x0807`** (240) — and note that at the floor `0x026C` is moved by nothing at
  all, so the oscillator story is about the levered rows only.
* **`0x194`'s nineteen doors** (236), **`0x82`'s seven words** (238), the three numbers nothing
  computes (231), `0x406F` (229), and everything owed at 215 onwards.

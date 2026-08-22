# Milestone 293: the blind spot was real and hid nothing

292 found that this project reads one argument slot and the cartridge uses six, and that **eleven
routines are handed a value only outside the slot every sweep reads**. This opens them.

The answer is the deflating one, and it is worth having.

---

## The eleven, and not one of them is branched on

```
      routine   slot     values  calls   what the answer is compared against
      0x0020    0x8005        4      6   nothing branches on it
      0x0021    0x8005        2      2   nothing branches on it
      0x0138    0x8005        2      2   nothing branches on it
      0x0138    0x8006        1      2   nothing branches on it
      0x018E    0x8005        2      2   nothing branches on it
      0x01A7    0x8005        2     20   nothing branches on it
      0x01A8    0x8005        2     20   nothing branches on it
      0x00FC    0x8008        1      1   nothing branches on it
      0x018C    0x8006        1      2   nothing branches on it
      0x0192    0x8005        1      1   nothing branches on it
      0x01B2    0x8006        1      2   nothing branches on it
      0x01B6    0x8005        1      1   nothing branches on it
```

**Eleven of eleven: nothing compares their answers at all.** They are called for what they do, and
the value they are handed is a parameter to the doing rather than a question. `0x0020` is the widest
of them at four values; `0x01A7` and `0x01A8` are a pair, each two values over twenty calls;
`0x0138` is handed values in **two** slots at once.

## And the whole population, asked properly

291's answer was "one routine of twenty-two" — asked in `0x8004` alone, which 292 then showed was
one slot of six. So the answer had to be re-asked of every routine, in every slot each is actually
handed a value in:

> **1 routine has the answer compared against different things depending on the value: `0x194`.**

The same one. **The blind spot was real and it hid nothing** — which is a finding rather than a
disappointment: it means 291's reading survives the correction to the instrument it rested on, and
this project now knows that rather than assuming it.

## The middle break was green because the fixture was wrong

Three breaks and a control on `Selectors`:

| break | killed |
|---|---|
| the whole-population question reads `0x8004` rather than each routine's own slot | **2** |
| only the FIRST slot of a routine handed values in two is asked | **0**, then **1** |
| "handed more than one value" counts instead of "compared differently" | **1** |
| **CONTROL:** the returned list ordered the other way | **0** |

The second one came back green, and the guard was not the problem. My fixture for "a routine handed
values in two slots is asked in both" gave the **discriminating** slot the most values — and
`SlotsOf` lists slots by how many values they carry, so the slot that mattered sorted first and a
version reading only the first slot passed it.

Rewritten so `0x8005` carries three values and finds nothing while `0x8006` carries two and picks
the question, it kills. **A fixture whose subject is "both" has to make the second one the one that
matters** — 289's fixture that asserted the opposite of its own name, and 292's ordering-control
that was not a control, are the same mistake in three different costumes: *the test passed, so I did
not look at what it tested.*

The control here is a control on purpose: no fixture has more than one selector in it, so the order
of the returned list genuinely cannot matter. That is what 292 said a control has to be.

## What is left

* **`9.6`'s puzzle** is where 292 left it: three routines, one called nowhere else in the game, none
  of them handed a value in any argument slot.
* **What the eleven DO** is behind the boundary as ever. What they take is now read.
* **`0x0138`'s two slots** are the only routine on this cartridge handed values in two at once, and
  nothing here asks what the pair means.
* **The window is still four commands** (`SpecialCalls.Before`), so an argument written five
  commands before a call is invisible to every number in 292 and 293.

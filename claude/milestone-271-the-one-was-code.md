# Milestone 271: the one was code

270 left three things: the window's width, the one boundary site a jump names, and which of the
boundary's sixty are the opening. All three are closed, and the middle one closed the wrong way.

---

## The one

270's strict test found exactly one boundary site on a block a jump names — `0x0014` at
`0x081C0D45`, reached by `call 0x081C0D40` from `0x1E2BF7` — against nought at every nudge, and
called it a name rather than a rate. The climb `--in-the-image 0x0014` had already said what the
next step was: **NOTHING IN THE FILE NAMES `0x081E2BF7` or the 192 bytes above it.**

The bytes there:

```
1e2bf0  01 68  88 88  7f 24  04 40  0d 1c  08 35  00 2c  03 d1
        ldr r1,[r0]  ldrh r0,[r1,#4]  mov r4,#0x7f  AND R4,R0  ADD R5,R1,#0  add r5,#8  cmp r4,#0  bne
```

`04 40 0d 1c 08` is `and r4, r0 ; add r5, r1, #0 ; add r5, #8` — THUMB, every halfword a
well-formed instruction in a sequence that makes sense — and read as script it is `call
0x081C0D40`. The block it names opens `setvar 0x0300, 0x2A1F`, a variable in no band this
project has read (flags `0x0000+`/`0x4000+`, variables `0x4000+`/`0x8000+`, measured at 264).

**One against a floor of nought is not a name.** 270 wrote that sentence the other way round, and
the correction is that a floor of nought at n=1 has no power to say otherwise — the accident rate
of the strict test on the 125 boundary sites is under one in 125 at every nudge, which is exactly
where one accident would sit. The window shut (below) says the same.

## The window shut

`--the-control` now runs the ladder with the window at nought — a jump aimed exactly at the site:

```
      control              sites   gated  flags
      as named               4     0/125   0/60
      +8 bytes               1     0/125   0/60
      +256 bytes             2     0/125   0/60
      +4096 bytes            1     0/125   0/60
```

Four of 3674 unopened sites have a jump aimed exactly at them, against one to four nudged; none
on the boundary at any width. 175's 192 was chosen to catch a site some way into a block, and at
nought the test is a control at every width rather than only past 192. It finds nothing on the
boundary either way.

## The sixty, sorted by what names the script

`--flags` has said since 175 that sixty boundary flags are "moved by something reading as script
that the maps never open", and offered "jumped into" as the promotion. `WhatTheBoundaryIs` sorts
them by the strongest thing naming the script that moves each, and the buckets are in order of
strength so a flag goes in the first it satisfies:

```
      the 60, by the strongest thing that names the script moving each (271):
         21  a command of the NEW-GAME script at 0x081A6481 — set before the first frame, in FlagsAtStart
          1  a command of a block a JUMP names, read from the jump's target
             0x0014 at 0x1C0D45 set  <- 0x1E2BF7  call 0x081C0D40
          0  a command of a block an aligned LITERAL names
         38  reads as a script and NOTHING in the file names the block it is on
```

Twenty-one are the opening, read from the other side — the cross-check 270 found, now in the
reading that needed it. One is the accident above. **Thirty-eight read as a script and nothing in
sixteen megabytes names the block they are on**, which 269 showed is what an accident of the
"reads as a script" filter looks like: a pointer aimed four bytes into a real script decodes two
thirds of the time, and these are not aimed at by anything at all.

So the boundary's honest split is: 173 moved by no script anywhere, 38 moved by bytes that read
as script and nothing names, 21 set by the opening, 1 that is code. The sixty-flag bucket was
never an entry point to find; it was the opening plus noise, and the prompt's "8 of those are
jumped into — an entry point to find" is withdrawn along with the rest of 270's window.

## The breaks, with the count predicted first

| break | predicted | killed |
|---|---|---|
| the opening is not consulted | 1 | 1 |
| a literal counts as a jump | 1 | 1 |
| the weakest site wins instead of the strongest | 1 | 1 |
| the literal bucket is the window | 2 | **1** — the first fixture has no window-not-block case; the third does |
| **CONTROL:** the early-out after the opening | **0** | **0** |

## What is left

* **The 38.** Each is a `setflag` that reads on to an end and that nothing names. 269's
  resynchronisation rate says most are accidents; a per-site test would be whether the block
  decodes from ITS start — the command boundary before the site — with a command mix like the
  maps' own (268's axis). Not built.
* ~~`0x0300`~~ — run: `--who-writes 0x0300` finds 764 sites in the file, 249 reading as script,
  **0 opened by the map scan**, written to twenty-odd different values. A three-byte pattern the
  file makes 764 times by accident is the accident.
* 269's **three-byte nudge** and **the seam** — still owed.

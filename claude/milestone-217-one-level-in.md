# Milestone 217: one level in

216 ended on the one thing this project has never built: **nothing here follows a `call` to
attribute an answer.** 214 added the barrier that stops the scan doing it wrongly and lost 42 of
1097 attributions doing so — the right way to be wrong, and a debt.

`--through-a-call` is the payment. It got the reading wrong twice on the way, both times in the
same shape, and both times the fix is in the shipped rule.

---

## What a call leaves behind

```
  336 place(s) call a block and compare the answer variable straight after
      49 leave NOTHING — the compare reads whatever was there before the call
     225 leave a routine's answer
       5 leave a number, and nothing in the block asks anything — a constant
      57 leave a number on the straight line, but an arm of the block asks a routine

    routine 0x05D answers at 79 place(s) through 1 block(s)
    routine 0x16C answers at 58 place(s) through 1 block(s)
    routine 0x16B answers at 58 place(s) through 1 block(s)
    routine 0x171 answers at 15 place(s) through 1 block(s)
    routine 0x18D answers at 14 place(s) through 1 block(s)
    routine 0x029 answers at  1 place(s) through 1 block(s)

  14 distinct block(s) are called this way, from 38 map(s)
```

Fourteen blocks. Three hundred and thirty-six places read an answer through one of them, and
**two hundred and twenty-five of those answers belong to six routines that were being credited
to nobody.**

`0x029` at SEVEN ISLAND is the one 214 found by hand — `special 0x0028 ; call 0x08170A1E ;
compare` — except that read it as `0x005D`, from `0x081A4EAF`, which is a different call in the
same script. Both are here now with the places counted.

## The rule, and the two ways I got it wrong

**The answer a call leaves behind is the LAST thing on its straight line that puts something in
the answer variable, of any kind.** Not the first, because a block that asks two things leaves
the second one's answer; and not down any branch, because a run takes one arm and this is a
question about the file.

**Wrong the first time: only routines counted.** The instrument credited `0x153` at fifty-seven
places. `0x081BBB1E` — 216's own subroutine — ends:

```
  0x1BBB2E   26 0D 80 53 01        specialvar 0x800D, 0x0153
  0x1BBB33   21 0D 80 01 00        compare 0x800D, 1
  0x1BBB38   06 01 52 BB 1B 08     if EQUAL goto ...
  0x1BBB3E   16 0D 80 01 00        setvar 0x800D, 1
  0x1BBB43   03                    return
```

The routine is asked and its answer is **thrown away** — the straight line ends by saying the
answer out loud. Crediting `0x153` there is the same fault the barrier was added for, one level
down.

**Wrong the second time: a literal was called a constant.** With `setvar` counted, fifty-seven
places read "a number the block says out loud — no ceiling at all", and that is false. The LESS
arm of the same block ends `setvar 0x800D, 0 ; return`. It returns one or nought **depending on
a routine this project cannot run**. A bucket named for a cause with the cause false, which is
trap 5 for the fourth time in this project and the second time in three milestones.

The fix is a fifth answer: a literal on the straight line is a constant only when **nothing
anywhere in the block asks a routine**. Five of the 336 are constants. Fifty-seven are not, and
the instrument says which and why.

## What this leaves

* **49 places** call something that leaves the answer variable alone entirely — the compare
  after them is reading whatever was there before the call, and one level does not say whose.
* **57 places** turn on an arm, and following arms is a level further in than this goes.
* **225 places** now have an owner.

Four breaks, four catches — first-not-last, any-slot-not-the-answer-slot, setvar-not-counted,
and literal-is-always-a-constant. Each is one of the two mistakes above or its mirror.

2867 → 2873 tests, all green. Nothing the run does changed.

---

## What is still owed

* **The 49 that leave nothing.** A call that touches nothing means the compare is reading an
  older answer still, and finding it means walking back past the call in the caller — which the
  barrier deliberately stops. That is a third instrument, not a tweak to this one.
* **The 57 that turn on an arm.** Both arms end in a literal here, so the block is a yes/no
  whose answer is a routine's. Following arms would say which routine, per arm.
* One level, on purpose. A call inside a called block leaves `Nothing` rather than being chased,
  and that is written into the rule rather than left to be discovered.
* `--through-a-call` reads only compares that come **straight** after the call. A compare two
  commands later is not seen, which is the same recall cost `Forks` accepts and for the same
  reason.

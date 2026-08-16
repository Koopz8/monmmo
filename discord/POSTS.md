# How to write a post in here

Two rules above all others.

**1. Write it for a player, not an engineer.** Most people reading have never
opened the repo and never will. They want to know what's different in the game.
If a sentence needs you to know what a fixture, a locator, a guard or a struct
is, rewrite it or cut it.

**2. Only what's new.** Check `COVERED.md` first. Anything already posted is
history, not news — no matter how good it was the first time.

---

## Plain language, concretely

| Don't write | Write |
|---|---|
| "the effect groups are no longer silent" | "every move actually does its thing now" |
| "the sample locator rejected packed recordings" | "it was missing every creature cry" |
| "instancing with a forty-player threshold" | "a busy town quietly splits into copies so it never becomes a crush" |
| "interest management replaced broadcast" | "you're only told about people you could actually see" |
| "1,568 tests passing" *as a headline* | one line at the end |

Jargon is allowed **once**, if the sentence right after it explains the thing in
ordinary words. Someone who reads the whole post should come away understanding
something, not feeling talked past.

Names of things in the game (SUBSTITUTE, COUNTER) are fine — people know those.
Names of things in the code are not.

## The shape

1. **What changed**, in two or three short chunks. Bold the first few words of
   each so it can be skimmed.
2. **One thing that went wrong**, if there was one. These are the best-read part
   of any devlog and the most honest thing in it. Tell it as a story.
3. **The test count**, one line.
4. **What's still to do**, briefly, in plain words.

## Rules of thumb

- **One or two Discord messages.** If it's running to four, it's an overview and
  needs cutting.
- **Point at #milestones** for the deep version rather than reproducing it.
- **Don't restate the project.** That's #welcome's job and it syncs itself.
- **Numbers, not adjectives** — but say what the number means.
- **Say what's unfinished or unproved.** That's the house voice; keep it.

## Before posting

```bash
node post.js "" posts/<file>.md --dry-run
```

More than two messages is the signal to cut. Then add a row to `COVERED.md`.

# Milestone 171 — Earning it together

Step three of co-op, and the first thing in it that writes to somebody else's save.

Milestone 170 made travelling together work by **borrowing**: while you are with somebody you
see the world they have opened, and nothing is written anywhere. That is right for travelling
and it is not enough for playing. A friend who walks through your doors and never keeps one is
a passenger.

So: a flag earned with somebody standing next to you is theirs too. The whole difficulty is
in which flags.

## The rule that could not be read

Two people playing together want a door one of them opened to be open for both. They do not
want a badge one of them earned to appear on the other.

The cartridge does not distinguish those. **There is no bit anywhere saying "this flag is
about the world"** — a badge and a guard moving out of the way are the same kind of thing to
the file, a numbered bit in a bit array. So the classification cannot be read, and a
hand-written list of flag numbers would be an invention nobody could ever check.

It can be **derived**, and the derivation is one question asked against the world file: *what
does turning this flag on actually change?*

- Somebody appears or disappears — `MapObject.HiddenBy`. This is the great majority of it;
  FireRed moves people about far more often than it locks a door.
- The boat sails — `FerryPass.Flag`.
- Nothing at all — and a flag that gates nothing in the world is a **mark on a character**.

`FlagGates` is that, computed from `WorldData` at startup. The server needs no cartridge for
it: it is derived from the same file it already loads to know what the ground looks like, so
it cannot drift from the world it describes.

### Wrong in a direction that is knowable

A flag gating something this project has not extracted yet reads as gating nothing, and stays
personal. That is a **door that fails to open for a friend** — annoying, immediate, and
somebody says so.

The opposite error hands somebody a badge, and nobody notices until much later, in a room
nobody connects to the cause. Of the two ways to be wrong, this is the one to choose, and the
choice is written into the class rather than left to whoever reads it.

`romdump --flags` prints the whole derivation: how many flags gate something, every one of
them and what it moves, and — the number that decides whether the rule is any good — how many
flags the scripts touch that gate nothing this build can see.

## What travels, and what does not

**Flags that are facts about the world.** Set and cleared both. The whole middle of this game
is flags being *cleared* — a person the story removes is as much a world fact as one it adds —
and propagating only the setting half would leave a friend looking at somebody who is no
longer there.

**Not variables.** A variable holds which starter was taken, which trainer the rival fielded,
how many steps an egg has left. Every one answers *what did you do*, and copying them across
is how one person's rival becomes somebody else's.

**Not marks.** Badges, and whatever else gates nothing.

**Only to people standing there.** In the company, and on the same map *and copy* — which is
what "there" means in a world with copies, and from inside is indistinguishable from being on
another map. Somebody who wandered off did not see it happen, and a story that reaches across
the world because two people are nominally travelling together is a story nobody can follow:
you would arrive in a town to find its events already over.

**Never to a stranger.** Being in a company is the opting-in. Writing to somebody's save
because they walked past is not a feature.

## Borrowed and earned, side by side

The two halves now do different jobs and it is worth having them in one place:

| | borrowed (170) | earned (171) |
|---|---|---|
| when | while travelling | when a script runs beside you |
| written | never | to the save |
| covers | everything the company has opened | only what gates the world |
| survives leaving | no | yes |

Which is exactly the drop-in shape: your friend joins three gyms behind and can immediately go
anywhere you can (borrowed), and everything you then do together he keeps (earned). He logs
off and loses only the borrowing.

## Seven guards, all of which fail

Every new rule broken on purpose; each failed a named test at once, including the two most
likely to go unwatched — that a mark does *not* travel, and that a stranger gets nothing. Both
are absence-of-behaviour rules, which are the ones a fixture usually cannot see, and both got
a test before either was implemented.

## Read and modelled

**Read.** What each flag gates — derived from the world file, which is itself read from a
cartridge. This is the first co-op rule that is not purely a decision.

**Modelled.** That a flag gating nothing is personal. That variables never travel. That
"standing there" means the same copy. That the classification should err towards a door not
opening rather than a badge appearing.

## What this owes

- **The item half.** A script that hands over a parcel hands it to one person. The design note
  says everyone present should get one; that is not built, and until it is, a propagated flag
  can put a save in a state its inventory cannot justify. **This is the next thing.**
- **Warps gated by flags**, if there are any the world file expresses — only people and the
  boat are classified so far, and `--flags` will say whether that is a gap.
- **The measurement itself has never been run against a cartridge.** Every number about the
  split is unknown. The rule is derived, so it is right by construction for whatever the file
  says — but how *many* flags fall each side is a finding nobody has yet.

2485 tests.

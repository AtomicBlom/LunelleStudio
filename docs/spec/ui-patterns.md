# Spec: patterns in the studio (UI, Phase 4.3)

Status: **implemented, drag and drop to come**. Builds on Lunelle's `docs/spec/patterns.md` and `transactions.md`.

## What it shows

- **The library's Patterns section** lists the project's patterns (`Patterns/**/*.pattern.json`) by id.
- **A part that plays a pattern** shows, in the roll, which pattern and how many parts in the project play it, in place
  of its notes: its notes are the pattern's, edited in the pattern's file (for every part that plays it) or detached.
- **A part with notes of its own** offers **Extract pattern** beside its other actions.

## What it does

Each is one step of the document (transactions.md), so it undoes, and the song sounds the same before and after an
extract or a detach (patterns.md §5):

| Action | Does |
|---|---|
| **Extract pattern** | Writes the part's notes, step and notation options to `Patterns/parts/<track>-<section>.pattern.json` (the first such name free), with the song's meter, and has the part play it. The part keeps its track, condition, placement and velocity. |
| **Detach pattern** | Writes the pattern the part plays into the part, as the resolver writes it in (its transforms applied, a sequence as a `"sequence"`), so the notes are the part's own. |
| **use**, on a pattern in the library | Has the part chosen play the pattern in place of its notes. |

## Still to come

- Dragging a pattern onto a lane (a new part) or onto a part (its content replaced), and a modifier to drop an inline
  copy, as the plan has it. The model is in place (`Workbench.UsePattern`); the drag needs the lanes' drop targets.
- Naming an extracted pattern before it is made (today it is named for its track and section; rename the file after).
- Dimming patterns that wouldn't fit the part chosen (a beat on a patch track).

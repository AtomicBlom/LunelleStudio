# Spec: patterns in the studio (UI, Phase 4.3)

Status: **implemented**. Builds on Lunelle's `docs/spec/patterns.md` and `transactions.md`.

## What it shows

- **The library's Patterns section** lists the project's patterns (`Patterns/**/*.pattern.json`) by id, in their
  folders. While a part is chosen, a pattern that wouldn't fit it (a beat for a patch's track, a line or a strum for a
  kit's, patterns.md §6) is dimmed, and its tooltip says why.
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
| **A pattern dragged** onto a part in Arrange | The part plays it in place of its notes, as **use** does. |
| … onto a lane's ground | A new part of the lane's track in that section plays it, after the track's others there. |
| … with Shift held as it is let go | The pattern's content is written in instead (a drop, then a detach, as one step): a copy, the part's own. |

As the drag goes, the part or lane under it is lit, or tinted as an error where the pattern wouldn't fit, and the ghost
says what letting go would do. A drop that wouldn't fit is refused, and says why.

## Still to come

- Naming an extracted pattern before it is made (today it is named for its track and section; rename the file after).
- A drop placed by where in the lane it lands (before or after a part), rather than after the track's parts.

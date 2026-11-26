# Spec: the library browser (UI, Phase 4.1)

Status: **implemented in part**. Builds on Lunelle's `docs/spec/project.md`.

## What it shows

The library is the project's music and Lunelle's own, by kind, filtered by the words typed:

| Section | Holds |
|---|---|
| Songs, Patches, Kits | The project's own entries, by id, so a folder's entries sit together: each shows its id's last segment, with its folder beside it (format 2's grouping, project.md §2.1), and its whole id as its tooltip. |
| Patterns | The project's patterns (ui-patterns.md). |
| Cues | The project's cues, ticked where the open song imports them (ui-cues.md). |
| Lunelle's sounds | Lunelle's library (`lunelle:…`): patches by category, then kits. A click plays one on the keys; **add** makes an entry of the project's based on it (overrides.md), to change without copying. |

The Files view (the raw files) stays, for a developer.

## Still to come

- A collapsible tree by source, kind and folder, in place of the sorted lists with their folders shown.
- Kind filters beside the search.
- Libraries other than Lunelle's (a game's, by path) listed as sources of their own.
- Entries that wouldn't fit what they are dragged onto, dimmed as the drag goes (a beat pattern over a patch track).

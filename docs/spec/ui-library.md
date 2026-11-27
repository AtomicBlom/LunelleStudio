# Spec: the library browser (UI, Phase 4.1)

Status: **implemented in part**. Builds on Lunelle's `docs/spec/project.md`.

## What it shows

The library is the project's music and Lunelle's own, by kind, filtered by the words typed:

| Section | Holds |
|---|---|
| Songs, Patches, Kits | The project's own entries, by id, as a tree: a folder's header (format 2's grouping, project.md §2.1) heads its entries, each its id's last segment with its whole id as its tooltip, and a click on the header folds them away or opens them. |
| Patterns | The project's patterns (ui-patterns.md), as the same tree. |
| Cues | The project's cues, ticked where the open song imports them (ui-cues.md). |
| Lunelle's sounds | Lunelle's library (`lunelle:…`): patches by category, then kits. A click plays one on the keys; **add** makes an entry of the project's based on it (overrides.md), to change without copying. |

**Filtering.** Words typed show only the rows whose ids have every one, and find a row in a folded folder as well. Under
the search, the kinds (songs, patches, kits, patterns, cues, sounds): with none ticked every kind shows; with some, those
alone.

The Files view (the raw files) stays, for a developer.

## Still to come

- Libraries other than Lunelle's (a game's, by path) listed as sources of their own.
- Patches and kits dimmed as a pattern is (ui-patterns.md), where they wouldn't fit what is chosen.

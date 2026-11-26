# Spec: the cues panel (UI, Phase 4.2)

Status: **implemented**. Builds on Lunelle's `docs/spec/cues.md` and `transactions.md`.

## What it shows

- **The library's Cues section** lists the project's cues, from its cue files (`Cues/**/*.cues.json`), by id, each with
  its kind (a level, or a choice's options) and its `about` as a tooltip. A tick says the open song imports it.
- **The cue strip** shows the open song's imported cues, in the order the song writes them, then any cue a song of code
  reads without a file to import it into. An imported cue the song doesn't read is shown dimmed, as today.
- **Opening a song** starts each imported cue at its preview value (its value in the song's `"cues"`); one previewed at
  `null` keeps the value it has. Reading the project again after an edit doesn't reset the cues.

## What it does

Every change is a step of the document (transactions.md), so it undoes:

| Action | Does |
|---|---|
| Tick a cue | Imports it into the open song, previewed at its initial value. |
| Untick a cue | Takes it out of the song's imports; refused, with why, while the song reads it. |
| **+** under Cues | Makes a cue: an id (a namespace before a `/`), a level or a choice among options, and its about. It goes into `Cues/<namespace>.cues.json` (or the project's own cue file, named for the project) and is imported into the open song, as one step. |
| A condition naming a project cue | Imports the cue as the condition is set. A name that is no cue says to make it with **+**. |
| **…** → rename | Renames the cue in its file and everywhere every song reads it (imports, conditions, levels, follows, exports), as one step. The namespace stays. |
| **…** → delete | Deletes a cue no song reads, from its file and every song's imports, as one step. |

## Out of scope for now

- Moving a cue to another namespace (make a new one, and move the songs to it).
- Editing a cue's options or initial value from the panel (edit the cue file; the project is read again).
- A tree by namespace: the list is sorted by id, so a namespace's cues sit together.

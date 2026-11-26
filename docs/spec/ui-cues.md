# Spec: the cues panel (UI, Phase 4.2)

Status: **implemented**. Builds on Lunelle's `docs/spec/cues.md` and `transactions.md`.

## What it shows

- **The library's Cues section** lists the project's cues, from its cue files (`Cues/**/*.cues.json`), as a tree by
  namespace (each namespace's header folds its cues away), each with its kind (a level, or a choice's options) and its
  `about` as a tooltip. A tick says the open song imports it.
- **The cue strip** shows the open song's imported cues, in the order the song writes them, then any cue a song of code
  reads without a file to import it into. An imported cue the song doesn't read is shown dimmed, as today. Its header
  has **+ cue**, and each cue **start here**.
- **Opening a song** starts each imported cue at its preview value (its value in the song's `"cues"`); one previewed at
  `null` keeps the value it has. Reading the project again after an edit doesn't reset the cues.

## What it does

Every change is a step of the document (transactions.md), so it undoes:

| Action | Does |
|---|---|
| Tick a cue | Imports it into the open song, previewed at its initial value. |
| Untick a cue | Takes it out of the song's imports; refused, with why, while the song reads it. |
| **+** under Cues, or **+ cue** on the strip | Makes a cue: an id (a namespace before a `/`), a level or a choice among options, and its about. It goes into `Cues/<namespace>.cues.json` (or the project's own cue file, named for the project) and is imported into the open song, as one step. |
| A condition naming a project cue | Imports the cue as the condition is set. |
| A condition naming no cue | Offers **Make** under the condition, for each name as the condition reads it: compared with a number, a level (`lava/heat > 0.5`); checked for an option, a choice among the options it names (`biome is Forest`; with `Other` where it names one, since a choice has two or more). Made as one step, imported, and the condition taken. A condition is checked against the cue files as they are, so it holds before the project is read again. |
| **start here** on the strip | Keeps the cue's value now as the song's preview value (its `"cues"`), where the song starts it. |
| **…** → save | Edits the cue's file entry: a choice's options (added, reordered or taken out; kept options keep their `about`, and one a song names can't be taken out), where it starts (`"initial"`), and its about. A level stays a level and a choice a choice. |
| **…** → rename | Renames the cue in its file and everywhere every song reads it (imports, conditions, levels, follows, exports), as one step. The namespace stays. |
| **…** → delete | Deletes a cue no song reads, from its file and every song's imports, as one step. |

**In a project with code**, a cue is also a name in the game's code (the generator's `Cues.Lava.Heat`, and
`Cues.Lava.HeatOption` for a choice). The cue's menu says so above its edits, and the options' tooltip that code naming
an option taken out won't build until it changes.

## Out of scope for now

- Moving a cue to another namespace (make a new one, and move the songs to it).
- Renaming an option (add the new one, move the songs to it, take the old one out).

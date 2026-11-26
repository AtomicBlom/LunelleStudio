# Spec: song exports in the studio (UI, Phase 4.5)

Status: **implemented, the automation lane to come**. Builds on Lunelle's `docs/spec/exports.md`.

## What it shows

**exports**, at the transport's end, opens the open song's exports: each by its title, with **render**, and **render all**
where there are several. Under them, a name and **record** (or **stop**, while recording).

## What it does

- **render** renders the export with Lunelle's `SongExport`, the edits saved first so it is what the project now says,
  to `<song>--<export>.wav`. The same export renders the same bytes every time (exports.md §6). On the desktop the
  studio asks for a folder to write into; in the browser each file is a download.
- **record** plays the song from its start. The cues' values then are the export's starting values, and every cue moved
  on the strip while it plays is an event at the bar and beat it was moved on (a later move on the same beat replaces
  the earlier). **stop** writes the export into the song, named as given, ending at the bar after the one playing: one
  undoable step. Render it, or edit its events in the song's file.
- A project with code renders nothing yet: its files alone would play its code as rests, which is not its music. The
  panel says so.

## Still to come

- An automation lane in Arrange, showing an export's events over the sections and editing them.
- Recording macro moves (a track's macro held), and snapping to the beat rather than taking the beat a move falls in.
- Playing an export live (its events applied as the song plays) before rendering it.
- Rendering a project with code, through its build.

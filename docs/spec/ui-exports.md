# Spec: song exports in the studio (UI, Phase 4.5)

Status: **implemented**. Builds on Lunelle's `docs/spec/exports.md`.

## What it shows

**exports**, at the transport's end, opens the open song's exports: each by its title, with **play** (or **stop**, while
it plays) and **render**, and **render all** where there are several. Under them, a name and **record** (or **stop**,
while recording).

**The automation lane**, under Arrange's tracks, shows one export's events over the sections, chosen at its head. Each
event is a mark labelled with what it does (`mood → Dark`, `lead bright → 0.4 over 2 beats`, `jump → chorus`), in a
section's column: an event at a section's start or end there; one at a bar in the section the song was in at that bar
when the export last played live, or, until it has, along the sections in their order from the export's start. A
click on a mark edits it (where, what, to, over) or deletes it; **+ event** adds one. Each is one undoable step.

A track's inspector in Arrange has **Macros**: a slider for each macro its patch has, holding it as the song plays
(Lunelle's `HoldMacro`) over what the song makes it follow, and **let go** to give it back.

## What it does

- **render** renders the export with Lunelle's `SongExport`, the edits saved first so it is what the project now says,
  to `<song>--<export>.wav`. The same export renders the same bytes every time (exports.md §6). On the desktop the
  studio asks for a folder to write into; in the browser each file is a download.
- **play** plays the export live: its cues where it starts them (else the song's previews), from its start section,
  each event applied as the song reaches it (a cue set or ramped, a track's macro held, ramped or let go, a jump), and
  stopped at its end. Live, an event lands on the beat it names as the player reaches it; the render is the exact one.
- **record** plays the song from its start. The cues' values then are the export's starting values, and every cue moved
  on the strip, and every track macro held or let go in the inspector, while it plays is an event on the beat nearest
  the move: the beat playing, or the next once more than half of it has gone (a later move on the same beat replaces
  the earlier). A macro is named as the manifest names it, else by its number. **stop** writes the export into the
  song, named as given, ending at the bar after the one playing: one undoable step. Render it, or edit its events in
  the song's file.
- A project with code renders nothing yet: its files alone would play its code as rests, which is not its music. The
  panel says so.

## Still to come

- Dragging a mark along the lane to move its event.
- Rendering a project with code, through its build.

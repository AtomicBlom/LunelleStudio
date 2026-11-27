# Spec: the editing model (transactions)

Status: **draft, implemented in Phase 3** (docs/plan.md). Builds on Lunelle's format 2 (its `docs/spec/`).

## 1. What changes

Today every edit is written straight to the project's files, and nothing can be undone. From here:

- **Edits go to a document, not the disk.** The project's files on disk are the last save. What the studio shows and
  plays is the last save with the edits since it on top.
- **Every edit is a step in a transaction log**, which undo and redo walk.
- **Save** (Ctrl+S) writes the edits to disk and clears the log. A **dirty marker** shows there is something to save.
- **The log is on disk as it grows**, so a crash loses nothing: opening the project again recovers the edits.

## 2. The document

The document is a layer over the project's files (`IMusicFiles`, the seam every write already goes through):

| Operation | On the document |
|---|---|
| Read a file | Its edited text if it has one, else the disk's. A file deleted since the save doesn't exist. |
| Write a file | Recorded as a step; the disk isn't touched. |
| Delete a file | Recorded as a step (the file's content becomes "none"). |
| List a folder | The disk's files, plus files made since the save, less files deleted since. |
| Watch a folder | The disk's changes (§6), plus every step done, undone or redone. |

**Everything that reads music reads the document**: a project of files alone (`WorkbenchFolder`), and, in a project
with code, the compile (its music files and C# are given to Roslyn from the document, §7). So hot reload follows every
edit, undo and redo as it does a save today.

**What isn't in the document**: the workbench's own state (`.lunelle-studio/workbench.json`) and the studio's settings,
which are written straight to disk, and samples a library brings (only text files are edited).

## 3. Steps and transactions

A **step** is one file's change: its path, its content before (or none: it didn't exist) and after (or none: it is
deleted). A **transaction** is one or more steps undone and redone together: renaming a cue across every song that
names it, extracting a pattern into its own file and the part that now refers to it.

The first version records **whole files**, before and after. That keeps every operation correct (it can't miss a
field), and the files are small. Finer operations can come later without changing what a step is to the log.

### 3.1 Coalescing

Some edits come as a stream: a slider dragged, notes painted in the roll, a song file saved after each pause. One
gesture is one undo step:

- **A write to the same file as the last step, within 2 seconds of it, joins that step** (its "after" is replaced; its
  "before" is kept), unless the last step has been **sealed**.
- The workbench seals the last step when the user does something else: chooses another song, patch or kit, presses
  undo or redo, saves, or a transaction ends.
- A transaction's steps are never joined with a step outside it.

### 3.2 Undo and redo

- **Undo** puts back each step of the last transaction, newest first: the file's content before, or deletes a file the
  transaction made.
- **Redo** does the transaction again. A new edit after an undo drops what could have been redone.
- Undo and redo are steps of the document like any other: hot reload, the library and the open song follow them.
- The log has no limit within a session. Saving clears it: **undo doesn't reach past a save.**

## 4. Save, revert and the dirty marker

- **Save** writes every file the log changed, as the document has it now (a file made and then deleted is not
  written), deletes the files it deleted, clears the log and its file on disk.
- **Revert** drops every edit since the save: the document is the disk again.
- **Dirty** is "the document differs from the disk": edits since the save that didn't all come back to where they
  were. The title shows `•` before the project's name while it is dirty. Closing a dirty studio saves the log (§5) and
  says so; nothing is lost and nothing is written to the project unasked.

## 5. The journal and recovery

The log is kept in `.lunelle-studio/journal.jsonl` beside the project (the browser's storage, in the browser): a line
per step as it is done, undone or redone:

```
{"op":"do","tx":3,"path":"Songs/aurora.song.json","before":"…","after":"…"}
{"op":"undo","tx":3}
{"op":"redo","tx":3}
{"op":"saved"}
```

- **`before` and `after` are the files' text** (null for none). The journal is written with each step, and flushed, so
  a crash loses at most the step being written.
- **A joined step** (§3.1) is written as another `do` with the same `tx` and path; replaying keeps the first `before` and
  the last `after`.
- **Opening a project with a journal** replays it over the disk: the document comes back as it was, every step still
  undoable, and the studio says "recovered N unsaved changes". If a file's text on disk is no longer the `before` the
  journal's first step on it expects (it was changed outside the studio since), that file's steps are dropped and the
  studio says which.
- A journal that can't be read is renamed `journal.jsonl.broken` and the project opens from the disk.

## 6. Changes made outside the studio

The disk is watched as today (an editor, git, a sync tool):

- **A file the document hasn't edited** is taken up from the disk at once, as today.
- **A file the document has edited** keeps the document's version, and the studio shows a notice naming it: "saving
  will replace Songs/aurora.song.json, changed outside the studio". Whichever saves last wins. Revert takes the disk's.

## 7. In a project with code

- The compile reads the project's music files, and its C#, from the document. A step on a `.cs` file (ejecting a part,
  bringing one back) is a step like any other, and undoable.
- The `.csproj` and what it imports are read from disk: a change to them still needs a save, as it does to MSBuild.

## 8. Test cases

1. **Edit, undo, redo.** A song edited twice, then undone twice, is the disk's again, and redone twice is the second
   edit; the disk is untouched throughout.
2. **Coalescing.** Ten writes to one file within two seconds are one step; a write after the step is sealed is another.
3. **Transactions.** A transaction writing three files undoes all three at once; a file it made is deleted on undo.
4. **Save.** Saving writes the edited files, deletes the deleted ones, clears the log, and the document is clean;
   undo then does nothing.
5. **Recovery.** Edits, then the document dropped without saving: a new document over the same files and journal has
   the edits, and undoes them one by one. A file changed on disk since is left out of the recovery, said.
6. **Outside changes.** A disk change to an unedited file is seen at once; to an edited one, the document's version
   stays and a notice names the file.
7. **Reload.** A project of files alone reads the music again on every step, undo and redo, as it does on a save.

## 9. Out of scope

- Finer operations than whole files (a field changed, a note added): the log's shape allows them later.
- Undo across a save, or history kept between saves (that is git's).
- Several people editing one project at once.

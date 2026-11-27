# Plan: shipping the studio, format 2, and the editing model

Written 2026-11-26 from a design discussion; built 2026-11-27 (see "Where it stands", at the end). Nobody uses LunelleStudio or Lunelle's
formats yet (no web release, no NuGet packages), so **nothing needs migrating**: formats can change freely.

The work spans four repositories: **Lunelle** (format, resolver, generator, player, export runner), **LunelleStudio**
(app, editing model, UI), **Kaldra** (dialogs, file drop), and **Sirrow** (offline rendering, already there).

---

## Decisions

### Product
- **One desktop application.** It opens music on its own, and has a code mode when it is given a `.csproj`. There is no
  separate dotnet tool and no separate standalone build. The browser build stays music-only.
- **Code mode requires the .NET SDK** (`dotnet` on PATH). `DesignTimeBuild` runs `dotnet msbuild` out of process for
  the compiler's arguments, the extension's property and the extension's build. That stays: in-process MSBuild would
  still need the SDK, and would tie the app's runtime to it. When `dotnet` is missing, say so and offer to open the
  project's music files alone.
- **Self-contained, untrimmed, ReadyToRun; not Native AOT.** Roslyn and collectible load contexts need the JIT.
  Roslyn assemblies must only load when a `.csproj` opens, so starting on music alone stays fast.
- Consequences of a self-contained app:
  - Code projects must target the app's .NET or older (`RollForward=LatestMajor` no longer helps), and the app should
    say so plainly.
  - App releases are locked to the project's Kaldra: `SharedReferences` only opens projects built against the exact
    Kaldra the app runs. The error must say "this project uses Kaldra X; this studio is Y". Ideally the project
    records the studio version it expects.

### File format (format 2)
- **`.lunelle` is the file users pass around.** It is a zip, and unzipping it gives the authoring folder, which is a
  lossless round trip. A player opens `.lunelle` files. Games author in the unpacked folder, with git for the JSON and
  Git LFS for samples.
- **`lunelle.meta.json`** is the manifest, at the root of both forms. It holds:
  - `format`, `name`, and optional title, version, about, author and licence;
  - `libraries`: named references, each pinned to a `version` or pointing at a `path` (a folder or another `.lunelle`);
  - `macros`;
  - cues are *not* here: they are entries under `Cues/` (decided in the project spec);
  - `songs`: an optional order for players, with unlisted songs after the listed ones.
- **What the manifest doesn't hold:**
  - The content itself, which is found by convention in `Songs/`, `Patches/`, `Kits/`, `Patterns/` and `Cues/`.
    Adding a song never touches the manifest, which matters for merges.
  - Workbench state, the transaction log, and code-mode settings. The extension is named by the `.csproj`.
- **Packing bundles libraries.** It bundles the library entries the project actually uses, at their pinned versions,
  under `Libraries/<name>/`, so a `.lunelle` stands alone. Overrides are kept, not flattened, so the file stays
  editable.
- **JSON stays; no comments.** The studio rewrites files, so comments would be silently lost (JSONC), or would mean a
  new parser that keeps them (YAML, TOML) in the netstandard2.0 generator. Instead, every cue, cue option, macro, song,
  patch, kit and pattern gets an **`about`** field (the name songs and patches already use). The studio shows it, and the generator emits it as XML doc
  comments in code mode.
- A new game project in folder form gets a `.gitattributes` that sends samples (`*.ogg`, `*.flac`, `*.wav`) to LFS.

### Libraries and overrides
- There are three layers: **Lunelle's built-in library** (today's `presets/`), **the game's library**, and **the song**.
  Any project can be a library.
- **References are prefixed with a library's local name**, such as `"base": "lunelle:fm-keys"` or `"base": "game:lead"`; `/` only ever separates folders within an id, and an entry's id is its path below its kind's folder. There are no
  precedence rules and no collisions.
- **Overrides are a flat list of `path → value`, where paths address array items by stable key** (an operator's id),
  never by index. RFC 6902 JSON Patch is index-based and silently retargets when the base changes. JSON Merge Patch
  can't address array items at all. Patches need stable ids on operators and effects.
- An override of a library patch is a file in `Patches/` that names its base. A song's overrides live in the song.
- **One resolver in Lunelle** is shared by `MusicFolder` (runtime) and the generator (code mode). Mind the
  generator's netstandard2.0 limits.
- Overrides whose target has been renamed or deleted in the base are **reported, never dropped**.
- Preset **add** changes from "copy it and everything it needs" to "reference it, and override it if you want".

### Cues
- Cues are **defined project-wide** and **imported per song**: `"cues": { "intensity": 0.6, "lava/heat": 0.2 }`.
  The workbench shows only the song's imported cues.
- A song's value is **the preview's starting value only**, not a reset at runtime: the game owns cue values.
  "Set when the song starts" would be a separate, explicit setting.
- A condition that reads a cue the song hasn't imported is an error.
- **Namespaced names** (`lava/heat`) support hundreds of cues (a party game with many minigames).
- Macros stay project-wide and aren't imported.

### Patterns
- A new library entry kind, **pattern**, with sub-kinds beat, line and strum. It holds what a part holds today, minus
  the track and the condition.
  - Beats are keyed by kit piece name (`kick`, `hat-closed`), so they work on any kit with those pieces.
  - Lines are scale degrees.
  - Strums voice the current chord.
- A part refers to one with `"pattern": "game:four-on-the-floor"`, and the pattern repeats to fill the part.
- **Overrides on a reference are a few transforms only**: velocity scale, swing, humanise, offset. Changing the notes
  means **detach**.
- Patterns can be made of patterns (with a cycle check).
- **Mismatches are reported**: a piece the kit lacks, a kind that doesn't fit the track, a meter mismatch.

### Song exports (renders for an OST)
- Songs carry named **`exports`**, several per song (for example Past, Present, Good Future and Bad Future). Each has:
  - a `seed`;
  - starting `cues`;
  - `events` that set or ramp cues and macros, placed at `bar:beat` or at the start of a section;
  - an `end`: a number of loops plus a fade, an end section, or a fixed bar.
- **They must be deterministic.** Seeds already come from names ([CompiledSong.cs](../../Lunelle/BinaryVibrance.Lunelle/Music/CompiledSong.cs)
  `Seed`, and `MusicPlayer.Prepare(song, scale, seed)`), and Lunelle uses no `System.Random`. Sirrow's
  `OfflineSession` renders faster than real time, in fixed blocks, the same every time. Events are applied between
  blocks at fixed frames, never on wall-clock or frame timing.
- **The export runner lives in Lunelle**, so the studio, a player and a command line all produce the same bytes.
- Output starts as WAV with `LOOPSTART`/`LOOPLENGTH` tags (so an export can stream as a looping game track). Later:
  FLAC/OGG/MP3 encoders, stems, and an "album" (track order, titles, artist) in `lunelle.meta.json`.

### Editing model
- **A document model with a transaction log** replaces writing each edit straight to the project's files.
  - Each operation carries both its forward and inverse change, so undo and redo walk the log.
  - The log is appended to disk as edits happen (in `.lunelle-studio/` or `%LOCALAPPDATA%`). Recovery after a crash
    is "last save + replay the log".
  - **Save** (Ctrl+S, with a dirty marker) writes the document and truncates the log. Hot reload still runs after
    each operation.
- The first version logs the before and after of the touched entity (one song or one patch). Operations can get finer
  later without changing the log's shape.
- **Coalescing**: a slider drag or a roll paint is one step.
- Multi-file edits (renaming a cue, extracting a pattern) are one transaction, and so one undo step.
- Edits made outside the studio to a file with unsaved changes: show a notice and keep the edits. Whichever saves last
  wins.

### Workbench state
- `WorkbenchState` existed to survive `dotnet watch` restarts, which no longer happen. Opening should start from sane
  defaults:
  - **Keep** `Song`, and probably `Mode`.
  - **Move** `LibraryPinned`, `PartPanelHeight`, `Octave` and `Velocity` to the app-wide `StudioSettings`.
  - **Drop** `Playing`, `Section`, `LoopSection`, `Cues`, `Forced`, `Tracks`, `ScaleRoot`/`ScaleName` and
    `SelectedPart`.
  - **Decide** about the bench's `Patch`/`Kit`.
- This also stops `.lunelle-studio/workbench.json` from changing every time something moves.

### Rejected
- A separate standalone build plus a dotnet tool. It was simpler for packaging, but two products, a command-name clash
  and version pinning through tool manifests.
- Native AOT, because of Roslyn.
- Compiling simple code projects without the SDK. It's possible for template-shaped projects, but it stops working
  at the first extra package or project reference.
- A draft folder that overlays files; the transaction log replaces it.
- RFC 6902 JSON Patch, JSONC, YAML and TOML (see above).

---

## Work, in order

### Specifications: before the phases that depend on them
The format is a contract between Lunelle's runtime, its generator, the studio and any future player, so it is
specified before it is built. Each spec says: the schema with examples, how it resolves, the errors it reports, what
is deliberately out of scope, and the test cases an implementation must pass. Specs live with whoever owns them.

**Written up front (before Phase 2):** drafts of all five are on Lunelle's `format-2-specs` branch.
1. **Lunelle, `docs/spec/project.md`**: `.lunelle` packing and unpacking, `lunelle.meta.json`, the content folders,
   library references, bundling, and `description`.
2. **Lunelle, `docs/spec/overrides.md`**: stable ids, override path syntax, the resolution order across the three
   layers, and broken overrides.
3. **Lunelle, `docs/spec/cues.md`**: definitions, namespaces, song imports, preview values, and how choice options
   are referenced.
4. **Lunelle, `docs/spec/patterns.md`**: kinds, references, transforms, composition, and mismatches.
5. **Lunelle, `docs/spec/exports.md`**: the schema for exports and events, the timing and determinism rules, endings,
   and output (WAV and loop tags).

**Before Phase 3:**
6. **LunelleStudio, `docs/spec/transactions.md`**: the document model, the shape of an operation and of the log,
   coalescing, recovery, save, and changes made outside the studio.

**At the start of each part of Phase 4:**
7. **LunelleStudio, `docs/spec/ui-*.md`**: the library browser, cues panel, patterns, patch overrides, and exports.
   These are written then, not now, because they depend on what the format turns out to be.

Phases 0 and 1 need no specs. They are self-contained, and this plan is enough for them.

### Phase 0: small, standalone (studio)
1. Trim `WorkbenchState` as above. Move layout preferences into `StudioSettings`, and stop autoplay on open.
2. A clear error when a project's Kaldra doesn't match the app's (`SharedReferences`).
3. Detect a missing `dotnet` on opening a `.csproj`, and offer to open its music files alone.

### Phase 1: a desktop app that ships
1. **Kaldra:** expose SDL3's open-folder, open-file and save-file dialogs, and file drop, through `DesktopAppHost`.
2. **Starting with no arguments.**
   - A default project in `Documents\Lunelle Studio\`, seeded with the Showcase. Move the seeding and `Import` out of
     `Web/BrowserProject.cs` into code both hosts share.
   - Remember the last project in `StudioSettings`.
3. **Open, import and export on the desktop.** Share the transport buttons from `Web/WorkbenchApp.Web.cs`.
4. **Packaging.**
   - Self-contained ReadyToRun publish, `WinExe`, icon, version from `Version.props`.
   - Fonts baked from the Vysara checkout.
   - Decide on a DX12 fallback; check macOS.
   - Installer, code signing, and the `.lunelle` file association.
5. A CI job that publishes the app.
6. Once packages exist: new code projects reference Lunelle and the generator as packages rather than
   `$(StudioDirectory)` (`ProjectTemplate.cs:58`, which an update to a versioned install folder breaks), then remove
   `CopyMusicGenerator`.

### Phase 2: format 2 (Lunelle)
1. `lunelle.meta.json`, the content-by-convention folders, `.lunelle` packing and unpacking (round-trip test), and
   `about` fields.
2. Libraries: the built-in library from `presets/`, namespaced references, pinned versions, and bundling when
   packing.
3. Stable ids on patch operators and effects, keyed-path overrides, the shared resolver (`MusicFolder` + generator),
   and reporting broken overrides.
4. Cue imports per song, namespaced cues, and the error for unimported cues.
   - Check: does a choice cue's value come from its option's index (`CueBase.OptionValue(i)`)? If so, conditions
     must refer to options by name, or reordering an option silently changes them.
5. Pattern entries, `"pattern"` references, transforms, composition, and mismatch reporting.
6. `exports` in songs, and the export runner on `OfflineSession`, with a test that renders twice and compares
   hashes. Kit round-robin takes are the first thing to check for determinism.
7. A player that reads a packed `.lunelle` at runtime, through the same resolver.

### Phase 3: the editing model (studio)
1. A document model over the project, with the transaction log, undo/redo, coalescing, on-disk recovery, save,
   revert and the dirty marker.
2. **Code mode reads music through the document**, not the disk. `MusicProject` has its own `FileSystemWatcher` and
   reads with `File.ReadAllText` (`MusicProject.cs`, `MusicCode.cs`, `CatalogReader.cs`). `.cs` files stay on disk.
3. Notices for changes made outside the studio.

### Phase 4: studio UI
1. **Library browser.** A tree (source → kind → folders/tags) replaces the flat `FilesViewModel` list. Search and
   kind filters cover everything. Entries that would mismatch the drop target are dimmed. The raw file view stays as
   a developer view.
2. **Cues panel on the left.**
   - A namespace tree with "imported by this song" checkboxes; create and edit (name, kind, options, initial value,
     description).
   - Create a cue from `+ cue` on the strip, or from an unknown name typed in a condition; both also import it.
   - Rename, delete and option edits are transactions, and warn in code mode (they rename generated API).
   - The existing cue strip shows only the song's imported cues.
3. **Patterns.**
   - Drag from the browser: onto an empty spot in a lane inserts a part; onto a part replaces its content. A modifier
     drops an inline copy instead.
   - Extract to library, and detach.
   - "Edit pattern (used by N parts)" vs "detach and edit"; library entries are read-only (editing makes a project
     override).
4. **Patch overrides.** Overridden fields are marked, with "revert to base".
5. **Exports.**
   - A panel to play live, export one, or export all.
   - An automation lane in Arrange.
   - **Record**: cue and macro moves made while playing become events, snapped to the beat.

---

## Open questions
- `songs` in the manifest: optional order (preferred), or an explicit include list?
- The exact syntax of override paths and keys.
- Which transforms a pattern reference allows.
- Workbench state: keep `Mode` per project? Keep the bench's `Patch` and `Kit`?
- A DX12 fallback on Windows; the backend on macOS.
- The command line for exports (`lunelle-studio export <song> --name "<export>"`): in the studio, or a Lunelle tool?

---

## Where it stands (2026-11-27)

Built, tested and pushed, on branches that stack (each from the last):

| Phase | Where | State |
|---|---|---|
| Specs | Lunelle `format-2` (`docs/spec/`), studio `docs/spec/` | All written; Lunelle's marked implemented, with what was settled as built |
| 0 | studio `studio-phase-0` | Done: opens stopped at its song and mode, layout in the studio's settings; Kaldra versions named; a missing SDK opens the music alone |
| 1 | studio `studio-phase-0`, Kaldra `file-dialogs` | Done but 1.6: SDL 3 file dialogs and drops; starts at the last project or the showcase in Documents; open, export, drop; self-contained ReadyToRun publish, icon, DirectX 12 behind Vulkan, an Inno Setup installer opening `.lunelle` files, the `Studio` workflow. 1.6 (packages instead of `$(StudioDirectory)`) waits for packages |
| 2 | Lunelle `format-2` | Done: the shared resolver (runtime and generator), libraries and Lunelle's own, overrides, cues, patterns, packing, exports with `HoldMacro`, `FromPackage` |
| — | studio `format-2` | The studio on format 2: presets as Lunelle's library, the Showcase and HelloSynth converted |
| 3 | studio `editing-model` | Done: the document and journal, undo across files, save, recovery, outside changes; code mode compiles from it |
| 4 | studio `studio-ui` | Done: the library as a tree by folder with kind filters; cues made from the library, the strip or a condition, edited, renamed and deleted, with code-mode warnings; patterns dragged onto parts and lanes (Shift for a copy), extracted and detached; override marks on each designer line with its revert; exports played live, rendered, recorded from cue and macro moves snapped to the beat, and an automation lane in Arrange. Smaller follow-ups are listed under each `docs/spec/ui-*.md`'s "Still to come" |

To merge: Kaldra `file-dialogs`, then Lunelle `format-2`, then the studio's branches in order. The studio's CI checks
out its siblings' master branches, so it goes green once those are merged.


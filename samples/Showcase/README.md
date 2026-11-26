# Showcase

The browser studio's first project, and a tour of what Lunelle's music files can say: five songs, each arranged by two
cues as it plays, on instruments from the presets (`presets/`). It is music files alone, with no C#. On the desktop:

```
dotnet run --project BinaryVibrance.LunelleStudio.App -- samples/Showcase --data
```

## Try this

Play a song and move the cues in the workbench's cue panel. **intensity** runs from 0 to 1, and **mood** is Calm, Bright or
Dark. Each song's sections say where to go next by their exits, so a change is heard at the next section's end. A part
that waits on a cue comes in at the next bar, or at the start of its phrase when it is marked on-phrase. Each song's
`"about"`, and each section's, says what it does.

| Song | What it shows |
|---|---|
| `aurora` | The tour. An intro, a groove, a build and a drop, chosen by intensity, up and back down. A sweep over the build, the riser and an airy supersaw opened across the section, and a snare roll that climbs a step at a time (`5566 7788 99XX`). Three fills, one each pass, shuffled (a variation). A bass bus whose duck keys on the kick. Filters that follow the intensity (a track's macro following a cue). Motifs chosen by mood, each on-phrase. A Phrygian lead when Dark. |
| `back-porch` | Swing (0.62) on every track and a humanised sampled kit. Open strums, rolled slowly (`spread`) or chucked (`x`). A banjo roll when the intensity is up or the mood is Bright (`or`). A borrowed minor four, `IVm`. |
| `blue-hour` | Chord symbols: `Imaj9`, `vim9`, `V13` and `VIM7b9`, the secondary dominant with a flat nine. Rootless voicings on the electric piano, comping a new rhythm each pass (a variation picked in turn, `cycle`). A walking bass past 0.5, a vibraphone solo in triplets, and a section that repeats while `intensity > 0.55 and pass < 2`. |
| `undertow` | Drum and bass at 172 in E Phrygian. A reese riff that slides (`~`) on a mono patch. A beat picked at random with weights (`[3, 1]`). `not mood is Calm`. A build and a drop. |
| `watchtower` | An orchestra over a sampled rock kit. Short strings whose level follows the intensity (`"level": "intensity"`). A hall group with a reverb, and an electric guitar through a distortion with a four-by-twelve cabinet, its drive following the intensity. Power chords, a tom fill each pass, and `VM`, harmonic minor's major five. |

The presets in the library, under the project's own, play on the keys with a click and come into the project with
**add**. The ones this project uses show **open**.

## The files

| File | What it is |
|---|---|
| `Songs/*.song.json` | The five songs (`lunelle-song/2`) |
| `Patches/*.patch.json` | The patches the songs play, and the kits' pieces' patches: each a preset's file as it is |
| `Kits/` | `general` and `rock`, sampled (CC0, each with its `LICENSE.txt` and `SOURCE.md`), and `breaks` and `breaks-deep`, made of patches |
| `lunelle-project.json` | The cues, `intensity` and `mood`, and the macros the patches turn: `bright`, `drive` and `sweep` |

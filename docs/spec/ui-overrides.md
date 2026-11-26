# Spec: patch overrides in the designer (UI, Phase 4.4)

Status: **implemented**. Builds on Lunelle's `docs/spec/overrides.md` and `transactions.md`.

## What it shows

A **derived patch** (a file with a `"base"`, such as one added from the library) opens in the designer as the whole
patch it resolves to, so every control works as it does for any patch. Above the designer:

- **Based on** names the base (`lunelle:synths/supersaw`).
- **Each change the file makes** is listed: a path set and its value (`filter.cutoff = 1800`), a path removed, or an
  item added (`operators[wobble] added`), each with **revert**.

A whole patch shows neither.

## What it does

- **Editing** a derived patch saves only what differs from its base (Lunelle's `OverrideDiff`): a value changed is a
  `set`, a property or item gone a `remove`, an item added an `insert`. A path the file set before stays set, even when
  it now equals the base's value (a set is pinned, overrides.md §4), until it is reverted.
- **Revert** takes that one change out of the file, so the base shows through there again, and the designer shows the
  patch as it now resolves. It is one undoable step.

## Still to come

- Marking each control the file overrides, in the designer itself, rather than in the list above it.
- A song's own patches (overrides.md §6), edited from the song's track.
- Kits: a derived kit is listed, but edited only as a file.

namespace BinaryVibrance.LunelleStudio;

// The editing model (docs/spec/transactions.md): where the workbench's files are a ProjectDocument, undo and redo are the
// document's, across every file the workbench writes; Ctrl+S saves; a step is sealed as the user turns to something else
public sealed partial class Workbench
{
	/// <summary>The document the workbench edits, when its <see cref="Files"/> are one; null writes straight to disk.</summary>
	public ProjectDocument? Document => Files as ProjectDocument;

	// The document's state on the view model, and what it recovered or found changed outside said once
	private void WatchDocument()
	{
		if (Document is not { } document)
			return;
		document.StateChanged += ShowDocument;
		ShowDocument();
		if (document.Recovered.Count > 0)
			ViewModel.Notice = string.Join(" ", document.Recovered);
	}

	private void ShowDocument()
	{
		if (Document is not { } document)
			return;
		ViewModel.IsDirty = document.IsDirty;
		if (document.ChangedOutside.Count > 0)
			ViewModel.Notice = $"Saving will replace {string.Join(", ", document.ChangedOutside)}, changed outside the studio.";
	}

	// What the editors hold, written to the document as one step of its own
	private void Flush()
	{
		_editor?.Save();
		SavePatch();
		Document?.Seal();
	}

	/// <summary>Ends the last step: the user turned to something else (another song, patch or kit).</summary>
	internal void Seal() => Document?.Seal();

	/// <summary>Saves the document (Ctrl+S): every edit written to the project's files. Says what it did.</summary>
	public void Save()
	{
		if (Document is not { } document)
			return;
		Flush();
		var edited = document.EditedFiles.Count;
		document.Save();
		ViewModel.Notice = "";
		ViewModel.BuildText = edited == 0 ? "nothing to save" : $"saved {edited} file{(edited == 1 ? "" : "s")} {DateTime.Now:HH:mm:ss}";
	}

	/// <summary>Drops every edit since the last save.</summary>
	public void Revert()
	{
		if (Document is not { } document)
			return;
		_editor?.Save();
		document.Revert();
		AfterDocumentStep();
		ViewModel.Notice = "";
		ViewModel.BuildText = "reverted to the last save";
	}

	// The document undone or redone: the open song's editor reads its file again, and the song is built again
	private bool DocumentStep(Func<ProjectDocument, bool> step)
	{
		if (Document is not { } document)
			return false;
		Flush();
		if (!step(document))
			return true;
		AfterDocumentStep();
		return true;
	}

	private void AfterDocumentStep()
	{
		_editor?.ReloadIfChanged();
		_editorNote = null;
		Rebuild(swap: true);
	}
}

using System.Runtime.InteropServices.JavaScript;

namespace BinaryVibrance.LunelleStudio.App.Web;

// The browser studio's reach into the page beyond Kaldra's and Sirrow's (wwwroot/lunelle-studio.js): the files kept in
// IndexedDB, and a file picked to import. Imported by Program before the studio starts.
internal static partial class StudioPage
{
	public const string Module = "lunelle-studio";

	[JSImport("filesOpen", Module)]
	[return: JSMarshalAs<JSType.Promise<JSType.Boolean>>]
	public static partial Task<bool> FilesOpen();

	[JSImport("filesLoaded", Module)] public static partial string FilesLoaded();
	[JSImport("filesTake", Module)] public static partial byte[] FilesTake(string path);
	[JSImport("filesPut", Module)] public static partial void FilesPut(string path, byte[] bytes);
	[JSImport("filesDelete", Module)] public static partial void FilesDelete(string path);

	[JSImport("pickFile", Module)]
	[return: JSMarshalAs<JSType.Promise<JSType.String>>]
	public static partial Task<string> PickFile(string accept, string question);

	[JSImport("pickedTake", Module)] public static partial byte[] PickedTake();
}

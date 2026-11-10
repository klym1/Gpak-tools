using System;
using System.IO;

namespace Viewer
{
    /// <summary>Remembers the game data opened last, so the viewer opens it again next time.</summary>
    public static class ViewerSettings
    {
        /// <summary>The file the path is kept in; in the user's application data folder by default.</summary>
        public static string File { get; set; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GpViewer", "last-game-data.txt");

        /// <summary>The game data opened last, or null if there is none or it is gone.</summary>
        public static string LastGameData
        {
            get
            {
                try
                {
                    var path = System.IO.File.ReadAllText(File).Trim();
                    return System.IO.File.Exists(path) || Directory.Exists(path) ? path : null;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    return null;
                }
            }
            set
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(File));
                    System.IO.File.WriteAllText(File, value ?? "");
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                }
            }
        }
    }
}

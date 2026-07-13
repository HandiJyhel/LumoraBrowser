using System.Runtime.InteropServices;

namespace Lumora.WinUI;

// Création/suppression de raccourcis Windows (.lnk) via l'interface COM Shell
// classique (IShellLinkW + IPersistFile). Pas de dépendance NuGet : ce sont des
// interfaces système stables, présentes sur toutes les versions de Windows.
internal static class ShellShortcut
{
    public static string StartMenuAppsFolder() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        "Programs", "Lumora Apps");

    public static string DesktopFolder() =>
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    public static void Create(string shortcutPath, string targetExe, string arguments, string? iconPath, string description)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(shortcutPath)!);

        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetExe);
        link.SetArguments(arguments);
        link.SetDescription(description);
        if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
        {
            link.SetIconLocation(iconPath, 0);
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, false);
        Marshal.ReleaseComObject(link);
    }

    public static void Delete(string shortcutPath)
    {
        try { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); } catch { }
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cchMaxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cchMaxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cchMaxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cchIconPath, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, Guid("0000010b-0000-0000-C000-000000000046"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        void IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}

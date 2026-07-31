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

    // appUserModelId (optionnel) : identifiant Windows distinct posé sur le
    // raccourci (propriété System.AppUserModel.ID). Sans lui, toutes les
    // fenêtres de Lumora.WinUI.exe (navigateur principal et applications web)
    // héritent du même identifiant par défaut dérivé de l'exécutable, et
    // Windows les regroupe sous une seule icône dans la barre des tâches /
    // épinglages au lieu de traiter chaque application comme distincte.
    public static void Create(string shortcutPath, string targetExe, string arguments, string? iconPath, string description, string? appUserModelId = null)
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

        if (!string.IsNullOrWhiteSpace(appUserModelId))
        {
            ApplyAppUserModelId(link, appUserModelId);
        }

        var file = (IPersistFile)link;
        file.Save(shortcutPath, false);
        Marshal.ReleaseComObject(link);
    }

    public static void Delete(string shortcutPath)
    {
        try { if (File.Exists(shortcutPath)) File.Delete(shortcutPath); } catch { }
    }

    // L'objet ShellLink implémente aussi IPropertyStore (depuis Windows Vista) :
    // c'est le canal standard pour poser une propriété comme AppUserModel.ID
    // sur un .lnk, distinct des champs classiques (chemin, arguments, icône...).
    private static void ApplyAppUserModelId(IShellLinkW link, string appUserModelId)
    {
        var store = (IPropertyStore)link;
        var pv = PropVariant.FromString(appUserModelId);
        try
        {
            store.SetValue(ref PkeyAppUserModelId, ref pv);
            store.Commit();
        }
        finally
        {
            PropVariantClear(ref pv);
        }
    }

    private static PROPERTYKEY PkeyAppUserModelId = new()
    {
        fmtid = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        pid = 5
    };

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [StructLayout(LayoutKind.Sequential)]
    private struct PROPERTYKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    // VT_LPWSTR uniquement (seul cas utilisé ici) : les 4 WORD d'en-tête
    // (vt + réservés) puis le pointeur de chaîne, disposition standard du
    // union PROPVARIANT côté natif pour ce type.
    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr pointerValue;

        private const ushort VT_LPWSTR = 31;

        public static PropVariant FromString(string value) => new()
        {
            vt = VT_LPWSTR,
            pointerValue = Marshal.StringToCoTaskMemUni(value)
        };
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PROPERTYKEY pkey);
        void GetValue(ref PROPERTYKEY key, out PropVariant pv);
        void SetValue(ref PROPERTYKEY key, ref PropVariant pv);
        void Commit();
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

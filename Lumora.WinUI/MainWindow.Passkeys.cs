using Microsoft.UI.Xaml;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Clés d'accès (Passkeys) ───────────────────────────────────────────────
    // Lumora ne stocke ni ne crée aucune clé WebAuthn/FIDO2 : c'est Windows Hello
    // (authenticator de la plateforme) qui gère la cérémonie et le secret. Ce
    // fichier ne tient qu'un journal local (origine + dates) pour savoir où
    // l'utilisateur a déjà une clé, affiché directement dans le Coffre à côté du
    // mot de passe et du TOTP du même site (0.93.46, unification Coffre V4) —
    // plus de panneau "Clés d'accès" séparé (voir MEMORY.md).

    private void LoadPasskeys()
    {
        _passkeys.Clear();
        if (_isGuestMode) return;
        try
        {
            var text = LumoraFile.TryReadAllText(_profile.PasskeysFile);
            if (text is null) return;
            foreach (var line in text.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var entry = PasskeyEntry.TryParse(trimmed);
                if (entry is not null) _passkeys.Add(entry);
            }
        }
        catch { }
    }

    private void SavePasskeys()
    {
        if (_isGuestMode) return;
        try
        {
            var text = string.Join('\n', _passkeys.Select(p => p.Serialize()));
            LumoraFile.WriteAllText(_profile.PasskeysFile, text);
        }
        catch { }
    }

    private void RecordPasskeyCreated(string origin)
    {
        var now      = DateTimeOffset.UtcNow;
        var existing = _passkeys.FirstOrDefault(p =>
            p.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            _passkeys[_passkeys.IndexOf(existing)] = existing with { LastUsedAt = now };
        else
            _passkeys.Add(new PasskeyEntry(origin, now, now));
        SavePasskeys();
    }

    private void RecordPasskeyUsed(string origin)
    {
        var existing = _passkeys.FirstOrDefault(p =>
            p.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            _passkeys[_passkeys.IndexOf(existing)] = existing with { LastUsedAt = DateTimeOffset.UtcNow };
            SavePasskeys();
        }
    }

    // Point d'entrée conservé pour le menu "Sécurité et données > Clés d'accès",
    // la palette de commandes et la tuile épinglable du menu Démarrer (aucun des
    // trois n'a besoin d'être modifié) : redirige simplement vers le Coffre, qui
    // affiche désormais la clé d'accès de chaque site dans sa fiche.
    private void PasskeysMenu_Click(object sender, RoutedEventArgs e) => VaultMenu_Click(sender, e);

    // Recherche par origine pour affichage dans une fiche du Coffre (détail
    // complet et accès rapide). Une seule entrée par origine (voir
    // RecordPasskeyCreated ci-dessus).
    private PasskeyEntry? PasskeyForOrigin(string origin) =>
        _passkeys.FirstOrDefault(p => p.Origin.Equals(origin, StringComparison.OrdinalIgnoreCase));

    // Retire l'entrée du JOURNAL LOCAL uniquement : la clé d'accès réelle reste
    // gérée par Windows Hello et n'est pas révoquée par cette action (voir
    // commentaire de fichier ci-dessus).
    private void DeletePasskeyEntry(PasskeyEntry entry)
    {
        _passkeys.Remove(entry);
        SavePasskeys();
    }

    private async Task OpenWindowsPasskeysSettingsAsync()
    {
        try
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-passkeys"));
        }
        catch
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:privacy-passkeys",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}

using System.Security.Cryptography;

namespace Lumora.WinUI;

// Entropie DPAPI propre a un profil sans mot de passe (2026-09-10, session
// "interface" / comptes sans mot de passe - demande explicite utilisateur :
// "je veux que leur acces soit le plus limite possible, meme pour
// l'utilisateur qui a cree le compte", specifiquement pour la RECUPERATION
// du profil - usage normal dans l'app inchange).
//
// Contexte : LumoraFile.cs chiffre tous les fichiers .lumora avec UNE SEULE
// entropie fixe ("Lumora.WinUI.v1"), ecrite en dur dans le code et partagee
// par TOUS les profils, avec ou sans mot de passe. Le depot etant destine a
// devenir public, cette "cle" est en pratique publique des qu'on lit le
// code - elle protege contre un curieux qui fouille dans l'Explorateur, mais
// pas contre quelqu'un qui saurait qu'il faut la reutiliser. Cette classe
// ajoute une entropie SUPPLEMENTAIRE, propre a chaque profil sans mot de
// passe (32 octets aleatoires, generes une fois et geres ici), combinee a
// l'entropie fixe par LumoraFile (voir LumoraFile.SetProfileEntropy) - les
// profils AVEC mot de passe n'y touchent jamais (aucun appelant ne le fait
// pour eux, voir MainWindow.xaml.cs).
//
// Le fichier lui-meme n'est PAS chiffre (dependance circulaire avec
// LumoraFile sinon) : un attaquant avec acces brut au disque peut le lire
// tout comme les autres fichiers .lumora chiffres a cote - ca n'est pas le
// but. Le but est de ne plus offrir une cle UNIQUE valable pour tous les
// profils sans mot de passe de tous les utilisateurs de Lumora, connue de
// quiconque lit le depot public.
internal static class ProfileEntropyStore
{
    private const int EntropyLengthBytes = 32;

    // Charge l'entropie du profil, la cree si absente (couvre aussi bien un
    // nouveau profil sans mot de passe qu'un profil sans mot de passe deja
    // existant avant l'ajout de cette fonctionnalite - migration transparente,
    // pas d'etape a part). Retourne un tableau vide en cas d'echec (disque
    // en lecture seule, etc.) : LumoraFile retombe alors sur l'entropie fixe
    // seule, comportement identique a avant cette fonctionnalite plutot
    // qu'une exception qui casserait le demarrage.
    public static byte[] LoadOrCreate(LumoraProfilePaths profile)
    {
        try
        {
            if (File.Exists(profile.EntropyFile))
            {
                var existing = TryReadBase64(profile.EntropyFile);
                if (existing is { Length: > 0 })
                {
                    return existing;
                }
                // Fichier present mais illisible/corrompu : on en regenere un
                // plutot que de rester bloque avec une entropie vide en
                // permanence (voir TryReadBase64 - n'arrive normalement pas
                // en usage reel, seulement sur un fichier altere a la main).
            }

            var fresh = RandomNumberGenerator.GetBytes(EntropyLengthBytes);
            var dir = Path.GetDirectoryName(profile.EntropyFile);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(profile.EntropyFile, Convert.ToBase64String(fresh));
            return fresh;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    private static byte[]? TryReadBase64(string path)
    {
        try
        {
            return Convert.FromBase64String(File.ReadAllText(path).Trim());
        }
        catch
        {
            return null;
        }
    }
}

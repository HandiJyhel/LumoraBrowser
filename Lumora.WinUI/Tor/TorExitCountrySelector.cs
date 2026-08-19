using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Lumora.WinUI.Tor;

// Pays de sortie propose par pays (2 lettres ISO) pour un nom affiche a
// l'utilisateur - jamais le noeud precis, seulement le pays via lequel Tor
// doit essayer de faire sortir le trafic (option torrc ExitNodes).
internal readonly record struct TorExitCountryOption(string Code, string DisplayName);

// Reglage "pays de sortie Tor", reserve a la fenetre Incognito quand Tor est
// actif (voir LumoraIncognitoWindow, bouton IncognitoExitCountryButton).
// Fichier separe de TorProcessManager pour ne pas alourdir son cycle de vie
// process/bootstrap deja dense : ne fait qu'une chose, reconfigurer
// ExitNodes sur un control port deja authentifie, puis rendre la main a
// TorProcessManager.RequestNewCircuitAsync pour le vrai changement de
// circuit (reutilise son cooldown NEWNYM, pas duplique ici).
//
// Choix assume et explique a l'utilisateur (voir menu) : preference souple,
// jamais StrictNodes. Si aucun noeud du pays choisi n'est disponible, Tor se
// rabat sur un autre plutot que de faire echouer la connexion - un pays
// impose reduit deja l'ensemble d'anonymat (moins de monde partage le meme
// petit groupe de noeuds), StrictNodes l'aurait reduit plus encore.
internal static class TorExitCountrySelector
{
    // Liste fixe et volontairement courte (2026-08-18, choix utilisateur) :
    // des pays avec une presence connue et durable de noeuds de sortie Tor a
    // bonne bande passante. Pas les Pays-Bas par defaut : c'est justement le
    // pays sur lequel les utilisateurs retombent presque toujours sans rien
    // choisir (le reseau Tor y concentre une part disproportionnee de ses
    // noeuds de sortie), donc le proposer ici n'apporterait aucun choix reel.
    public static readonly IReadOnlyList<TorExitCountryOption> Options = new[]
    {
        new TorExitCountryOption("FR", "France"),
        new TorExitCountryOption("DE", "Allemagne"),
        new TorExitCountryOption("CH", "Suisse"),
        new TorExitCountryOption("SE", "Suède"),
        new TorExitCountryOption("GB", "Royaume-Uni"),
        new TorExitCountryOption("US", "États-Unis"),
        new TorExitCountryOption("CA", "Canada"),
    };

    // Applique le pays choisi (ou le retire si countryCode est null - retour
    // a "Automatique") puis demande un nouveau circuit pour que le prochain
    // chargement de page emprunte reellement le nouveau reglage. N'ecrit
    // jamais rien sur disque : reglage tenu uniquement en memoire process,
    // reinitialise a "Automatique" a chaque nouvelle fenetre Incognito
    // (coherent avec le principe de session ephemere deja applique ailleurs
    // dans cette fenetre).
    public static async Task<(bool Success, string Message)> ApplyAsync(
        TorProcessManager tor, string? countryCode, CancellationToken cancellationToken = default)
    {
        if (tor.State != TorEngineState.Connected)
        {
            return (false, "Le moteur Tor n'est pas connecte.");
        }

        var cookiePath = tor.CookieAuthPath;
        if (cookiePath is null || !File.Exists(cookiePath))
        {
            return (false, "Authentification du contrôle Tor indisponible.");
        }

        // Timeout local (voir meme correctif dans TorProcessManager.RequestNewCircuitAsync,
        // audit du 2026-08-19) : sans lui, un tor.exe qui cesse de repondre laisserait
        // le bouton "Sortie : ..." desactive indefiniment.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));
        var linkedToken = timeoutCts.Token;

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, tor.ControlPort, linkedToken);
            await using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };
            using var reader = new StreamReader(stream, Encoding.ASCII);

            var cookieBytes = await File.ReadAllBytesAsync(cookiePath, linkedToken);
            var cookieHex = Convert.ToHexString(cookieBytes);

            await writer.WriteLineAsync($"AUTHENTICATE {cookieHex}");
            var authResponse = await reader.ReadLineAsync(linkedToken);
            if (authResponse is null || !authResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return (false, "Authentification aupres du controle Tor refusee.");
            }

            // Chaine vide = RESETCONF (retour a "Automatique", laisse Tor
            // choisir librement) ; sinon SETCONF avec le pays demande.
            var configCommand = string.IsNullOrEmpty(countryCode)
                ? "RESETCONF ExitNodes"
                : $"SETCONF ExitNodes=\"{{{countryCode}}}\"";

            await writer.WriteLineAsync(configCommand);
            var configResponse = await reader.ReadLineAsync(linkedToken);
            if (configResponse is null || !configResponse.StartsWith("250", StringComparison.Ordinal))
            {
                return (false, "Le moteur Tor a refusé le changement de pays de sortie.");
            }

            await writer.WriteLineAsync("QUIT");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (false, "Le contrôle Tor ne répond pas (délai dépassé).");
        }
        catch (Exception ex)
        {
            return (false, $"Changement de pays de sortie impossible : {ex.Message}");
        }

        // Le nouveau reglage ExitNodes ne s'applique qu'aux prochains
        // circuits : sans ce SIGNAL NEWNYM, les onglets garderaient leur
        // circuit (et donc leur pays) actuel jusqu'a fermeture naturelle.
        // NEWNYM partage son cooldown avec le bouton "Nouveau circuit"
        // (TorProcessManager) et peut donc echouer independamment du
        // SETCONF/RESETCONF ci-dessus, qui lui a deja reellement pris effet
        // sur le process Tor a ce stade. Bug reel du 2026-08-19 : faire
        // dependre le succes global de ApplyAsync du seul NEWNYM faisait que
        // l'appelant (IncognitoExitCountryOption_Checked) recochait l'ancien
        // pays alors que la config Tor avait deja change - le menu mentait
        // sur le pays reellement actif. Le changement de pays est desormais
        // TOUJOURS rapporte comme reussi une fois le SETCONF/RESETCONF
        // accepte, meme si le nouveau circuit est differe par le cooldown.
        var (circuitRenewed, circuitMessage) = await tor.RequestNewCircuitAsync(cancellationToken);
        return circuitRenewed
            ? (true, circuitMessage)
            : (true, $"Pays de sortie changé (nouveau circuit différé : {circuitMessage})");
    }
}

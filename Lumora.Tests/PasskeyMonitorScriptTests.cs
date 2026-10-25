using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

public class PasskeyMonitorScriptTests
{
    [Fact]
    public void Un_appel_publicKey_ne_resout_jamais_a_null()
    {
        // Le bug corrigé : `Promise.resolve(null)` n'est pas un résultat WebAuthn
        // valide, la connexion PlayStation affichait « Une erreur s'est produite ».
        Assert.DoesNotContain("Promise.resolve(null)", PasskeyMonitorScript.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Un_appel_explicite_est_refuse_comme_une_annulation_utilisateur()
    {
        Assert.Contains("'NotAllowedError'", PasskeyMonitorScript.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void La_saisie_automatique_reste_en_attente_et_respecte_AbortSignal()
    {
        Assert.Contains("mediation==='conditional'", PasskeyMonitorScript.Source, StringComparison.Ordinal);
        Assert.Contains("signal.addEventListener('abort'", PasskeyMonitorScript.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_appels_sans_publicKey_restent_delegues_au_navigateur()
    {
        Assert.Contains("_origGet(options)", PasskeyMonitorScript.Source, StringComparison.Ordinal);
        Assert.Contains("_origCreate(options)", PasskeyMonitorScript.Source, StringComparison.Ordinal);
    }
}

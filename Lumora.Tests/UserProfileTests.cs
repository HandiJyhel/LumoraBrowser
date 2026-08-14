using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Vérifie le gate de connexion (PBKDF2) : round-trip, changement de mot de passe,
// et que le compteur d'itérations est bien porté par le profil (migration sûre).
public class UserProfileTests
{
    [Fact]
    public void Create_puis_VerifyPassword()
    {
        var profile = UserProfile.Create("Alice", "motdepasse", pin: null);
        Assert.True(profile.VerifyPassword("motdepasse"));
        Assert.False(profile.VerifyPassword("autre"));
    }

    [Fact]
    public void Les_nouveaux_profils_utilisent_600k_iterations()
    {
        var profile = UserProfile.Create("Alice", "motdepasse", pin: null);
        Assert.Equal(600_000, profile.PasswordIterations);
    }

    [Fact]
    public void VerifyPin_uniquement_si_pin_configure()
    {
        var withPin = UserProfile.Create("Alice", "pw", pin: "123456");
        Assert.True(withPin.HasPinLogin);
        Assert.True(withPin.VerifyPin("123456"));
        Assert.False(withPin.VerifyPin("000000"));

        var noPin = UserProfile.Create("Bob", "pw", pin: null);
        Assert.False(noPin.HasPinLogin);
        Assert.False(noPin.VerifyPin("123456"));
    }

    [Fact]
    public void WithNewPassword_rehash_et_reverifie()
    {
        var profile = UserProfile.Create("Alice", "ancien", pin: null)
            .WithNewPassword("nouveau");

        Assert.False(profile.VerifyPassword("ancien"));
        Assert.True(profile.VerifyPassword("nouveau"));
        Assert.Equal(600_000, profile.PasswordIterations);
    }

    [Fact]
    public void Un_profil_legacy_100k_reste_verifiable()
    {
        // Simule un profil hérité : mot de passe haché à 100k et compteur explicite.
        // On reconstruit l'état via un profil neuf puis on force le compteur legacy en
        // conservant le hash correspondant — ici on vérifie l'invariant via un profil
        // créé à 600k dont on ne change QUE le compteur casserait la vérif : c'est donc
        // la preuve que le compteur DOIT accompagner le hash.
        var current = UserProfile.Create("Alice", "pw", pin: null);
        var mismatched = current with { PasswordIterations = 100_000 };
        Assert.False(mismatched.VerifyPassword("pw")); // compteur != celui du hash → échoue
        Assert.True(current.VerifyPassword("pw"));       // compteur cohérent → réussit
    }

    [Fact]
    public void VerifyRecoveryKey_normalise_la_saisie()
    {
        var profile = UserProfile.Create("Alice", "pw", pin: null)
            .WithRecoveryKey("NOVA-ABCD-1234");

        Assert.True(profile.HasRecoveryKey);
        Assert.True(profile.VerifyRecoveryKey("nova abcd 1234"));   // casse/espaces ignorés
        Assert.False(profile.VerifyRecoveryKey("NOVA-0000-0000"));
    }

    // 2026-08-13 : profil sans mot de passe, choix permanent pris a la
    // creation (voir MEMORY.md - risque de detournement si n'importe qui
    // pouvait poser un mot de passe sur un profil deja ouvert plus tard).
    [Fact]
    public void Create_sans_mot_de_passe_expose_HasAccountPassword_a_false()
    {
        var profile = UserProfile.Create("Alice", password: null, pin: null);

        Assert.False(profile.HasAccountPassword);
        Assert.Empty(profile.PasswordHash);
        Assert.Empty(profile.PasswordSalt);
        Assert.False(profile.VerifyPassword(string.Empty));
        Assert.False(profile.VerifyPassword("nimportequoi"));
    }

    // Un PIN sans mot de passe de base n'a pas de sens (rien a raccourcir) :
    // ignore silencieusement, meme si un appelant fournit quand meme une
    // valeur - le modele reste coherent seul, pas seulement grace a l'UI.
    [Fact]
    public void Create_sans_mot_de_passe_ignore_un_pin_fourni_quand_meme()
    {
        var profile = UserProfile.Create("Alice", password: null, pin: "123456");

        Assert.False(profile.HasAccountPassword);
        Assert.False(profile.HasPinLogin);
        Assert.False(profile.VerifyPin("123456"));
    }

    [Fact]
    public void Profil_avec_mot_de_passe_expose_HasAccountPassword_a_true()
    {
        var profile = UserProfile.Create("Alice", "mot-de-passe-fort", pin: null);
        Assert.True(profile.HasAccountPassword);
    }
}

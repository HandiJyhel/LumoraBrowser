using Lumora.WinUI.Credentials;
using Xunit;

namespace Lumora.Tests;

public class PasswordGeneratorTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(64)]
    public void Generate_respecte_la_longueur_demandee(int length)
    {
        Assert.Equal(length, PasswordGenerator.Generate(length).Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Generate_borne_le_minimum(int tooShort)
    {
        Assert.Equal(PasswordGenerator.MinLength, PasswordGenerator.Generate(tooShort).Length);
    }

    [Fact]
    public void Generate_borne_le_maximum()
    {
        Assert.Equal(PasswordGenerator.MaxLength, PasswordGenerator.Generate(9999).Length);
    }

    [Fact]
    public void Generate_contient_au_moins_une_minuscule_majuscule_et_chiffre()
    {
        for (var i = 0; i < 200; i++)
        {
            var pw = PasswordGenerator.Generate(12, useSymbols: false);
            Assert.Contains(pw, char.IsLower);
            Assert.Contains(pw, char.IsUpper);
            Assert.Contains(pw, char.IsDigit);
        }
    }

    [Fact]
    public void Generate_sans_symboles_reste_alphanumerique()
    {
        var pw = PasswordGenerator.Generate(40, useSymbols: false);
        Assert.All(pw, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    [Fact]
    public void Generate_evite_les_caracteres_ambigus()
    {
        for (var i = 0; i < 100; i++)
        {
            var pw = PasswordGenerator.Generate(64);
            Assert.DoesNotContain('0', pw);
            Assert.DoesNotContain('O', pw);
            Assert.DoesNotContain('1', pw);
            Assert.DoesNotContain('l', pw);
            Assert.DoesNotContain('I', pw);
        }
    }

    [Fact]
    public void Generate_produit_des_valeurs_distinctes()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 500; i++)
        {
            set.Add(PasswordGenerator.Generate(20));
        }

        // Collision quasi impossible sur un espace aussi large : on tolère de rares doublons.
        Assert.True(set.Count > 490);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(10)]
    public void GeneratePassphrase_respecte_le_nombre_de_mots_demande(int wordCount)
    {
        var phrase = PasswordGenerator.GeneratePassphrase(wordCount);
        Assert.Equal(wordCount, phrase.Split('-').Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void GeneratePassphrase_borne_le_minimum(int tooFew)
    {
        var phrase = PasswordGenerator.GeneratePassphrase(tooFew);
        Assert.Equal(PasswordGenerator.MinPassphraseWords, phrase.Split('-').Length);
    }

    [Fact]
    public void GeneratePassphrase_borne_le_maximum()
    {
        var phrase = PasswordGenerator.GeneratePassphrase(999);
        Assert.Equal(PasswordGenerator.MaxPassphraseWords, phrase.Split('-').Length);
    }

    [Fact]
    public void GeneratePassphrase_utilise_le_separateur_fourni()
    {
        var phrase = PasswordGenerator.GeneratePassphrase(4, separator: " ");
        Assert.Equal(4, phrase.Split(' ').Length);
        Assert.DoesNotContain('-', phrase);
    }

    [Fact]
    public void GeneratePassphrase_ne_contient_que_des_mots_minuscules()
    {
        var phrase = PasswordGenerator.GeneratePassphrase(6);
        Assert.All(phrase, c => Assert.True(char.IsLower(c) || c == '-'));
    }

    [Fact]
    public void GeneratePassphrase_produit_des_valeurs_distinctes()
    {
        var set = new HashSet<string>();
        for (var i = 0; i < 300; i++)
        {
            set.Add(PasswordGenerator.GeneratePassphrase(6));
        }

        // ~270 mots ^ 6 : espace largement suffisant pour eviter des collisions frequentes.
        Assert.True(set.Count > 295);
    }
}

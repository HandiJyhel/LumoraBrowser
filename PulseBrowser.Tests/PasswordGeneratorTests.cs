using PulseBrowser.WinUI.Credentials;
using Xunit;

namespace PulseBrowser.Tests;

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
}

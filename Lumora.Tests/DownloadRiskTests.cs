using Lumora.WinUI;
using Xunit;

namespace Lumora.Tests;

// Audit de sécurité 2026-09-24 : confirmation avant d'ouvrir un téléchargement
// capable d'exécuter du code.
public class DownloadRiskTests
{
    [Theory]
    [InlineData(@"C:\Téléchargements\setup.exe")]
    [InlineData(@"C:\Téléchargements\facture.pdf.exe")]
    [InlineData(@"C:\Téléchargements\SCRIPT.PS1")]
    [InlineData(@"C:\Téléchargements\lettre.vbs")]
    [InlineData(@"C:\Téléchargements\aide.chm")]
    [InlineData(@"C:\Téléchargements\raccourci.lnk")]
    [InlineData(@"C:\Téléchargements\page.hta")]
    [InlineData(@"C:\Téléchargements\image.iso")]
    [InlineData(@"C:\Téléchargementsapport.msc")]
    [InlineData(@"C:\Téléchargements\mise-a-jour.appinstaller")]
    [InlineData(@"C:\Téléchargementsureau.rdp")]
    [InlineData(@"C:\Téléchargementsecherche.search-ms")]
    [InlineData(@"C:\Téléchargements\setup.exe.")]
    [InlineData(@"C:\Téléchargements\setup.exe ")]
    public void Types_executables_dangereux(string path) =>
        Assert.True(DownloadRisk.IsDangerous(path));

    [Theory]
    [InlineData(@"C:\Téléchargements\facture.pdf")]
    [InlineData(@"C:\Téléchargements\photo.jpg")]
    [InlineData(@"C:\Téléchargements\archive.zip")]
    [InlineData(@"C:\Téléchargements\film.mp4")]
    [InlineData(@"C:\Téléchargements\sans-extension")]
    [InlineData("")]
    [InlineData(null)]
    public void Types_ordinaires_sans_confirmation(string? path) =>
        Assert.False(DownloadRisk.IsDangerous(path));
}

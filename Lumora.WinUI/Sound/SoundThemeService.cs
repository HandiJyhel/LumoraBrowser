using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lumora.WinUI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace Lumora.WinUI.Sound;

// "Sons & ambiance" (chantier "Ajustement n2", 2026-09-11, demande explicite
// utilisateur - propose/valide avant integration, sources documentees dans
// Assets/Sound/*/LICENSE-*.txt) : un pack de sons d'interface (clic,
// bascule) + une ambiance de fond en boucle (jamais de la musique -
// exigence explicite), tous les deux propres a ce profil.
//
// ElementSoundPlayer (voir VisualFlashService, Accessibility/) ne permet PAS
// de remplacer les sons integres de WinUI par des fichiers a nous (pas de
// SetCustomEffect dans le Windows App SDK, contrairement a l'UWP historique)
// - donc plutot que d'aller cabler chaque bouton individuellement, ce
// service ecoute une seule fois le PointerReleased au niveau de la racine
// de la fenetre (evenement routé qui remonte depuis n'importe quel controle)
// et determine depuis la ou l'evenement est parti s'il s'agit d'un clic ou
// d'une bascule - un seul point d'ecoute pour toute l'app.
internal sealed class SoundThemeService : IDisposable
{
    private static readonly string[] KnownEffectPacks = ["base", "doux", "mecanique", "arcade"];
    private static readonly string[] KnownAmbiances = ["cascade", "mer", "pluie"];

    private readonly string _assetsRoot = Path.Combine(AppContext.BaseDirectory, "Assets", "Sound");
    private readonly MediaPlayer _clickPlayer = new();
    private readonly MediaPlayer _togglePlayer = new();
    private readonly MediaPlayer _ambiancePlayer = new() { IsLoopingEnabled = true };
    // Zones exclues de l'ecoute automatique (les selecteurs de pack/ambiance
    // des Reglages eux-memes) - sinon un clic sur "Arcade" pour le choisir
    // sonorise avec l'ANCIEN pack (le PointerReleased remonte et joue avant
    // que SelectionChanged n'ait mis a jour _effectsPack), ce qui donnait
    // l'impression que "c'est toujours le meme son" quel que soit le pack
    // clique (retour utilisateur reel, 2026-09-11). Ces zones-la utilisent
    // PreviewClick/PreviewToggle explicitement, avec le pack DEJA a jour.
    private readonly List<DependencyObject> _excludedRoots = [];
    private string _effectsPack = "base";
    private bool _disposed;

    public bool Enabled { get; private set; }
    public string CurrentAmbianceId { get; private set; } = "none";

    public SoundThemeService()
    {
        // Diagnostic reel (2026-09-11, apres 2 diagnostics ecrits sans
        // verification qui ne se sont pas confirmes) : plutot que de
        // supposer le comportement d'AutoPlay ou du decodeur, on trace ce
        // que MediaPlayer rapporte reellement pour chaque lecture.
        void Wire(MediaPlayer player, string label)
        {
            player.MediaOpened += (_, _) => WinUiRuntimeTrace.Write($"SoundThemeService[{label}]: MediaOpened, duree={player.PlaybackSession.NaturalDuration}");
            player.MediaFailed += (_, args) => WinUiRuntimeTrace.Write($"SoundThemeService[{label}]: MediaFailed error={args.Error} message={args.ErrorMessage}");
            player.MediaEnded += (_, _) => WinUiRuntimeTrace.Write($"SoundThemeService[{label}]: MediaEnded");
            player.CurrentStateChanged += (_, _) => WinUiRuntimeTrace.Write($"SoundThemeService[{label}]: state={player.CurrentState}");
        }
        Wire(_clickPlayer, "click");
        Wire(_togglePlayer, "toggle");
        Wire(_ambiancePlayer, "ambiance");
    }

    // A appeler une seule fois (depuis MainWindow, apres InitializeComponent)
    // avec l'element racine de la fenetre : abonne l'ecoute app-wide des
    // clics/bascules pour les sons d'interface.
    public void AttachTo(UIElement root)
    {
        root.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler(OnRootPointerReleased), true);
    }

    // A appeler pour chaque conteneur dont les clics ne doivent jamais
    // declencher le son automatique (ex. les selecteurs de pack/ambiance
    // des Reglages, qui appellent PreviewClick/PreviewToggle eux-memes avec
    // la valeur a jour).
    public void ExcludeFromAutoClickSound(DependencyObject root) => _excludedRoots.Add(root);

    // Joue explicitement le son du pack COURANT - a utiliser juste apres
    // SetEffectsPack pour donner un retour immediat et correct quand
    // l'utilisateur choisit un pack, plutot que de laisser l'ecoute
    // automatique jouer l'ancien pack (voir _excludedRoots ci-dessus).
    // Fonctionne meme si Enabled=false : on doit pouvoir ecouter un pack
    // avant d'activer la fonctionnalite.
    public void PreviewClick() => PlayEffect(_clickPlayer, "click.wav");
    public void PreviewToggle() => PlayEffect(_togglePlayer, "toggle.wav");

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if (!enabled)
        {
            _ambiancePlayer.Pause();
        }
        else if (CurrentAmbianceId != "none")
        {
            _ambiancePlayer.Play();
        }
    }

    public void SetEffectsPack(string packId)
    {
        _effectsPack = KnownEffectPacks.Contains(packId) ? packId : "base";
    }

    // ambianceId "none" coupe l'ambiance. Volume 0.0-1.0.
    public void SetAmbiance(string ambianceId, double volume)
    {
        CurrentAmbianceId = KnownAmbiances.Contains(ambianceId) ? ambianceId : "none";
        _ambiancePlayer.Volume = Math.Clamp(volume, 0.0, 1.0);

        if (CurrentAmbianceId == "none")
        {
            _ambiancePlayer.Pause();
            _ambiancePlayer.Source = null;
            return;
        }

        var path = Path.Combine(_assetsRoot, "Ambiance", $"{CurrentAmbianceId}.mp3");
        if (!File.Exists(path))
        {
            return;
        }

        _ambiancePlayer.Source = MediaSource.CreateFromUri(new Uri(path));
        if (Enabled)
        {
            _ambiancePlayer.Play();
        }
    }

    public void SetAmbianceVolume(double volume)
    {
        _ambiancePlayer.Volume = Math.Clamp(volume, 0.0, 1.0);
    }

    private void OnRootPointerReleased(object sender, PointerRoutedEventArgs e)
        => HandlePotentialClick(e.OriginalSource as DependencyObject);

    // Point d'entree teste directement par le self-test (2026-09-11) :
    // reproduit exactement ce qu'un vrai clic declenche, sans avoir besoin
    // d'un evenement pointeur synthetique (impossible a fabriquer simplement
    // depuis du code) - en lui passant un vrai controle de l'arbre XAML, ca
    // verifie la meme logique (garde Enabled, zones exclues, detection de
    // type) qu'un clic reel emprunterait.
    internal void SimulateInteractionForSelfTest(DependencyObject source) => HandlePotentialClick(source);

    private void HandlePotentialClick(DependencyObject? node)
    {
        if (!Enabled)
        {
            WinUiRuntimeTrace.Write("SoundThemeService.HandlePotentialClick: ignore, Enabled=false");
            return;
        }

        // Remonte l'arbre visuel depuis la source exacte du clic (souvent un
        // TextBlock/Path interne, pas le controle lui-meme) jusqu'a trouver
        // un controle qu'on sait sonoriser - meme logique que le reperage
        // d'elements deja documente pour l'automation de cette app.
        while (node is not null)
        {
            if (_excludedRoots.Contains(node))
            {
                WinUiRuntimeTrace.Write($"SoundThemeService.HandlePotentialClick: zone exclue ({node.GetType().Name})");
                return;
            }

            switch (node)
            {
                // ToggleSwitch ne derive pas de ButtonBase (contrairement a
                // RadioButton/CheckBox, tous deux ToggleButton -> ButtonBase) -
                // seul vrai "interrupteur" de l'app, le reste (boutons,
                // radios, cases a cocher) sonne comme un clic.
                case ToggleSwitch:
                    WinUiRuntimeTrace.Write("SoundThemeService.HandlePotentialClick: ToggleSwitch detecte");
                    PlayEffect(_togglePlayer, "toggle.wav");
                    return;
                case ButtonBase:
                    WinUiRuntimeTrace.Write($"SoundThemeService.HandlePotentialClick: ButtonBase detecte ({node.GetType().Name})");
                    PlayEffect(_clickPlayer, "click.wav");
                    return;
            }
            node = VisualTreeHelper.GetParent(node);
        }

        WinUiRuntimeTrace.Write("SoundThemeService.HandlePotentialClick: aucun controle sonorisable trouve en remontant l'arbre");
    }

    private void PlayEffect(MediaPlayer player, string fileName)
    {
        var path = Path.Combine(_assetsRoot, "Effects", _effectsPack, fileName);
        if (!File.Exists(path))
        {
            WinUiRuntimeTrace.Write($"SoundThemeService.PlayEffect: fichier introuvable {path}");
            return;
        }

        // Recree la source a chaque appel plutot que de rejouer la meme
        // instance, pour des clics rapprochés qui doivent sonner pleinement
        // a chaque fois. Appel explicite de Play() (2026-09-11, correction) :
        // ne plus supposer qu'affecter Source suffit (AutoPlay) - un appel
        // explicite est correct quelle que soit sa valeur reelle par defaut.
        WinUiRuntimeTrace.Write($"SoundThemeService.PlayEffect: lecture de {path}");
        player.Source = MediaSource.CreateFromUri(new Uri(path));
        player.Play();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _clickPlayer.Dispose();
        _togglePlayer.Dispose();
        _ambiancePlayer.Dispose();
    }
}

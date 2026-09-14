using Microsoft.UI.Xaml;

namespace Lumora.WinUI.Accessibility;

// Rappel de pause (accessibilite, chantier "Ajustement n2" 2026-09-11) :
// minuteur repete tant que Lumora tourne, extrait de MainWindow pour ne pas
// alourdir davantage ses fichiers deja tres charges - pure logique de
// minuterie, aucune dependance a l'UI. MainWindow se contente de creer une
// instance, de l'alimenter via Restart(minutes) a chaque chargement/
// changement de reglage, et de s'abonner a ReminderDue pour decider quoi
// afficher (texte de statut, flash visuel...).
internal sealed class BreakReminderService : IDisposable
{
    private readonly DispatcherTimer _timer = new();
    private bool _disposed;

    public event Action? ReminderDue;

    public BreakReminderService()
    {
        _timer.Tick += (_, _) => ReminderDue?.Invoke();
    }

    // 0 (ou moins) desactive le rappel. Sans effet si la valeur ne change
    // pas depuis le dernier appel, hormis le redemarrage implicite du
    // minuteur (comportement voulu : un changement de reglage doit repartir
    // de zero, pas continuer a decompter sur l'ancienne duree).
    public void Restart(int minutes)
    {
        _timer.Stop();
        if (minutes <= 0)
        {
            return;
        }

        _timer.Interval = TimeSpan.FromMinutes(minutes);
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }
}

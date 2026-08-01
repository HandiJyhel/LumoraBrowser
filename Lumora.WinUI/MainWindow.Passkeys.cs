using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    // ── Clés d'accès (Passkeys) ───────────────────────────────────────────────

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

    private void PasskeysMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            UpdateStatusText("Clés d'accès indisponibles en mode invité.");
            return;
        }
        ShowPanel(PasskeysPanel, "Clés d'accès (Passkeys)");
        RenderPasskeysPanel();
    }

    private async void PasskeysWindowsSettingsButton_Click(object sender, RoutedEventArgs e)
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

    private void RenderPasskeysPanel()
    {
        PasskeysPanelItems.Children.Clear();

        if (_passkeys.Count == 0)
        {
            PasskeysPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucune clé d'accès enregistrée pour l'instant.",
                Opacity = 0.65,
                FontSize = AccessibilitySecondaryFontSize(),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var entry in _passkeys.OrderByDescending(p => p.LastUsedAt))
        {
            PasskeysPanelItems.Children.Add(BuildPasskeyCard(entry));
        }
    }

    private UIElement BuildPasskeyCard(PasskeyEntry entry)
    {
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var originBlock = new TextBlock
        {
            Text = entry.Origin,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize(),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(originBlock, 0);
        headerGrid.Children.Add(originBlock);

        var deleteBtn = new Button
        {
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center
        };
        ApplyNovaControlAccessibility(deleteBtn, $"Supprimer la clé d'accès pour {entry.Origin}");
        deleteBtn.Content = new FontIcon
        {
            Glyph = "\uE74D",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14
        };
        ToolTipService.SetToolTip(deleteBtn, "Supprimer cette clé d'accès");
        var captured = entry;
        deleteBtn.Click += (_, _) => PasskeyDeleteButton_Click(captured);
        Grid.SetColumn(deleteBtn, 1);
        headerGrid.Children.Add(deleteBtn);

        var createdBlock = new TextBlock
        {
            Text = $"Créée le {entry.CreatedAt.LocalDateTime:dd/MM/yyyy}",
            Opacity = 0.65,
            FontSize = AccessibilitySecondaryFontSize(),
            Margin = new Thickness(0, 4, 0, 0)
        };

        var usedBlock = new TextBlock
        {
            Text = $"Dernière utilisation : {entry.LastUsedAt.LocalDateTime:dd/MM/yyyy HH:mm}",
            Opacity = 0.5,
            FontSize = AccessibilitySecondaryFontSize(),
            Margin = new Thickness(0, 2, 0, 0)
        };

        var body = new StackPanel();
        body.Children.Add(headerGrid);
        body.Children.Add(createdBlock);
        body.Children.Add(usedBlock);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private void PasskeyDeleteButton_Click(PasskeyEntry entry)
    {
        _passkeys.Remove(entry);
        SavePasskeys();
        RenderPasskeysPanel();
        UpdateStatusText($"Clé d'accès supprimée : {entry.Origin}");
    }
}

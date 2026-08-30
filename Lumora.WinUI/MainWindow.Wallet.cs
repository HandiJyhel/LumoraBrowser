using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Lumora.WinUI;

// Portefeuille : moyens de paiement stockés UNIQUEMENT dans vault.lumora.
// Non-traçabilité par conception : jamais de pré-remplissage silencieux (un
// script de page ne peut pas sonder le portefeuille), remplissage sur clic
// explicite uniquement, CVV jamais stocké, autofill Chromium désactivé.
public sealed partial class MainWindow
{
    // ── Panneau Portefeuille ──────────────────────────────────────────────────

    private async void WalletMenu_Click(object sender, RoutedEventArgs e)
    {
        if (_isGuestMode)
        {
            UpdateStatusText("Portefeuille indisponible en mode invité.");
            return;
        }

        // Même barrière que le gestionnaire de mots de passe : re-demande le PIN
        // ou le mot de passe à chaque ouverture, zone la plus sensible.
        if (!await RequireVaultAccessAsync())
        {
            UpdateStatusText("Accès au portefeuille refusé : code incorrect ou annulé.", notificationKind: Microsoft.UI.Xaml.Automation.Peers.AutomationNotificationKind.ActionAborted);
            return;
        }

        ShowPanel(WalletPanel, "Portefeuille");
        RefreshWalletPanel();
    }

    private void RefreshWalletPanel()
    {
        WalletPanelItems.Children.Clear();

        var cards = _vault.ListCards();
        if (cards.Count == 0)
        {
            WalletPanelItems.Children.Add(new TextBlock
            {
                Text = "Aucune carte dans le portefeuille.",
                Opacity = 0.65,
                FontSize = AccessibilitySecondaryFontSize(),
                Margin = new Thickness(0, 8, 0, 0)
            });
            return;
        }

        foreach (var card in cards.OrderByDescending(c => c.UpdatedAt))
        {
            WalletPanelItems.Children.Add(BuildWalletCardElement(card));
        }
    }

    private UIElement BuildWalletCardElement(VaultPaymentCard card)
    {
        var brand = PaymentCardUtil.BrandOf(card.Number);
        var title = string.IsNullOrWhiteSpace(card.Label)
            ? $"{brand} •••• {PaymentCardUtil.Last4(card.Number)}"
            : card.Label;

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        titleRow.Children.Add(new FontIcon
        {
            Glyph = "",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 15,
            VerticalAlignment = VerticalAlignment.Center
        });
        titleRow.Children.Add(new TextBlock
        {
            Text = title,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontSize = AccessibilityBodyFontSize(),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(titleRow, 0);
        headerGrid.Children.Add(titleRow);

        var editBtn = new Button
        {
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14
            }
        };
        ApplyNovaControlAccessibility(editBtn, $"Modifier la carte {title}");
        ToolTipService.SetToolTip(editBtn, "Modifier cette carte");
        editBtn.Click += async (_, _) =>
        {
            var updated = await PromptPaymentCardAsync(card);
            if (updated is null) return;
            _vault.UpsertCard(updated);
            RefreshWalletPanel();
            UpdateStatusText("Carte mise à jour.");
        };
        Grid.SetColumn(editBtn, 1);
        headerGrid.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            FontSize = AccessibilitySecondaryFontSize(),
            Padding = new Thickness(8, 4, 8, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Content = new FontIcon
            {
                Glyph = "",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 14
            }
        };
        ApplyNovaControlAccessibility(deleteBtn, $"Supprimer la carte {title}");
        ToolTipService.SetToolTip(deleteBtn, "Supprimer cette carte");
        deleteBtn.Click += async (_, _) =>
        {
            var confirm = new ContentDialog
            {
                Title = "Supprimer cette carte ?",
                Content = $"{title}\n{PaymentCardUtil.MaskedNumber(card.Number)}\n\nCette action est définitive.",
                PrimaryButtonText = "Supprimer",
                CloseButtonText = "Annuler",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;
            _vault.DeleteCard(card.Id);
            RefreshWalletPanel();
            UpdateStatusText("Carte supprimée.");
        };
        Grid.SetColumn(deleteBtn, 2);
        headerGrid.Children.Add(deleteBtn);

        var body = new StackPanel();
        body.Children.Add(headerGrid);

        body.Children.Add(new TextBlock
        {
            Text = PaymentCardUtil.MaskedNumber(card.Number),
            FontSize = AccessibilityBodyFontSize(),
            Opacity = 0.75,
            Margin = new Thickness(0, 6, 0, 0)
        });

        var detail = $"{brand} · Expire {PaymentCardUtil.ExpiryText(card.ExpMonth, card.ExpYear)}";
        if (!string.IsNullOrWhiteSpace(card.Holder)) detail += $" · {card.Holder}";
        body.Children.Add(new TextBlock
        {
            Text = detail,
            Opacity = 0.6,
            FontSize = AccessibilitySecondaryFontSize(),
            Margin = new Thickness(0, 2, 0, 0)
        });

        if (PaymentCardUtil.IsExpired(card.ExpMonth, card.ExpYear, DateTime.UtcNow))
        {
            body.Children.Add(new TextBlock
            {
                Text = "Carte expirée",
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                FontSize = AccessibilitySecondaryFontSize(),
                Margin = new Thickness(0, 2, 0, 0)
            });
        }

        if (!string.IsNullOrWhiteSpace(card.Note))
        {
            body.Children.Add(new TextBlock
            {
                Text = card.Note,
                Opacity = 0.55,
                FontSize = AccessibilitySecondaryFontSize(),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            });
        }

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var copyNumber = new Button { Content = "Copier le numéro", FontSize = AccessibilitySecondaryFontSize() };
        ApplyNovaControlAccessibility(copyNumber, $"Copier le numéro de la carte {title}");
        copyNumber.Click += (_, _) =>
            CopySecretToClipboard(card.Number, "Numéro de carte copié (effacé dans 30 s).", clearAfterSeconds: 30);
        actions.Children.Add(copyNumber);

        var fillBtn = new Button { Content = "Utiliser sur la page active", FontSize = AccessibilitySecondaryFontSize() };
        ApplyNovaControlAccessibility(fillBtn, $"Utiliser la carte {title} sur la page active");
        fillBtn.Click += async (_, _) =>
        {
            ShowPanel(BrowserPanel, CurrentTab()?.Title ?? "Page active");
            UpdateStatusText((await FillCardAsync(card)).Message);
        };
        actions.Children.Add(fillBtn);

        body.Children.Add(actions);

        return new Border
        {
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 10, 14, 10),
            Child = body
        };
    }

    private async void WalletAddButton_Click(object sender, RoutedEventArgs e)
    {
        var card = await PromptPaymentCardAsync(existing: null);
        if (card is null) return;
        _vault.UpsertCard(card);
        RefreshWalletPanel();
        UpdateStatusText("Carte ajoutée au portefeuille.");
    }

    // Dialogue d'ajout/modification. Le numéro est validé par Luhn (détection de
    // faute de frappe) et l'expiration par plage. Pas de champ CVV : jamais stocké.
    private async Task<VaultPaymentCard?> PromptPaymentCardAsync(VaultPaymentCard? existing)
    {
        var labelBox = new TextBox
        {
            Header = "Nom de la carte",
            PlaceholderText = "ex. Carte perso",
            Text = existing?.Label ?? string.Empty,
            MaxLength = 40,
            MinWidth = 340
        };
        var holderBox = new TextBox
        {
            Header = "Titulaire",
            PlaceholderText = "NOM Prénom (tel qu'inscrit sur la carte)",
            Text = existing?.Holder ?? string.Empty,
            MinWidth = 340
        };
        var numberBox = new TextBox
        {
            Header = "Numéro de carte",
            PlaceholderText = "4111 1111 1111 1111",
            Text = existing?.Number ?? string.Empty,
            MinWidth = 340
        };
        var monthBox = new NumberBox
        {
            Header = "Mois",
            Minimum = 1,
            Maximum = 12,
            Value = existing?.ExpMonth is > 0 and <= 12 ? existing.ExpMonth : double.NaN,
            PlaceholderText = "MM",
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 140
        };
        var yearBox = new NumberBox
        {
            Header = "Année",
            Minimum = 0,
            Maximum = 2099,
            Value = existing?.ExpYear is > 0 ? existing.ExpYear : double.NaN,
            PlaceholderText = "AAAA",
            SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
            Width = 160
        };
        var noteBox = new TextBox
        {
            Header = "Note",
            PlaceholderText = "Optionnelle",
            Text = existing?.Note ?? string.Empty,
            MinWidth = 340
        };
        var errorText = new TextBlock
        {
            Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            Visibility = Visibility.Collapsed
        };

        var expiryRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        expiryRow.Children.Add(monthBox);
        expiryRow.Children.Add(yearBox);

        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(labelBox);
        panel.Children.Add(holderBox);
        panel.Children.Add(numberBox);
        panel.Children.Add(expiryRow);
        panel.Children.Add(noteBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Le cryptogramme (CVV) n'est jamais enregistré : vous le saisirez au moment du paiement.",
            Opacity = 0.6,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
        panel.Children.Add(errorText);

        var dialog = new ContentDialog
        {
            Title = existing is null ? "Ajouter une carte" : "Modifier la carte",
            Content = new ScrollViewer { Content = panel },
            PrimaryButtonText = "Enregistrer",
            CloseButtonText = "Annuler",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        // Validation au clic Enregistrer : on garde le dialogue ouvert tant que la
        // saisie est invalide, avec un message d'erreur clair.
        dialog.PrimaryButtonClick += (_, args) =>
        {
            var error = ValidateCardInput(numberBox.Text, monthBox.Value, yearBox.Value);
            if (error is not null)
            {
                errorText.Text = error;
                errorText.Visibility = Visibility.Visible;
                args.Cancel = true;
            }
        };

        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return null;

        return new VaultPaymentCard
        {
            Id = existing?.Id ?? string.Empty,
            Label = labelBox.Text.Trim(),
            Holder = holderBox.Text.Trim(),
            Number = PaymentCardUtil.NormalizeNumber(numberBox.Text),
            ExpMonth = (int)monthBox.Value,
            ExpYear = PaymentCardUtil.NormalizeYear((int)yearBox.Value),
            Note = noteBox.Text.Trim()
        };
    }

    private static string? ValidateCardInput(string number, double month, double year)
    {
        if (!PaymentCardUtil.IsValidNumber(number))
            return "Numéro de carte invalide : vérifiez la saisie.";
        if (double.IsNaN(month) || double.IsNaN(year) ||
            !PaymentCardUtil.IsValidExpiry((int)month, (int)year))
            return "Date d'expiration invalide (mois 1-12, année ex. 2027).";
        return null;
    }

    // ── Détection des pages de paiement ───────────────────────────────────────

    // Script injecté à la création de chaque document : signale UNE FOIS la
    // présence d'un champ de numéro de carte (formulaire présent au chargement ou
    // ajouté dynamiquement). Il ne lit AUCUNE valeur et n'expose rien à la page.
    private async Task RegisterPaymentMonitorAsync(CoreWebView2 core)
    {
        try
        {
            await core.AddScriptToExecuteOnDocumentCreatedAsync("""
                (function(){
                    if(window.__nova_payment_monitor)return;
                    window.__nova_payment_monitor=true;
                    var reported=false;
                    var sel='input[autocomplete*="cc-number"],input[name*="cardnumber" i],input[id*="cardnumber" i],input[name*="card-number" i],input[id*="card-number" i],input[name*="card_number" i],input[id*="card_number" i],input[name*="ccnumber" i],input[id*="ccnumber" i]';
                    var obs=new MutationObserver(detect);
                    function detect(){
                        if(reported)return;
                        if(!document.querySelector(sel))return;
                        reported=true;
                        try{obs.disconnect();}catch(_){}
                        try{window.chrome.webview.postMessage(JSON.stringify({t:'nova.payment.form',o:location.origin}));}catch(_){}
                    }
                    function start(){
                        detect();
                        if(!reported){
                            try{obs.observe(document.documentElement,{childList:true,subtree:true});}catch(_){}
                        }
                    }
                    if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',start);
                    else start();
                })();
                """);
        }
        catch { }
    }

    // Un champ carte est apparu sur la page ACTIVE : proposer le portefeuille.
    // La barre n'affiche aucune donnée de carte ; coffre verrouillé, on propose
    // quand même (le déverrouillage se fait au clic).
    private void HandlePaymentFormDetected(BrowserTabState tab)
    {
        // `tab` vient directement de l'abonnement (BrowserCore_WebMessageReceived,
        // corrige 2026-08-30) - plus de TabForCore ici (piege deja documente).
        if (_isGuestMode || !IsActiveView(tab.View)) return;
        if (!_vault.IsLocked && _vault.ListCards().Count == 0) return;

        WalletFillBar.Visibility = Visibility.Visible;
    }

    private async void WalletFillAccept_Click(object sender, RoutedEventArgs e)
    {
        if (_vault.IsLocked && !await UnlockVaultIfNeededAsync())
        {
            StatusText.Text = "Portefeuille verrouillé : remplissage annulé.";
            return;
        }

        var cards = _vault.ListCards();
        if (cards.Count == 0)
        {
            WalletFillBar.Visibility = Visibility.Collapsed;
            StatusText.Text = "Aucune carte dans le portefeuille.";
            return;
        }

        if (cards.Count == 1)
        {
            WalletFillBar.Visibility = Visibility.Collapsed;
            StatusText.Text = (await FillCardAsync(cards[0])).Message;
            return;
        }

        // Plusieurs cartes : choix explicite via un menu ancré au bouton.
        var flyout = new MenuFlyout();
        foreach (var card in cards.OrderByDescending(c => c.UpdatedAt))
        {
            var caption = string.IsNullOrWhiteSpace(card.Label)
                ? $"{PaymentCardUtil.BrandOf(card.Number)} •••• {PaymentCardUtil.Last4(card.Number)}"
                : $"{card.Label} (•••• {PaymentCardUtil.Last4(card.Number)})";
            var item = new MenuFlyoutItem { Text = caption };
            var chosen = card;
            item.Click += async (_, _) =>
            {
                WalletFillBar.Visibility = Visibility.Collapsed;
                StatusText.Text = (await FillCardAsync(chosen)).Message;
            };
            flyout.Items.Add(item);
        }
        HookFlyoutPointerSupport(flyout);
        flyout.ShowAt(WalletFillAccept);
    }

    private void WalletFillDismiss_Click(object sender, RoutedEventArgs e) =>
        WalletFillBar.Visibility = Visibility.Collapsed;

    // ── Remplissage (sur clic uniquement) ─────────────────────────────────────

    private async Task<(bool Success, string Message)> FillCardAsync(VaultPaymentCard card)
    {
        var core = _browserView?.CoreWebView2;
        if (core is null) return (false, "Moteur web indisponible.");

        var payload = JsonSerializer.Serialize(new
        {
            number = card.Number,
            holder = card.Holder,
            expMonthPadded = $"{card.ExpMonth:00}",
            expYearFull = PaymentCardUtil.NormalizeYear(card.ExpYear).ToString(),
            expYearShort = (PaymentCardUtil.NormalizeYear(card.ExpYear) % 100).ToString("00"),
            expText = PaymentCardUtil.ExpiryText(card.ExpMonth, card.ExpYear)
        });

        try
        {
            var script = PaymentFillScript.Replace("__NOVA_CARD_PAYLOAD__", payload);
            var resultJson = await core.ExecuteScriptAsync(script);
            var filled = ParseWalletFillResult(resultJson);
            return filled > 0
                ? (true, $"Carte remplie ({filled} champ{(filled > 1 ? "s" : "")}). Saisissez le CVV vous-même.")
                : (false, "Aucun champ de carte accessible sur cette page (formulaire dans un cadre externe ?).");
        }
        catch (Exception ex)
        {
            return (false, $"Remplissage impossible : {ex.Message}");
        }
    }

    private static int ParseWalletFillResult(string rawJson)
    {
        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is JsonValue value && value.TryGetValue<string>(out var nested))
                node = JsonNode.Parse(nested);
            return node is JsonObject obj && obj["filled"] is JsonValue f && f.TryGetValue<int>(out var n)
                ? n
                : 0;
        }
        catch { return 0; }
    }

    // Remplit les champs carte visibles et déclenche les événements input/change
    // pour les frameworks JS. Expiration : champ combiné MM/AA ou champs séparés
    // (input ou select). Ne touche jamais à un champ CVV.
    private const string PaymentFillScript = """
        (function(){
            var card=__NOVA_CARD_PAYLOAD__;
            function fire(el){
                ['input','change','keyup','blur'].forEach(function(t){
                    try{el.dispatchEvent(new Event(t,{bubbles:true}));}catch(_){}
                });
            }
            function visible(el){return !!(el&&el.offsetParent!==null);}
            function first(sels){
                for(var i=0;i<sels.length;i++){
                    var list=document.querySelectorAll(sels[i]);
                    for(var j=0;j<list.length;j++){if(visible(list[j]))return list[j];}
                }
                return null;
            }
            function setValue(el,value){
                if(!el||value===undefined||value===null||value==='')return false;
                var v=String(value);
                if(el.tagName==='SELECT'){
                    var done=false;
                    for(var i=0;i<el.options.length;i++){
                        var ov=el.options[i].value;
                        if(ov===v||ov===('0'+v)||String(parseInt(ov,10))===String(parseInt(v,10))){
                            el.selectedIndex=i;done=true;break;
                        }
                    }
                    if(!done)return false;
                }else{
                    try{el.focus();}catch(_){}
                    el.value=v;
                }
                fire(el);
                return true;
            }
            var filled=0;
            var numEl=first(['input[autocomplete*="cc-number"]','input[name*="cardnumber" i]','input[id*="cardnumber" i]','input[name*="card-number" i]','input[id*="card-number" i]','input[name*="card_number" i]','input[id*="card_number" i]','input[name*="ccnumber" i]','input[id*="ccnumber" i]']);
            if(setValue(numEl,card.number))filled++;
            var nameEl=first(['input[autocomplete*="cc-name"]','input[name*="cardholder" i]','input[id*="cardholder" i]','input[name*="card-holder" i]','input[id*="card-holder" i]','input[name*="card_holder" i]','input[name*="holdername" i]']);
            if(setValue(nameEl,card.holder))filled++;
            var moEl=first(['input[autocomplete="cc-exp-month"]','select[autocomplete="cc-exp-month"]','select[name*="exp" i][name*="month" i]','select[id*="exp" i][id*="month" i]','select[name*="expmonth" i]','select[id*="expmonth" i]','input[name*="exp" i][name*="month" i]']);
            var yrEl=first(['input[autocomplete="cc-exp-year"]','select[autocomplete="cc-exp-year"]','select[name*="exp" i][name*="year" i]','select[id*="exp" i][id*="year" i]','select[name*="expyear" i]','select[id*="expyear" i]','input[name*="exp" i][name*="year" i]']);
            if(moEl||yrEl){
                if(setValue(moEl,card.expMonthPadded))filled++;
                if(yrEl&&(setValue(yrEl,card.expYearFull)||setValue(yrEl,card.expYearShort)))filled++;
            }else{
                var expEl=first(['input[autocomplete*="cc-exp"]','input[name*="expir" i]','input[id*="expir" i]','input[name*="exp-date" i]','input[id*="exp-date" i]','input[name*="exp_date" i]']);
                if(setValue(expEl,card.expText))filled++;
            }
            return JSON.stringify({filled:filled});
        })();
        """;
}

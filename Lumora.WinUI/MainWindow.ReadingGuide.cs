using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace Lumora.WinUI;

public sealed partial class MainWindow
{
    private static int NormalizeReadingGuideBandHeight(int bandHeight) =>
        bandHeight switch
        {
            120 or 160 or 220 or 300 => bandHeight,
            < 140 => 120,
            < 190 => 160,
            < 260 => 220,
            _ => 300
        };

    private int SelectedReadingGuideBandHeight()
    {
        if (ReadingGuideBandHeightCombo.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Tag?.ToString(), out var bandHeight))
        {
            return NormalizeReadingGuideBandHeight(bandHeight);
        }

        return NormalizeReadingGuideBandHeight(_uiSettings.AccessibilityReadingGuideBandHeight);
    }

    private async Task ApplyReadingGuideToAllTabsAsync()
    {
        foreach (var tab in _tabs)
        {
            await ApplyReadingGuideAsync(tab.View);
        }
    }

    private async Task ApplyReadingGuideAsync(WebView2? view)
    {
        var core = view?.CoreWebView2;
        if (view is null || core is null)
        {
            return;
        }

        try
        {
            await core.ExecuteScriptAsync(
                BuildReadingGuideScript(
                    _uiSettings.AccessibilityReadingGuideEnabled,
                    NormalizeReadingGuideBandHeight(_uiSettings.AccessibilityReadingGuideBandHeight),
                    _uiSettings.AccessibilityHighContrast));
        }
        catch (Exception error)
        {
            WinUiRuntimeTrace.Write($"Reading guide script skipped: {error.GetType().Name}");
        }
    }

    private static string BuildReadingGuideScript(bool enabled, int bandHeight, bool highContrast)
    {
        var serializedConfig = JsonSerializer.Serialize(new
        {
            enabled,
            bandHeight = NormalizeReadingGuideBandHeight(bandHeight),
            dimColor = highContrast ? "rgba(0,0,0,0.58)" : "rgba(7,13,18,0.34)",
            bandColor = highContrast ? "rgba(255,213,0,0.14)" : "rgba(255,248,234,0.08)",
            edgeColor = highContrast ? "rgba(255,213,0,0.92)" : "rgba(255,185,53,0.54)"
        });

        return $$"""
(() => {
  const config = {{serializedConfig}};
  const stateKey = "__lumoraReadingGuideState";
  const styleId = "lumora-reading-guide-style";
  const overlayId = "lumora-reading-guide-overlay";
  const bandId = "lumora-reading-guide-band";

  const teardown = () => {
    const state = window[stateKey];
    if (state && typeof state.cleanup === "function") {
      state.cleanup();
    }
    delete window[stateKey];
    document.getElementById(overlayId)?.remove();
    document.getElementById(styleId)?.remove();
  };

  if (!config.enabled) {
    teardown();
    return;
  }

  teardown();

  const clamp = (value, min, max) => Math.min(max, Math.max(min, value));

  let style = document.getElementById(styleId);
  if (!style) {
    style = document.createElement("style");
    style.id = styleId;
    style.textContent = `
      #${overlayId}{
        position:fixed!important;
        inset:0!important;
        pointer-events:none!important;
        z-index:2147483646!important;
        overflow:hidden!important;
      }
      #${bandId}{
        position:fixed!important;
        left:0!important;
        right:0!important;
        border-top:1px solid ${config.edgeColor}!important;
        border-bottom:1px solid ${config.edgeColor}!important;
        background:${config.bandColor}!important;
        box-shadow:0 0 0 9999px ${config.dimColor}!important;
        transition:top 90ms ease-out,height 90ms ease-out!important;
      }
    `;
    (document.head || document.documentElement).appendChild(style);
  }

  let overlay = document.getElementById(overlayId);
  if (!overlay) {
    overlay = document.createElement("div");
    overlay.id = overlayId;
    overlay.setAttribute("aria-hidden", "true");
    const band = document.createElement("div");
    band.id = bandId;
    overlay.appendChild(band);
    (document.documentElement || document.body).appendChild(overlay);
  }

  const band = document.getElementById(bandId);
  if (!band) {
    return;
  }

  const defaultY = () => Math.round(window.innerHeight * 0.36);
  const minPadding = 18;

  const ensureState = () => {
    if (window[stateKey]) {
      return window[stateKey];
    }

    const state = {
      centerY: defaultY(),
      cleanup: null
    };

    const render = () => {
      state.centerY = clamp(
        state.centerY,
        Math.round(config.bandHeight / 2) + minPadding,
        Math.max(Math.round(config.bandHeight / 2) + minPadding, window.innerHeight - Math.round(config.bandHeight / 2) - minPadding)
      );
      band.style.height = `${config.bandHeight}px`;
      band.style.top = `${Math.round(state.centerY - config.bandHeight / 2)}px`;
    };

    const moveTo = (y) => {
      state.centerY = y;
      render();
    };

    const onPointerMove = (event) => moveTo(event.clientY);
    const onWheel = (event) => moveTo(state.centerY + Math.sign(event.deltaY || 0) * Math.min(42, Math.abs(event.deltaY || 0)));
    const onResize = () => render();
    const onFocusIn = (event) => {
      const target = event.target;
      if (!target || typeof target.getBoundingClientRect !== "function") {
        return;
      }
      const rect = target.getBoundingClientRect();
      if (rect && Number.isFinite(rect.top) && rect.height > 0) {
        moveTo(rect.top + Math.min(rect.height / 2, config.bandHeight / 2));
      }
    };
    const onKeyDown = (event) => {
      const bigStep = Math.max(72, Math.round(config.bandHeight * 0.9));
      const smallStep = Math.max(28, Math.round(config.bandHeight * 0.32));
      switch (event.key) {
        case "ArrowDown":
          moveTo(state.centerY + smallStep);
          break;
        case "ArrowUp":
          moveTo(state.centerY - smallStep);
          break;
        case "PageDown":
        case " ":
          moveTo(state.centerY + bigStep);
          break;
        case "PageUp":
          moveTo(state.centerY - bigStep);
          break;
        case "Home":
          moveTo(Math.round(config.bandHeight / 2) + 28);
          break;
        case "End":
          moveTo(window.innerHeight - Math.round(config.bandHeight / 2) - 28);
          break;
      }
    };

    window.addEventListener("mousemove", onPointerMove, { passive: true });
    window.addEventListener("wheel", onWheel, { passive: true });
    window.addEventListener("resize", onResize, { passive: true });
    document.addEventListener("focusin", onFocusIn, true);
    document.addEventListener("keydown", onKeyDown, true);

    state.cleanup = () => {
      window.removeEventListener("mousemove", onPointerMove);
      window.removeEventListener("wheel", onWheel);
      window.removeEventListener("resize", onResize);
      document.removeEventListener("focusin", onFocusIn, true);
      document.removeEventListener("keydown", onKeyDown, true);
    };

    state.render = render;
    window[stateKey] = state;
    return state;
  };

  const state = ensureState();
  state.render();
})();
""";
    }
}

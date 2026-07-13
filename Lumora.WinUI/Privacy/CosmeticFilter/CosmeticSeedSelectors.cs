namespace Lumora.Privacy.CosmeticFilter;

// Sélecteurs CSS ad/tracker actifs dès le premier lancement, sans téléchargement des listes.
// Choix conservateur : uniquement des sélecteurs sans ambiguïté fonctionnelle.
internal static class CosmeticSeedSelectors
{
    internal static readonly string[] Generic =
    [
        // ── Google Ads ────────────────────────────────────────────────────────
        "ins.adsbygoogle",
        "div.adsbygoogle",
        "[data-ad-slot]",
        "[data-ad-client]",
        "[data-ad-unit-id]",
        "[id^='div-gpt-ad']",
        "[id^='google_ads']",
        "[id^='google_ad']",
        ".google-auto-placed",

        // ── Noms de classe génériques pub ─────────────────────────────────────
        ".ad-banner",
        ".ad-container",
        ".ad-wrapper",
        ".ad-block",
        ".ad-slot",
        ".ad-unit",
        ".ad-zone",
        ".ad-area",
        ".ad-box",
        ".ad-row",
        ".ad-col",
        ".ad-frame",
        ".adspot",
        ".adsense",
        ".adunit",

        // ── advertisement / adverts ───────────────────────────────────────────
        ".advertisement",
        ".advertisement-container",
        ".advertise-here",
        ".advert",
        ".adverts",
        "[class*='advertisement']",
        "[class*='advert-']",

        // ── Sponsored / promo ─────────────────────────────────────────────────
        ".sponsored",
        ".sponsored-content",
        ".sponsored-article",
        ".sponsored-post",
        ".sponsored-link",
        ".sponsor-label",
        ".sponsor-message",
        "[data-sponsored='true']",

        // ── Bannières / sidebars pub ──────────────────────────────────────────
        ".banner-ad",
        ".banner-advertisement",
        ".sidebar-ad",
        ".sidebar-ads",
        ".leaderboard-ad",
        ".rectangle-ad",
        ".sticky-ad",
        ".floating-ad",
        ".overlay-ad",
        ".interstitial-ad",

        // ── Attributs data-* pub ──────────────────────────────────────────────
        "[data-ad='true']",
        "[data-ad-loaded]",
        "[data-adunit]",
        "[data-adtype]",
        "[data-dfp]",
        "[data-google-query-id]",

        // ── IDs pub courants ──────────────────────────────────────────────────
        "[id^='dfp-ad-']",
        "[id^='dfp_ad_']",
        "[id*='-ad-slot']",
        "[id*='_ad_slot']",
        "[id$='-ad']",
        "[id$='_ads']",
        "[id^='ad-unit-']",

        // ── Taboola ───────────────────────────────────────────────────────────
        ".taboola",
        ".trc_related_container",
        "#taboola-above-article-thumbnails",
        "#taboola-below-article-thumbnails",
        "#taboola-right-rail-thumbnails",
        "[id^='taboola-']",
        "[class^='tbl-']",

        // ── Outbrain ──────────────────────────────────────────────────────────
        ".OUTBRAIN",
        ".outbrain",
        "[data-widget-id^='OB-']",

        // ── Criteo ────────────────────────────────────────────────────────────
        ".criteo-ad",
        "[data-criteo-id]",

        // ── Pub Yandex ────────────────────────────────────────────────────────
        ".yandex-ad",
        "[id^='yandex_rtb']",

        // ── Pub Facebook/Meta ─────────────────────────────────────────────────
        "[id^='fb-ad-']",

        // ── Formats standards (tailles) ───────────────────────────────────────
        ".ad-300x250",
        ".ad-728x90",
        ".ad-320x50",
        ".ad-160x600",
        ".ad-970x250",

        // ── Divers ───────────────────────────────────────────────────────────
        ".ad-label",
        ".ad-disclosure",
        ".ad-placeholder",
        "[data-testid='ad']",
        "[aria-label='Advertisement']",
        "[aria-label='Advertising']",
        ".revenue_ad",
        ".ads-container",
        ".ads-wrapper",
        ".ads-slot",
        ".display-ad",
        ".native-ad",
        ".in-article-ad",
        ".in-feed-ad",
        ".parallax-ad",
        ".video-ad",
        ".pre-roll-ad",
    ];
}

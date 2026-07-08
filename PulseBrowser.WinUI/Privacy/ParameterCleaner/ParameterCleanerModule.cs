using System.Web;

namespace PulseBrowser.Privacy.ParameterCleaner;

// Supprime les paramètres de tracking des URLs avant navigation.
// Liste exhaustive couvrant Google, Facebook, Microsoft, HubSpot, Mailchimp,
// TikTok, LinkedIn, Pinterest, Twitter, Reddit, Klaviyo, Marketo et +30 autres.
internal sealed class ParameterCleanerModule : IPrivacyModule
{
    public string Id => "parameter-cleaner";
    public string DisplayName => "Nettoyage des URL de tracking";
    public bool IsEnabled { get; set; } = true;

    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        // ── Google Analytics / UTM ─────────────────────────────────────────────
        "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
        "utm_id", "utm_source_platform", "utm_creative_format", "utm_marketing_tactic",
        // ── Google Ads ────────────────────────────────────────────────────────
        "gclid", "gclsrc", "gad_source", "gbraid", "wbraid", "dclid",
        // ── Facebook / Meta ───────────────────────────────────────────────────
        "fbclid", "fb_action_ids", "fb_action_types", "fb_source", "fb_ref",
        // ── Microsoft / Bing Ads ──────────────────────────────────────────────
        "msclkid",
        // ── HubSpot ───────────────────────────────────────────────────────────
        "_hsenc", "_hsmi", "__hssc", "__hstc", "__hsfp", "hsCtaTracking",
        // ── Mailchimp ─────────────────────────────────────────────────────────
        "mc_cid", "mc_eid",
        // ── ActiveCampaign ────────────────────────────────────────────────────
        "vero_id", "vero_conv",
        // ── Klaviyo ───────────────────────────────────────────────────────────
        "_kx",
        // ── Brevo (ex-Sendinblue) ─────────────────────────────────────────────
        "sib_uid",
        // ── Drip ──────────────────────────────────────────────────────────────
        "drip_token",
        // ── ConvertKit ────────────────────────────────────────────────────────
        "ck_subscriber_id",
        // ── Iterable ─────────────────────────────────────────────────────────
        "_itb",
        // ── Marketo ───────────────────────────────────────────────────────────
        "mkt_tok",
        // ── Pinterest ─────────────────────────────────────────────────────────
        "epik",
        // ── LinkedIn ─────────────────────────────────────────────────────────
        "li_fat_id",
        // ── TikTok ────────────────────────────────────────────────────────────
        "ttclid",
        // ── Snapchat ─────────────────────────────────────────────────────────
        "ScCid",
        // ── Twitter / X ──────────────────────────────────────────────────────
        "twclid",
        // ── Reddit ────────────────────────────────────────────────────────────
        "rdt_cid",
        // ── Yandex ───────────────────────────────────────────────────────────
        "yclid", "ymclid",
        // ── Instagram ─────────────────────────────────────────────────────────
        "igshid",
        // ── Zanox / Awin ─────────────────────────────────────────────────────
        "zanpid",
        // ── IBM Campaign ─────────────────────────────────────────────────────
        "cm_mmc", "cm_mmca1", "cm_mmca2", "cm_mmca3", "cm_mmca4", "cm_mmca5",
        // ── Eloqua ───────────────────────────────────────────────────────────
        "elqTrackId", "elqaid", "elqat",
        // ── Adobe / Omniture ─────────────────────────────────────────────────
        "s_kwcid", "s_tbe",
        // ── Omnisend ─────────────────────────────────────────────────────────
        "omnisend",
        // ── Divers ───────────────────────────────────────────────────────────
        "ICID", "icid",
        "trk_contact", "trk_msg", "trk_module", "trk_sid",   // Pardot
        "hsa_acc", "hsa_cam", "hsa_grp", "hsa_ad", "hsa_src",
        "hsa_tgt", "hsa_kw", "hsa_mt", "hsa_net", "hsa_ver", // HubSpot Ads
        "oly_anon_id", "oly_enc_id",                          // Olyvid
        "_openstat",                                          // Yandex/openstat
        "wickedid",                                           // Wicked Reports
        "rb_clickid",                                         // RichAds
    };

    public string? CleanUrl(string uri)
    {
        if (!uri.Contains('?') && !uri.Contains('#')) return null;

        try
        {
            var u = new Uri(uri);
            var query = HttpUtility.ParseQueryString(u.Query);

            var paramsToRemove = query.AllKeys
                .Where(k => k is not null && TrackingParams.Contains(k))
                .ToList();

            if (paramsToRemove.Count == 0) return null;

            foreach (var k in paramsToRemove)
                query.Remove(k);

            var newQuery = query.Count > 0 ? "?" + query : string.Empty;
            var builder = new UriBuilder(u)
            {
                Query = newQuery.TrimStart('?')
            };

            return builder.Uri.AbsoluteUri;
        }
        catch { return null; }
    }
}

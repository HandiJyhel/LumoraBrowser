namespace Lumora.Privacy.LoginCompatibility;

internal static class LoginCompatibilityScripts
{
    internal static string BuildInjectionScript(IEnumerable<string> rootDomains)
    {
        var compatibilityList = CompatibilityList(rootDomains);

        return $$$"""
        (function(){
            'use strict';

            var COMPAT = {{{compatibilityList}}};
            var USER_ACTION_WINDOW_MS = 6000;
            var lastUserActionAt = 0;
            var installAttempts = 0;
            var installTimer = 0;

            function hostMatchesDomain(host, domain) {
                return host === domain || host.endsWith('.' + domain);
            }

            function enabledForPage() {
                try {
                    var host = (location.hostname || '').toLowerCase();
                    for (var i = 0; i < COMPAT.length; i++) {
                        if (hostMatchesDomain(host, COMPAT[i])) return true;
                    }
                } catch(e) {}
                return false;
            }

            if (!enabledForPage()) return;

            function markUserAction() {
                lastUserActionAt = Date.now();
            }

            function hasRecentUserAction() {
                return Date.now() - lastUserActionAt <= USER_ACTION_WINDOW_MS;
            }

            function suppressedMoment() {
                return {
                    isDisplayMoment: function(){ return false; },
                    isDisplayed: function(){ return false; },
                    isNotDisplayed: function(){ return true; },
                    getNotDisplayedReason: function(){ return 'suppressed_by_nova_until_user_click'; },
                    isSkippedMoment: function(){ return true; },
                    getSkippedReason: function(){ return 'auto_google_prompt_blocked_by_nova'; },
                    isDismissedMoment: function(){ return false; },
                    getDismissedReason: function(){ return ''; },
                    getMomentType: function(){ return 'skipped'; }
                };
            }

            function sanitizeInitializeOptions(options) {
                if (!options || typeof options !== 'object') return options;
                try {
                    options.auto_select = false;
                    options.cancel_on_tap_outside = true;
                } catch(e) {}
                return options;
            }

            function wrapGoogleIdentity() {
                try {
                    var id = window.google && window.google.accounts && window.google.accounts.id;
                    if (!id || id.__novaLoginCompatibilityWrapped) return false;

                    var originalInitialize = id.initialize;
                    var originalPrompt = id.prompt;

                    if (typeof originalInitialize === 'function') {
                        id.initialize = function(options) {
                            return originalInitialize.call(this, sanitizeInitializeOptions(options));
                        };
                    }

                    if (typeof originalPrompt === 'function') {
                        id.prompt = function(momentCallback) {
                            if (!hasRecentUserAction()) {
                                try {
                                    if (typeof momentCallback === 'function') {
                                        setTimeout(function(){ momentCallback(suppressedMoment()); }, 0);
                                    }
                                } catch(e) {}
                                return;
                            }
                            return originalPrompt.apply(this, arguments);
                        };
                    }

                    try {
                        Object.defineProperty(id, '__novaLoginCompatibilityWrapped', {
                            value: true,
                            configurable: true
                        });
                    } catch(e) {
                        id.__novaLoginCompatibilityWrapped = true;
                    }

                    return true;
                } catch(e) {
                    return false;
                }
            }

            function installSoon() {
                if (wrapGoogleIdentity()) {
                    if (installTimer) clearInterval(installTimer);
                    return;
                }
                if (!installTimer) {
                    installTimer = setInterval(function(){
                        installAttempts++;
                        if (wrapGoogleIdentity() || installAttempts > 160) {
                            clearInterval(installTimer);
                            installTimer = 0;
                        }
                    }, 125);
                }
            }

            try {
                window.addEventListener('pointerdown', markUserAction, true);
                window.addEventListener('keydown', markUserAction, true);
                window.addEventListener('submit', markUserAction, true);
            } catch(e) {}

            try {
                var currentGoogle = window.google;
                Object.defineProperty(window, 'google', {
                    configurable: true,
                    get: function(){ return currentGoogle; },
                    set: function(value){
                        currentGoogle = value;
                        setTimeout(installSoon, 0);
                    }
                });
                if (currentGoogle) setTimeout(installSoon, 0);
            } catch(e) {
                setTimeout(installSoon, 0);
            }

            if (document.readyState === 'loading') {
                document.addEventListener('DOMContentLoaded', installSoon, { once: true });
            } else {
                installSoon();
            }
        })();
        """;
    }

    private static string CompatibilityList(IEnumerable<string> rootDomains)
    {
        var domains = rootDomains
            .Select(domain => domain.Trim().ToLowerInvariant())
            .Where(domain => !string.IsNullOrWhiteSpace(domain))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(domain => "'" + domain.Replace("\\", "\\\\").Replace("'", "\\'") + "'");
        return "[" + string.Join(",", domains) + "]";
    }
}

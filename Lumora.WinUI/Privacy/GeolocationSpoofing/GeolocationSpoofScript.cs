using System.Globalization;
using System.Text.Json;

namespace Lumora.Privacy.GeolocationSpoofing;

// Remplace navigator.geolocation.getCurrentPosition/watchPosition par une
// position fixe AVANT que le script de la page ne s'execute (injecte via
// AddScriptToExecuteOnDocumentCreatedAsync). Les sites presents dans
// exemptRootDomains (regle "Autoriser" explicite dans Site actuel > Localisation)
// gardent l'API native intacte : cette regle par site reste prioritaire.
internal static class GeolocationSpoofScript
{
    public static string Build(double latitude, double longitude, IReadOnlyList<string> exemptRootDomains)
    {
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);
        var exemptJson = JsonSerializer.Serialize(exemptRootDomains);

        return $@"(function(){{
if(!navigator.geolocation)return;
var exempt={exemptJson};
var host=(document.location&&document.location.hostname||'').toLowerCase();
for(var i=0;i<exempt.length;i++){{
var root=exempt[i];
if(host===root||host.endsWith('.'+root))return;
}}
var fakePosition={{coords:{{latitude:{lat},longitude:{lon},accuracy:100,altitude:null,altitudeAccuracy:null,heading:null,speed:null}},timestamp:Date.now()}};
navigator.geolocation.getCurrentPosition=function(success){{
if(typeof success==='function')setTimeout(function(){{success(fakePosition);}},0);
}};
navigator.geolocation.watchPosition=function(success){{
if(typeof success==='function')setTimeout(function(){{success(fakePosition);}},0);
return 1;
}};
navigator.geolocation.clearWatch=function(){{}};
}})();";
    }
}

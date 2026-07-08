using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace PulseBrowser.Privacy.CnameUncloaker;

// Résout les chaînes CNAME via l'API Windows native DnsQuery_W.
// Utilise le résolveur DNS configuré par l'utilisateur (routeur, DoH système…)
// sans envoyer de données vers aucun service tiers Pulse.
internal static class CnameResolver
{
    private const ushort DnsTypeCname = 5;
    private const uint   DnsFreeRecordListDeep = 1;

    [DllImport("dnsapi.dll", EntryPoint = "DnsQuery_W",
               CharSet = CharSet.Unicode, ExactSpelling = true,
               SetLastError = false)]
    private static extern int DnsQuery(
        string  pszName,
        ushort  wType,
        uint    options,
        nint    pExtra,
        ref nint ppQueryResults,
        nint    pReserved);

    [DllImport("dnsapi.dll", SetLastError = false)]
    private static extern void DnsRecordListFree(nint pRecord, uint freeType);

    // x64 layout de DNS_RECORD (windns.h) :
    //   +0  pNext        (nint, 8 octets)
    //   +8  pName        (nint, 8 octets)
    //   +16 wType        (ushort, 2 octets)
    //   +18 wDataLength  (ushort, 2 octets)
    //   +20 Flags        (uint, 4 octets)
    //   +24 dwTtl        (uint, 4 octets)
    //   +28 dwReserved   (uint, 4 octets)
    //   +32 Data union   → pNameHost (nint) pour CNAME
    private const int OffsetWType     = 16;
    private const int OffsetPNext     = 0;
    private const int OffsetPNameHost = 32; // premier champ de DNS_CNAME_DATA

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    private static readonly ConcurrentDictionary<string, (IReadOnlyList<string> Chain, DateTime Expires)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly SemaphoreSlim Throttle = new(8, 8);

    // Retourne la chaîne CNAME complète pour le hostname donné.
    // Ex : "metrics.site.com" → ["cdn.tracker.net", "tracker.net"]
    public static async Task<IReadOnlyList<string>> ResolveCnameChainAsync(string hostname)
    {
        if (Cache.TryGetValue(hostname, out var cached) && DateTime.UtcNow < cached.Expires)
            return cached.Chain;

        await Throttle.WaitAsync();
        try
        {
            var chain = await Task.Run(() => WalkCnameChain(hostname));
            Cache[hostname] = (chain, DateTime.UtcNow + CacheTtl);
            return chain;
        }
        finally { Throttle.Release(); }
    }

    private static IReadOnlyList<string> WalkCnameChain(string hostname)
    {
        var result  = new List<string>();
        var current = hostname;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { hostname };

        for (int depth = 0; depth < 10; depth++)
        {
            var next = QuerySingleCname(current);
            if (next is null) break;
            if (!visited.Add(next)) break; // boucle détectée

            result.Add(next);
            current = next;
        }

        return result;
    }

    private static string? QuerySingleCname(string hostname)
    {
        nint pResults = nint.Zero;
        try
        {
            int hr = DnsQuery(hostname, DnsTypeCname, 0, nint.Zero, ref pResults, nint.Zero);
            if (hr != 0 || pResults == nint.Zero) return null;

            // Parcourir la liste chaînée de records pour trouver un CNAME
            nint cur = pResults;
            while (cur != nint.Zero)
            {
                ushort wType = (ushort)Marshal.ReadInt16(cur, OffsetWType);
                if (wType == DnsTypeCname)
                {
                    nint pNameHost = Marshal.ReadIntPtr(cur, OffsetPNameHost);
                    return pNameHost != nint.Zero
                        ? Marshal.PtrToStringUni(pNameHost)
                        : null;
                }
                cur = Marshal.ReadIntPtr(cur, OffsetPNext);
            }

            return null;
        }
        catch { return null; }
        finally
        {
            if (pResults != nint.Zero)
                DnsRecordListFree(pResults, DnsFreeRecordListDeep);
        }
    }
}

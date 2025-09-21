using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using OtpNet;
using SpotifyExplode.Utils.Extensions;

namespace SpotifyExplode;

internal class SpotifyHttp(HttpClient http)
{
    private static byte[]? TotpGenSecret;
    private static int? TotpGenVer;
    public async ValueTask<string> GetAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        var accessToken = await GetAccessTokenAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return await http.ExecuteAsync(request, cancellationToken);
    }

    private void GetLatestSecrets()
    {
        // https://github.com/Thereallo1026/spotify-secrets/blob/main/secrets/secrets.json?raw=true
        /*
         * import requests
           
           # Fetch secrets
           response = requests.get("https://github.com/Thereallo1026/spotify-secrets/blob/main/secrets/secretDict.json?raw=true")
           secrets = response.json()
           
           # Get latest version
           latest_secret = secrets[(v := max(secrets, key=int))]
           print(f"Version {v}: {latest_secret}")
         */

        const string secretsUrl =
            "https://github.com/Thereallo1026/spotify-secrets/blob/main/secrets/secretBytes.json?raw=true";
        
        var secretsJson = http.ExecuteAsync(secretsUrl).GetAwaiter().GetResult();
        var secretsArray = JsonNode.Parse(secretsJson)!.AsArray();
        var latestSecret = secretsArray
            .Select(node => new
            {
                Version = node!["version"]!.GetValue<int>(),
                Secret = node["secret"]!.AsArray().Select(n => n!.GetValue<byte>()).ToArray()
            })
            .OrderByDescending(s => s.Version)
            .First();
        
        var transformed = latestSecret.Secret
            .Select((b, i) => (byte)(b ^ ((i % 33) + 9)))
            .ToArray();

        var joined = transformed
            .Select(b => b.ToString())
            .Aggregate((a, b) => a + b);
        
        var hexStr = string.Concat(joined.Select(c => ((int)c).ToString("x2")));
        var hexBytes = HexStringToBytes(hexStr);
        
        TotpGenVer = latestSecret.Version;
        TotpGenSecret = hexBytes;
    }

    private async ValueTask<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        if (TotpGenSecret == null || TotpGenVer == null)
            GetLatestSecrets();

        string totp = CalculateTotp();
        
        string product = "app";
        string reason = "transport";
        
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://open.spotify.com/api/token?reason={reason}&productType={product}&totp={totp}&totpServer={totp}&totpVer={TotpGenVer}"
        );

        var tokenJson = await http.ExecuteAsync(request, cancellationToken);
        var spotifyJsonToken = JsonNode.Parse(tokenJson)!;

        return spotifyJsonToken["accessToken"]!.ToString();
    }
    
    private static string CalculateTotp()
    {
        // string base32Secret = Base32Encoding.ToString(secret);
        // var totp = new Totp(Base32Encoding.ToBytes(base32Secret), step: 30, totpSize: 6);
        // string token = totp.ComputeTotp();
        // return token;
        
        var totp = new Totp(SpotifyHttp.TotpGenSecret, step: 30, totpSize: 6);
        string token = totp.ComputeTotp();
        return token;
    }
    
    private static byte[] HexStringToBytes(string hex)
    {
        int length = hex.Length;
        byte[] bytes = new byte[length / 2];
        for (int i = 0; i < length; i += 2)
            bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
        return bytes;
    }
}
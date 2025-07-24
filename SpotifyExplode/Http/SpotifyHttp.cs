using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using OtpNet;
using SpotifyExplode.Utils.Extensions;

namespace SpotifyExplode;

internal class SpotifyHttp(HttpClient http)
{
    private static readonly byte[] TotpGenSecret = HexStringToBytes("373935343432353338313131383835343934393434343533353130373735383535393131373934");
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

    private async ValueTask<string> GetAccessTokenAsync(
        CancellationToken cancellationToken = default)
    {
        string totp = CalculateTotp(TotpGenSecret);
        
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://open.spotify.com/api/token?reason=transport&productType=app&totp={totp}&totpServer={totp}&totpVer=18"
        );

        var tokenJson = await http.ExecuteAsync(request, cancellationToken);

        var spotifyJsonToken = JsonNode.Parse(tokenJson)!;

        return spotifyJsonToken["accessToken"]!.ToString();
    }

    private static string CalculateTotp(byte[] secret)
    {
        string base32Secret = Base32Encoding.ToString(secret);
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret), step: 30, totpSize: 6);
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
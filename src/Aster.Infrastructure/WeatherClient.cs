using System.Globalization;
using System.Net;
using System.Text.Json;
using Aster.Core;

namespace Aster.Infrastructure;

public sealed record WeatherReading(double TemperatureC, double WindKph, int Code);
public sealed class WeatherClient : IDisposable
{
    private readonly HttpClient client;
    private readonly NetworkConsent consent;
    private readonly NetworkAudit audit;
    public WeatherClient(NetworkConsent consent, NetworkAudit audit, HttpMessageHandler? transport = null)
    {
        this.consent = consent; this.audit = audit;
        client = new HttpClient(transport ?? new HttpClientHandler
        {
            AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 256_000 };
    }
    public async Task<WeatherReading> FetchAsync(double latitude, double longitude, CancellationToken token = default)
    {
        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude is < -90 or > 90 || longitude is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(latitude), "Enter valid latitude and longitude.");
        var url = new Uri(string.Create(CultureInfo.InvariantCulture, $"https://api.open-meteo.com/v1/forecast?latitude={latitude:F4}&longitude={longitude:F4}&current=temperature_2m,wind_speed_10m,weather_code"));
        CancellationToken permission;
        try { permission = consent.Authorize(url); }
        catch (UnauthorizedAccessException) { audit.Record(url.Host, "blocked"); throw; }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(permission, token);
        audit.Record(url.Host, "attempt");
        try
        {
            using var response = await client.GetAsync(url, linked.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(linked.Token).ConfigureAwait(false));
            var current = doc.RootElement.GetProperty("current");
            var result = new WeatherReading(current.GetProperty("temperature_2m").GetDouble(), current.GetProperty("wind_speed_10m").GetDouble(), current.GetProperty("weather_code").GetInt32());
            if (!double.IsFinite(result.TemperatureC) || !double.IsFinite(result.WindKph)) throw new InvalidDataException("Invalid weather response.");
            linked.Token.ThrowIfCancellationRequested();
            audit.Record(url.Host, "success"); return result;
        }
        catch (OperationCanceledException) { audit.Record(url.Host, "cancelled-or-timeout"); throw; }
        catch { audit.Record(url.Host, "failed"); throw; }
    }
    public void Dispose() => client.Dispose();
}

using System.Text.Json;
using Microsoft.Extensions.Options;

namespace DndHelper.Data.Open5e;

/// <summary>Thin client over the Open5e v2 REST API that follows pagination.</summary>
public sealed class Open5eClient(HttpClient http, IOptions<Open5eOptions> options)
{
    private readonly Open5eOptions _options = options.Value;

    public async Task<IReadOnlyList<JsonElement>> GetAllAsync(
        string endpoint,
        string label,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var keys = Uri.EscapeDataString(string.Join(",", _options.DocumentKeys));
        string? url = $"v2/{endpoint}/?document__key__in={keys}&limit={_options.PageSize}";

        var results = new List<JsonElement>();
        while (url is not null)
        {
            using var response = await http.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);
            var root = doc.RootElement;

            if (root.TryGetProperty("results", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                    results.Add(item.Clone());
            }

            var total = root.TryGetProperty("count", out var c) && c.TryGetInt32(out var n) ? n : results.Count;
            progress?.Report($"Downloading {label}: {results.Count} / {total}");

            url = root.TryGetProperty("next", out var next) && next.ValueKind == JsonValueKind.String
                ? next.GetString()
                : null;
        }

        return results;
    }
}
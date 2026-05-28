using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZenQuotesApp.Wpf.Models;

namespace ZenQuotesApp.Wpf.Services;

// Клиент API zenquotes.io (~50 цитат за запрос). Решение о фолбеке принимает QuoteService.
public class QuoteApiClient
{
    private const string BaseUrl = "https://zenquotes.io/api/quotes";
    private static readonly HttpClient Client;

    static QuoteApiClient()
    {
        Client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        // API из некоторых сетей отвечает медленно (~18с) и режет «ботов» — представляемся браузером.
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<List<Quote>> FetchAsync(string? apiKey = null, CancellationToken cancellationToken = default)
    {
        // С ключом ZenQuotes принимает его как сегмент пути: /api/quotes/<key>. Без ключа — обычный лимитированный эндпоинт.
        string url = string.IsNullOrWhiteSpace(apiKey) ? BaseUrl : $"{BaseUrl}/{apiKey.Trim()}";

        using var response = await Client.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();

        string content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!content.TrimStart().StartsWith('['))
            return new List<Quote>();

        var dtos = JsonSerializer.Deserialize<List<ApiQuote>>(content);
        if (dtos is null)
            return new List<Quote>();

        return dtos
            .Where(d => !string.IsNullOrWhiteSpace(d.Text)
                        && !string.Equals(d.Author, "zenquotes.io", StringComparison.OrdinalIgnoreCase))
            .Select(d => new Quote
            {
                Text = d.Text!.Trim(),
                Author = string.IsNullOrWhiteSpace(d.Author) ? "Unknown" : d.Author!.Trim()
            })
            .ToList();
    }

    private sealed record ApiQuote
    {
        [JsonPropertyName("q")] public string? Text { get; init; }
        [JsonPropertyName("a")] public string? Author { get; init; }
    }
}

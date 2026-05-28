using ZenQuotesApp.Wpf.Data;
using ZenQuotesApp.Wpf.Models;

namespace ZenQuotesApp.Wpf.Services;

public enum QuoteSource
{
    Api,
    Database,
    Backup
}

public sealed class LoadResult
{
    public required IReadOnlyList<Quote> Quotes { get; init; }
    public required QuoteSource Source { get; init; }
    public int AddedToDatabase { get; init; }
    public string? ApiError { get; init; }
}

// Оркестрация загрузки: сначала API → при неудаче БД → если БД пуста, backup.
// Backup намеренно НЕ пишется в БД, поэтому база содержит только реальные данные.
public class QuoteService
{
    // Пауза между запросами без ключа — лимит ZenQuotes ~5 запросов за 30 секунд на IP.
    private static readonly TimeSpan KeylessPause = TimeSpan.FromSeconds(6);

    private readonly QuoteRepository _repository;
    private readonly QuoteApiClient _apiClient;

    public QuoteService(QuoteRepository repository, QuoteApiClient apiClient)
    {
        _repository = repository;
        _apiClient = apiClient;
    }

    public async Task<LoadResult> LoadAsync(
        int batchCount,
        string? apiKey = null,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (batchCount < 1)
            batchCount = 1;

        bool keyless = string.IsNullOrWhiteSpace(apiKey);
        var collected = new Dictionary<string, Quote>();
        string? apiError = null;

        try
        {
            for (int i = 0; i < batchCount; i++)
            {
                progress?.Report(batchCount > 1
                    ? $"Загрузка из API: запрос {i + 1} из {batchCount}..."
                    : "Загрузка из API...");

                var batch = await _apiClient.FetchAsync(apiKey, cancellationToken);
                foreach (var quote in batch)
                    collected.TryAdd(quote.Text, quote);

                if (keyless && i < batchCount - 1)
                {
                    progress?.Report($"Пауза перед следующим запросом (готово {i + 1} из {batchCount})...");
                    await Task.Delay(KeylessPause, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Сетевые/таймаут ошибки или отмена: используем то, что успели собрать до сбоя.
            apiError = ex.Message;
        }

        // БД-операции без токена: даже при отмене сохраняем то, что успели скачать.
        if (collected.Count > 0)
        {
            var fetched = collected.Values.ToList();
            int added = await Task.Run(() => _repository.UpsertMany(fetched));
            var all = await Task.Run(() => _repository.GetAll());
            return new LoadResult { Quotes = all, Source = QuoteSource.Api, AddedToDatabase = added, ApiError = apiError };
        }

        apiError ??= "API не вернул цитат (возможна блокировка/лимит).";

        var fromDb = await Task.Run(() => _repository.GetAll());
        if (fromDb.Count > 0)
            return new LoadResult { Quotes = fromDb, Source = QuoteSource.Database, ApiError = apiError };

        return new LoadResult { Quotes = BackupQuotes.Get(), Source = QuoteSource.Backup, ApiError = apiError };
    }
}

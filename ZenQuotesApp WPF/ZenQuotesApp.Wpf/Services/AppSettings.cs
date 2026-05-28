namespace ZenQuotesApp.Wpf.Services;

public class AppSettings
{
    // Сколько запросов к API делать за одно обновление (каждый возвращает ~50 цитат).
    public int BatchCount { get; set; } = 3;

    // Необязательный платный ключ ZenQuotes. Пусто => работаем без ключа (с паузами между запросами).
    public string ApiKey { get; set; } = string.Empty;
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ZenQuotesApp
{
    public class Quote
    {
        [JsonPropertyName("q")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("a")]
        public string Author { get; set; } = string.Empty;
    }

    class Program
    {
        private static readonly HttpClient client = new HttpClient();
        private static readonly Random rnd = new Random();
        private static List<Quote> quotes = new List<Quote>();
        private const string DataFile = "quotes_storage.json";

        static async Task Main(string[] args)
        {
            // Чтоб сайт не думал что мы бот
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            Console.WriteLine(">> Консольный генератор цитат <<");
            Console.WriteLine("Добро пожаловать!");

            await LoadQuotes();

            while (true)
            {
                Console.WriteLine("\nВыберите действие:");
                Console.WriteLine("1. Показать случайную цитату");
                Console.WriteLine("2. Найти цитату по ключевому слову");
                Console.WriteLine("3. Игра: Угадай автора цитаты");
                Console.WriteLine("4. Принудительно обновить список цитат с сайта");
                Console.WriteLine("q. Выход");
                Console.Write("Ваш выбор: ");

                string? choice = Console.ReadLine()?.Trim().ToLower();

                if (choice == "q")
                {
                    Console.WriteLine("\nСпасибо за использование программы! Хорошего дня!");
                    break;
                }

                switch (choice)
                {
                    case "1":
                        ShowRandomQuote();
                        break;
                    case "2":
                        SearchQuotes();
                        break;
                    case "3":
                        await GuessAuthorGame();
                        break;
                    case "4":
                        await RefreshQuotes();
                        break;
                    default:
                        Console.WriteLine("Неверный ввод. Попробуйте еще раз.");
                        break;
                }
            }
        }

        // Загружаем сохраненные цитаты или качаем новые если ничего нет
        private static async Task LoadQuotes()
        {
            if (File.Exists(DataFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(DataFile);
                    quotes = JsonSerializer.Deserialize<List<Quote>>(json) ?? new List<Quote>();
                }
                catch
                {
                    Console.WriteLine("Предупреждение: Файл хранилища поврежден. Будет создан новый.");
                }
            }

            if (quotes.Count == 0)
            {
                Console.WriteLine("Локальное хранилище пусто. Загрузка цитат из сети...");
                await DownloadAndSave();
            }
            else
            {
                Console.WriteLine($"Успешно загружено {quotes.Count} цитат из локального хранилища.");
            }
        }

        // Принудительно качаем свежие цитаты с сайта
        private static async Task RefreshQuotes()
        {
            Console.WriteLine("\n=== ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ ЦИТАТ ===");
            Console.WriteLine("Вы действительно хотите обновить список цитат с сайта?");
            Console.WriteLine("Это заменит текущие сохраненные цитаты новыми.");
            Console.Write("Продолжить? (y/n): ");

            string? confirmation = Console.ReadLine()?.Trim().ToLower();

            if (confirmation != "y" && confirmation != "yes")
            {
                Console.WriteLine("Обновление отменено.");
                return;
            }

            Console.WriteLine("\nНачинаю загрузку свежих цитат с сайта...");

            var freshQuotes = new List<Quote>();

            try
            {
                await FetchQuotesFromApi(freshQuotes);

                if (freshQuotes.Count > 0)
                {
                    quotes = freshQuotes;
                    string jsonString = JsonSerializer.Serialize(quotes, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(DataFile, jsonString);

                    Console.WriteLine($"Обновление успешно завершено!");
                    Console.WriteLine($"Загружено и сохранено {quotes.Count} цитат.");
                    Console.WriteLine("Старые цитаты были заменены новыми.");
                }
                else
                {
                    Console.WriteLine("Не удалось загрузить новые цитаты с сайта.");
                    Console.WriteLine("Возможные причины:");
                    Console.WriteLine("  - Временная недоступность API");
                    Console.WriteLine("  - Блокировка вашего IP-адреса");
                    Console.WriteLine("  - Отсутствие интернет-соединения");
                    Console.WriteLine("\nТекущие цитаты не были изменены.");

                    Console.Write("\nЗагрузить резервный пул цитат? (y/n): ");
                    string? useFallback = Console.ReadLine()?.Trim().ToLower();
                    if (useFallback == "y" || useFallback == "yes")
                    {
                        var backupQuotes = new List<Quote>();
                        LoadBackupQuotes(backupQuotes);

                        if (backupQuotes.Count > 0)
                        {
                            quotes = backupQuotes;
                            string jsonString = JsonSerializer.Serialize(quotes, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(DataFile, jsonString);
                            Console.WriteLine($"✅ Загружено {quotes.Count} резервных цитат.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обновлении: {ex.Message}");
                Console.WriteLine("Текущие цитаты не были изменены.");
            }

            Console.WriteLine("\nНажмите любую клавишу для продолжения...");
            Console.ReadKey();
        }

        // Качаем цитаты из интернета и сохраняем
        private static async Task DownloadAndSave()
        {
            var downloadedQuotes = new List<Quote>();

            try
            {
                await FetchQuotesFromApi(downloadedQuotes);

                if (downloadedQuotes.Count == 0)
                {
                    Console.WriteLine("Не удалось получить данные из API (вероятно поможет VPN или смена IP адреса).");
                    LoadBackupQuotes(downloadedQuotes);
                }

                quotes = downloadedQuotes;

                string jsonString = JsonSerializer.Serialize(quotes, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(DataFile, jsonString);

                Console.WriteLine($"Пул готов. {quotes.Count} цитат сохранено в '{DataFile}'.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Непредвиденная ошибка: {ex.Message}");
                LoadBackupQuotes(quotes);
            }
        }

        // Собственно запрос к API за одной пачкой цитат, пытался сделать в два этапа по 50 цитат, чтобы получить 100, но ловлю непонятный бан сайта
        private static async Task FetchQuotesFromApi(List<Quote> targetList)
        {
            try
            {
                HttpResponseMessage response = await client.GetAsync("https://zenquotes.io/api/quotes");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"API вернул статус: {response.StatusCode}. Доступ ограничен.");
                    return;
                }

                string content = await response.Content.ReadAsStringAsync();

                // Проверяем что нам действительно прислали JSON, а не другие данные
                if (!content.Trim().StartsWith("["))
                {
                    Console.WriteLine("Сайт вернул некорректный формат данных (заставил проходить капчу или заблокировал запрос).");
                    return;
                }

                var batch = JsonSerializer.Deserialize<List<Quote>>(content);
                if (batch != null)
                {
                    int addedCount = 0;
                    foreach (var item in batch)
                    {
                        if (!targetList.Any(q => q.Text == item.Text))
                        {
                            targetList.Add(item);
                            addedCount++;
                        }
                    }
                    if (addedCount > 0)
                        Console.WriteLine($"Добавлено {addedCount} новых уникальных цитат.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при чтении порции данных: {ex.Message}");
            }
        }

        // Запасной вариант - встроенные цитаты на случай если сайт недоступен
        private static void LoadBackupQuotes(List<Quote> targetList)
        {
            Console.WriteLine("Загрузка встроенного резервного пула цитат для обеспечения работы программы...");

            var backupQuotes = new List<Quote>
            {
                new Quote { Text = "The only true wisdom is in knowing you know nothing.", Author = "Socrates" },
                new Quote { Text = "Beware the barrenness of a busy life.", Author = "Socrates" },
                new Quote { Text = "To be is to do.", Author = "Socrates" },
                new Quote { Text = "An unexamined life is not worth living.", Author = "Socrates" },
                new Quote { Text = "The mind is everything. What you think you become.", Author = "Buddha" },
                new Quote { Text = "Peace comes from within. Do not seek it without.", Author = "Buddha" },
                new Quote { Text = "The root of suffering is attachment.", Author = "Buddha" },
                new Quote { Text = "Arise, awake and stop not till the goal is reached.", Author = "Swami Vivekananda" }
            };

            foreach (var q in backupQuotes)
            {
                if (!targetList.Any(item => item.Text == q.Text))
                    targetList.Add(q);
            }
        }

        // Показываем рандомную цитату
        private static void ShowRandomQuote()
        {
            if (quotes.Count == 0)
            {
                Console.WriteLine("Нет цитат для отображения");
                return;
            }

            int index = rnd.Next(quotes.Count);
            var quote = quotes[index];
            Console.WriteLine($"\n\"{quote.Text}\"");
            Console.WriteLine($"— {quote.Author}");
        }

        // Поиск цитат по слову
        private static void SearchQuotes()
        {
            Console.Write("Введите слово для поиска (на английском): ");
            string? keyword = Console.ReadLine()?.Trim().ToLower();

            if (string.IsNullOrEmpty(keyword))
            {
                Console.WriteLine("Ключевое слово не может быть пустым.");
                return;
            }

            var found = quotes.Where(q => q.Text.ToLower().Contains(keyword)).ToList();

            if (found.Count == 0)
            {
                Console.WriteLine($"Цитат со словом '{keyword}' в хранилище не найдено.");
                return;
            }

            Console.WriteLine($"\nНайдено цитат: {found.Count}");
            Console.WriteLine(new string('=', 60));

            for (int i = 0; i < found.Count; i++)
            {
                Console.WriteLine($"{i + 1}. \"{found[i].Text}\"");
                Console.WriteLine($"   — {found[i].Author}");
                Console.WriteLine();
            }
        }

        // Игра - угадай автора
        private static async Task GuessAuthorGame()
        {
            if (quotes.Count < 2)
            {
                Console.WriteLine("Недостаточно цитат для игры.");
                return;
            }

            var targetQuote = quotes[rnd.Next(quotes.Count)];

            var authorsList = quotes
                .Select(q => q.Author)
                .Distinct()
                .Where(a => a != targetQuote.Author)
                .OrderBy(x => rnd.Next())
                .Take(3)
                .ToList();

            authorsList.Add(targetQuote.Author);
            var shuffled = authorsList.OrderBy(x => rnd.Next()).ToList();

            Console.WriteLine($"\nКому принадлежит цитата: \"{targetQuote.Text}\"");
            for (int i = 0; i < shuffled.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {shuffled[i]}");
            }

            Console.Write("Ваш ответ (цифра): ");
            if (int.TryParse(Console.ReadLine(), out int userChoice) && userChoice > 0 && userChoice <= shuffled.Count)
            {
                if (shuffled[userChoice - 1] == targetQuote.Author)
                {
                    Console.WriteLine("Правильно! Отличный результат.");
                }
                else
                {
                    Console.WriteLine($"Неверно. Правильный ответ: {targetQuote.Author}");
                }
            }
            else
            {
                Console.WriteLine("Некорректный ввод. Игра окончена.");
            }

            await Task.CompletedTask;
        }
    }
}
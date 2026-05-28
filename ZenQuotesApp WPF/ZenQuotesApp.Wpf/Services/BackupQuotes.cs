using ZenQuotesApp.Wpf.Models;

namespace ZenQuotesApp.Wpf.Services;

// Встроенный резервный пул на случай, когда API недоступен, а БД пуста. В БД не сохраняется.
public static class BackupQuotes
{
    public static List<Quote> Get() => new()
    {
        new Quote { Text = "The only true wisdom is in knowing you know nothing.", Author = "Socrates" },
        new Quote { Text = "Beware the barrenness of a busy life.", Author = "Socrates" },
        new Quote { Text = "To be is to do.", Author = "Socrates" },
        new Quote { Text = "An unexamined life is not worth living.", Author = "Socrates" },
        new Quote { Text = "The mind is everything. What you think you become.", Author = "Buddha" },
        new Quote { Text = "Peace comes from within. Do not seek it without.", Author = "Buddha" },
        new Quote { Text = "The root of suffering is attachment.", Author = "Buddha" },
        new Quote { Text = "Arise, awake and stop not till the goal is reached.", Author = "Swami Vivekananda" },
    };
}

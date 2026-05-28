using System.IO;
using Microsoft.Data.Sqlite;
using ZenQuotesApp.Wpf.Models;

namespace ZenQuotesApp.Wpf.Data;

// Хранилище цитат в SQLite. Дедуп обеспечивается UNIQUE-индексом по тексту цитаты.
public class QuoteRepository
{
    private readonly string _connectionString;

    public QuoteRepository(string dbPath)
    {
        string? dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();
        Initialize();
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS Quotes (
                Id     INTEGER PRIMARY KEY AUTOINCREMENT,
                Text   TEXT NOT NULL UNIQUE,
                Author TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public int Count()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Quotes;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public List<Quote> GetAll()
    {
        var result = new List<Quote>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Text, Author FROM Quotes ORDER BY Id;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(new Quote { Text = reader.GetString(0), Author = reader.GetString(1) });
        return result;
    }

    // Вставляет только новые цитаты (INSERT OR IGNORE по UNIQUE Text). Возвращает число реально добавленных.
    public int UpsertMany(IEnumerable<Quote> quotes)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT OR IGNORE INTO Quotes (Text, Author) VALUES (@text, @author);";
        var textParam = cmd.Parameters.Add("@text", SqliteType.Text);
        var authorParam = cmd.Parameters.Add("@author", SqliteType.Text);

        int inserted = 0;
        foreach (var quote in quotes)
        {
            if (string.IsNullOrWhiteSpace(quote.Text))
                continue;
            textParam.Value = quote.Text;
            authorParam.Value = string.IsNullOrWhiteSpace(quote.Author) ? "Unknown" : quote.Author;
            inserted += cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        return inserted;
    }
}

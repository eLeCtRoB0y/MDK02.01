using System.Collections.ObjectModel;
using System.Windows.Input;
using ZenQuotesApp.Wpf.Models;
using ZenQuotesApp.Wpf.Services;

namespace ZenQuotesApp.Wpf.ViewModels;

public enum Section
{
    Home,
    Search,
    Game,
    Library,
    Settings
}

public class MainViewModel : ViewModelBase
{
    private readonly QuoteService _service;
    private readonly SettingsStore _settingsStore;
    private readonly Random _rnd = new();
    private CancellationTokenSource? _cts;

    public MainViewModel(QuoteService service, SettingsStore settingsStore)
    {
        _service = service;
        _settingsStore = settingsStore;

        var settings = _settingsStore.Load();
        _batchCount = settings.BatchCount;
        _apiKey = settings.ApiKey;

        NavigateCommand = new RelayCommand(Navigate);
        NextRandomCommand = new RelayCommand(_ => PickRandom(), _ => Library.Count > 0);
        SearchCommand = new RelayCommand(RunSearch);
        RefreshCommand = new RelayCommand(async _ => await LoadAsync(BatchCount), _ => !IsLoading);
        AnswerCommand = new RelayCommand(Answer, _ => CanAnswer);
        NewGameCommand = new RelayCommand(StartNewGame, _ => Library.Count > 0);
        SaveSettingsCommand = new RelayCommand(_ => SaveSettings());
        CancelCommand = new RelayCommand(_ => _cts?.Cancel(), _ => IsLoading);
    }

    public ObservableCollection<Quote> Library { get; } = new();
    public ObservableCollection<Quote> SearchResults { get; } = new();
    public ObservableCollection<string> GameOptions { get; } = new();

    public ICommand NavigateCommand { get; }
    public ICommand NextRandomCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AnswerCommand { get; }
    public ICommand NewGameCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand CancelCommand { get; }

    private Section _currentSection = Section.Home;
    public Section CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (SetProperty(ref _currentSection, value))
            {
                OnPropertyChanged(nameof(IsHome));
                OnPropertyChanged(nameof(IsSearch));
                OnPropertyChanged(nameof(IsGame));
                OnPropertyChanged(nameof(IsLibrary));
                OnPropertyChanged(nameof(IsSettings));
            }
        }
    }

    public bool IsHome => CurrentSection == Section.Home;
    public bool IsSearch => CurrentSection == Section.Search;
    public bool IsGame => CurrentSection == Section.Game;
    public bool IsLibrary => CurrentSection == Section.Library;
    public bool IsSettings => CurrentSection == Section.Settings;

    private Quote? _currentRandom;
    public Quote? CurrentRandom
    {
        get => _currentRandom;
        private set => SetProperty(ref _currentRandom, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    private string _searchStatus = string.Empty;
    public string SearchStatus
    {
        get => _searchStatus;
        private set => SetProperty(ref _searchStatus, value);
    }

    private Quote? _gameQuote;
    public Quote? GameQuote
    {
        get => _gameQuote;
        private set
        {
            if (SetProperty(ref _gameQuote, value))
                OnPropertyChanged(nameof(HasGameQuote));
        }
    }

    public bool HasGameQuote => GameQuote is not null;

    private string _gameFeedback = string.Empty;
    public string GameFeedback
    {
        get => _gameFeedback;
        private set => SetProperty(ref _gameFeedback, value);
    }

    private bool _gameAnswered;
    public bool GameAnswered
    {
        get => _gameAnswered;
        private set
        {
            if (SetProperty(ref _gameAnswered, value))
                OnPropertyChanged(nameof(CanAnswer));
        }
    }

    public bool CanAnswer => HasGameQuote && !GameAnswered;

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _sourceLabel = "Источник: —";
    public string SourceLabel
    {
        get => _sourceLabel;
        private set => SetProperty(ref _sourceLabel, value);
    }

    private string _statusDetail = string.Empty;
    public string StatusDetail
    {
        get => _statusDetail;
        private set => SetProperty(ref _statusDetail, value);
    }

    private int _quoteCount;
    public int QuoteCount
    {
        get => _quoteCount;
        private set => SetProperty(ref _quoteCount, value);
    }

    private int _batchCount;
    public int BatchCount
    {
        get => _batchCount;
        set => SetProperty(ref _batchCount, value < 1 ? 1 : value);
    }

    private string _apiKey = string.Empty;
    public string ApiKey
    {
        get => _apiKey;
        set => SetProperty(ref _apiKey, value);
    }

    private string _settingsStatus = string.Empty;
    public string SettingsStatus
    {
        get => _settingsStatus;
        private set => SetProperty(ref _settingsStatus, value);
    }

    public Task InitializeAsync() => LoadAsync(1);

    private async Task LoadAsync(int batchCount)
    {
        if (IsLoading)
            return;

        _cts = new CancellationTokenSource();
        IsLoading = true;
        StatusDetail = "Загрузка из API...";
        try
        {
            var progress = new Progress<string>(message => StatusDetail = message);
            var result = await _service.LoadAsync(batchCount, ApiKey, progress, _cts.Token);
            ApplyResult(result);

            if (_cts.IsCancellationRequested)
                StatusDetail = $"Загрузка отменена. В библиотеке цитат: {QuoteCount}";
        }
        finally
        {
            IsLoading = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private void ApplyResult(LoadResult result)
    {
        Library.Clear();
        foreach (var quote in result.Quotes)
            Library.Add(quote);
        QuoteCount = Library.Count;

        SourceLabel = result.Source switch
        {
            QuoteSource.Api => "Источник: API",
            QuoteSource.Database => "Источник: база данных",
            QuoteSource.Backup => "Источник: резервные",
            _ => "Источник: —"
        };
        StatusDetail = result.Source switch
        {
            QuoteSource.Api => result.AddedToDatabase > 0
                ? $"Загружено из API, добавлено новых: {result.AddedToDatabase}"
                : "Загружено из API, новых цитат нет",
            QuoteSource.Database => "Показаны цитаты из базы",
            QuoteSource.Backup => "Резервный пул (API недоступен или база пуста)",
            _ => string.Empty
        };

        PickRandom();
        StartNewGame(null);
        RunSearch(null);
    }

    private void Navigate(object? parameter)
    {
        if (parameter is string name && Enum.TryParse<Section>(name, out var section))
            CurrentSection = section;
    }

    private void PickRandom()
    {
        if (Library.Count == 0)
        {
            CurrentRandom = null;
            return;
        }
        if (Library.Count == 1)
        {
            CurrentRandom = Library[0];
            return;
        }

        Quote next;
        do
        {
            next = Library[_rnd.Next(Library.Count)];
        }
        while (ReferenceEquals(next, CurrentRandom));
        CurrentRandom = next;
    }

    private void RunSearch(object? parameter)
    {
        SearchResults.Clear();
        string term = SearchText.Trim();
        if (term.Length == 0)
        {
            SearchStatus = "Введите слово или имя автора для поиска.";
            return;
        }

        foreach (var quote in Library.Where(q =>
                     q.Text.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                     q.Author.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            SearchResults.Add(quote);
        }

        SearchStatus = SearchResults.Count == 0
            ? $"По запросу «{term}» ничего не найдено."
            : $"Найдено: {SearchResults.Count}";
    }

    private void StartNewGame(object? parameter)
    {
        GameFeedback = string.Empty;
        GameAnswered = false;
        GameOptions.Clear();
        GameQuote = null;

        var distinctAuthors = Library.Select(q => q.Author).Distinct().ToList();
        if (distinctAuthors.Count < 2)
        {
            GameFeedback = "Недостаточно разных авторов для игры.";
            return;
        }

        var target = Library[_rnd.Next(Library.Count)];

        var options = distinctAuthors
            .Where(a => a != target.Author)
            .OrderBy(_ => _rnd.Next())
            .Take(3)
            .ToList();
        options.Add(target.Author);

        foreach (var author in options.OrderBy(_ => _rnd.Next()))
            GameOptions.Add(author);

        GameQuote = target;
    }

    private void Answer(object? parameter)
    {
        if (!CanAnswer || GameQuote is null)
            return;

        GameAnswered = true;
        string? chosen = parameter as string;
        GameFeedback = chosen == GameQuote.Author
            ? "Верно!"
            : $"Неверно. Правильный ответ: {GameQuote.Author}";
    }

    private void SaveSettings()
    {
        _settingsStore.Save(new AppSettings { BatchCount = BatchCount, ApiKey = ApiKey.Trim() });
        SettingsStatus = "Настройки сохранены.";
    }
}

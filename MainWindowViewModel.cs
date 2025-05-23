using System;
using System.Collections.ObjectModel; // Для списку торентів, що динамічно оновлюється
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls; // Для доступу до TopLevel для FilePicker
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage; // Для FilePicker
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TorrentFlow.TorrentClientLibrary;
using TorrentFlow.TorrentClientLibrary.Extensions; // Ваша бібліотека
using Avalonia.Styling;

namespace TorrentFlow
{
    public partial class MainWindowViewModel : ObservableObject
    {
        // Поля для роботи з торент-клієнтом
        private ThrottlingManager _throttlingManager;
        private ObservableCollection<TorrentTransferViewModel> _torrents; // Список торентів для відображення

        private AppSettings _currentSettings;
        
        // Константу DefaultListeningPort можна видалити, якщо вона більше ніде не використовується,
        // або залишити для значень за замовчуванням, якщо _currentSettings ще не ініціалізовано.
        // Однак, _currentSettings.ListeningPort вже має значення за замовчуванням в AppSettings.

        public ObservableCollection<TorrentTransferViewModel> Torrents
        {
            get => _torrents;
            set => SetProperty(ref _torrents, value);
        }

        public MainWindowViewModel()
        {
            _currentSettings = SettingsManager.LoadSettings(); // Завантажуємо налаштування
            ApplySettings(); // Застосовуємо завантажені налаштування
            
            _throttlingManager = new ThrottlingManager();
            UpdateThrottlingManagerLimits(); // Встановлюємо ліміти з налаштувань

            Torrents = new ObservableCollection<TorrentTransferViewModel>();

            // Ініціалізуємо команди
            AddTorrentCommand = new AsyncRelayCommand(AddTorrentAsync);
            OpenSettingsCommand = new RelayCommand(OpenSettingsWindow); // Нова команда
        }
        
        private void ApplySettings()
        {
            if (Application.Current != null && _currentSettings.SelectedTheme != null)
            {
                Application.Current.RequestedThemeVariant = _currentSettings.SelectedTheme;
            }
        }

        private void UpdateThrottlingManagerLimits()
        {
            if (_throttlingManager != null && _currentSettings != null)
            {
                _throttlingManager.ReadSpeedLimit = _currentSettings.MaxDownloadSpeed > 0 ? _currentSettings.MaxDownloadSpeed : long.MaxValue;
                _throttlingManager.WriteSpeedLimit = _currentSettings.MaxUploadSpeed > 0 ? _currentSettings.MaxUploadSpeed : long.MaxValue;
            }
        }

        // Команда для додавання нового торента
        public IAsyncRelayCommand AddTorrentCommand { get; }
        public IRelayCommand OpenSettingsCommand { get; } // Нова команда
        
        private async Task AddTorrentAsync()
        {
            // Отримуємо TopLevel, необхідний для FilePicker
            var topLevel = GetTopLevel();
            if (topLevel == null) return;

            // Відкриваємо діалог вибору файлу
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Оберіть .torrent файл",
                AllowMultiple = false,
                FileTypeFilter = new[] { new FilePickerFileType("Torrent Files") { Patterns = new[] { "*.torrent" } } }
            });

            if (files.Count >= 1)
            {
                string torrentFilePath = files[0].Path.LocalPath; 

                if (TorrentInfo.TryLoad(torrentFilePath, out TorrentInfo torrentInfo)) 
                {
                    // Використовуємо шлях з налаштувань
                    string downloadDirectory = Path.Combine(_currentSettings.DownloadPath, torrentInfo.InfoHash); 
                    
                    try
                    {
                        // Спочатку перевіряємо та створюємо базову директорію завантажень, якщо її немає
                        string baseDownloadDir = _currentSettings.DownloadPath;
                        if (!Directory.Exists(baseDownloadDir))
                        {
                            Directory.CreateDirectory(baseDownloadDir);
                        }
                        // Потім створюємо піддиректорію для конкретного торента
                        Directory.CreateDirectory(downloadDirectory);
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка створення директорії {downloadDirectory}: {ex.Message}");
                        // Тут можна показати повідомлення користувачу про помилку створення директорії
                        return;
                    }

                    // Перевірка вільного місця на диску
                    try
                    {
                        string rootDownloadPath = Path.GetPathRoot(downloadDirectory);
                        if (!string.IsNullOrEmpty(rootDownloadPath))
                        {
                             DriveInfo drive = new DriveInfo(rootDownloadPath);
                             if (drive.AvailableFreeSpace < torrentInfo.Length)
                             {
                                 System.Diagnostics.Debug.WriteLine($"Недостатньо місця на диску {drive.Name}. Потрібно: {torrentInfo.Length.ToBytes()}, Доступно: {drive.AvailableFreeSpace.ToBytes()}");
                                 // Показати користувачеві повідомлення про помилку (наприклад, через діалогове вікно)
                                 // Можна створити сервіс для показу повідомлень або використати MessageBox.Avalonia
                                 return;
                             }
                        }
                    }
                    catch (System.Exception ex) 
                    {
                        System.Diagnostics.Debug.WriteLine($"Помилка перевірки вільного місця для шляху '{downloadDirectory}': {ex.Message}");
                        // Показати користувачеві повідомлення про помилку
                        return;
                    }

                    PersistenceManager persistenceManager = new PersistenceManager(
                        downloadDirectory,
                        torrentInfo.Length, 
                        torrentInfo.PieceLength, 
                        torrentInfo.PieceHashes, 
                        torrentInfo.Files); 

                    TransferManager transferManager = new TransferManager(
                        _currentSettings.ListeningPort, // ВИПРАВЛЕНО: Використовуємо порт з налаштувань
                        torrentInfo,
                        _throttlingManager,
                        persistenceManager); 

                    var torrentViewModel = new TorrentTransferViewModel(transferManager);
                    Torrents.Add(torrentViewModel);

                    transferManager.Start(); 
                }
                else
                {
                    Console.WriteLine($"Помилка завантаження файлу: {torrentFilePath}");
                }
            }
        }
        private void OpenSettingsWindow()
        {
            // Передаємо null як ownerWindow, оскільки SettingsViewModel тепер сам обробляє закриття
            // і йому не обов'язково знати про батьківське вікно для цього.
            // Однак, для ShowDialog краще передати батьківське вікно.
            var owner = GetTopLevel() as Window;
            var settingsViewModel = new SettingsViewModel(_currentSettings, null); // Спочатку null
            var settingsWindow = new SettingsWindow(settingsViewModel);
            
            // Якщо ми хочемо, щоб SettingsViewModel міг закрити вікно, передаємо йому посилання на вікно
            // Це можна зробити, якщо SettingsViewModel приймає Window у конструкторі
            // settingsViewModel.OwnerWindow = settingsWindow; // Якщо є така властивість у SettingsViewModel

            if (owner != null)
            {
                settingsWindow.ShowDialog(owner); 
            }
            else
            {
                settingsWindow.Show();
            }
            // Після закриття вікна налаштувань, оновлюємо ліміти швидкості
            UpdateThrottlingManagerLimits();
            // Також, якщо тема змінилася, її вже застосував SettingsViewModel,
            // але якщо потрібно оновити щось ще в головному вікні, робимо це тут.
        }
        
        // Допоміжний метод для отримання TopLevel (вікна або контролу найвищого рівня)
        private TopLevel GetTopLevel()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }
    }

    // Додатковий ViewModel для представлення одного торента в списку
    public partial class TorrentTransferViewModel : ObservableObject
    {
        private readonly TransferManager _transferManager;
        private System.Timers.Timer _updateTimer;

        [ObservableProperty]
        private string _torrentName;

        [ObservableProperty]
        private double _progress; // Від 0.0 до 100.0

        [ObservableProperty]
        private string _status; // Наприклад, "Hashing", "Leeching", "Seeding", "Stopped"

        [ObservableProperty]
        private string _downloadSpeed;

        [ObservableProperty]
        private string _uploadSpeed;

        [ObservableProperty]
        private string _downloadedSize;

        [ObservableProperty]
        private string _totalSize;

        [ObservableProperty]
        private int _seeders;

        [ObservableProperty]
        private int _leechers;


        public TorrentTransferViewModel(TransferManager transferManager)
        {
            _transferManager = transferManager;
            // Спробуємо отримати назву з TorrentInfo.Name, якщо є, інакше з першого файлу
            _torrentName = !string.IsNullOrEmpty(_transferManager.TorrentInfo.Comment) // Використовуємо Comment як назву, якщо є
                           ? _transferManager.TorrentInfo.Comment
                           : _transferManager.TorrentInfo.Files.FirstOrDefault()?.FilePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).LastOrDefault() ?? "Unknown Torrent";
            _totalSize = _transferManager.TorrentInfo.Length.ToBytes(); 

            // Підписуємося на події від TransferManager для оновлення UI
            _transferManager.TorrentHashing += (s, e) => Status = "Хешування"; 
            _transferManager.TorrentLeeching += (s, e) => Status = "Завантаження"; 
            _transferManager.TorrentSeeding += (s, e) => Status = "Роздача"; 
            _transferManager.TorrentStarted += (s, e) => Status = "Запущено"; 
            _transferManager.TorrentStopped += (s, e) => Status = "Зупинено"; 

            // Таймер для періодичного оновлення інформації про швидкість, прогрес і т.д.
            _updateTimer = new System.Timers.Timer(1000); // Оновлення щосекунди
            _updateTimer.Elapsed += UpdateTorrentProgress;
            _updateTimer.AutoReset = true;
            _updateTimer.Enabled = true;

            UpdateTorrentProgress(null, null); // Початкове оновлення
        }

        private void UpdateTorrentProgress(object sender, System.Timers.ElapsedEventArgs e)
        {
            // Оновлюємо властивості, які будуть відображатися в UI
            // Потрібно забезпечити, щоб оновлення відбувалося в UI потоці,
            // Avalonia використовує Dispatcher.UIThread.Post або Dispatcher.UIThread.InvokeAsync
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_transferManager == null || _transferManager.IsDisposed) 
                {
                    _updateTimer?.Stop();
                    return;
                }

                Progress = (double)_transferManager.CompletedPercentage * 100; 
                DownloadSpeed = _transferManager.DownloadSpeed.ToBytes() + "/s"; 
                UploadSpeed = _transferManager.UploadSpeed.ToBytes() + "/s"; 
                DownloadedSize = _transferManager.Downloaded.ToBytes(); 
                Seeders = _transferManager.SeedingPeerCount; 
                Leechers = _transferManager.LeechingPeerCount; 

                // Оновлення статусу, якщо він не був встановлений подіями
                // Додаємо перевірку на _transferManager.pieceManager, оскільки він може бути null, якщо торент ще не стартував повністю
                if (string.IsNullOrEmpty(Status) && _transferManager.pieceManager != null)
                {
                     if (_transferManager.pieceManager.IsComplete) Status = "Роздача"; 
                     else Status = "Завантаження";
                }
                else if (string.IsNullOrEmpty(Status)) // Якщо pieceManager ще не створений, але торент доданий
                {
                    Status = "Очікування...";
                }
            });
        }

        // Команди для керування конкретним торентом (Start, Stop, Pause, Remove)
        // Наприклад:
        [RelayCommand]
        private void StopTorrent()
        {
            if (_transferManager != null && !_transferManager.IsDisposed)
            {
                _transferManager.Dispose(); // Змінено Stop() на Dispose()
            }
            _updateTimer?.Stop();
            Status = "Зупинено";
        }

        [RelayCommand]
        private void StartTorrent()
        {
            // Ця логіка потребує доопрацювання, оскільки TransferManager
            // після Stop() може потребувати перестворення або методу Resume().
            // Поточний TransferManager.Start() ініціалізує багато речей, 
            // включаючи PieceManager, що може бути проблематично для перезапуску.
            //
            // if (_transferManager != null && _transferManager.IsDisposed)
            // {
            //     // Потрібно буде перестворити TransferManager або додати логіку Resume
            // }
            // else if (_transferManager != null)
            // {
            //    // _transferManager.Start(); // Якщо Start() можна викликати повторно
            //    _updateTimer?.Start();
            //    Status = "Запущено"; // Або інший відповідний статус
            // }
             System.Diagnostics.Debug.WriteLine("StartTorrent: Функціонал перезапуску потребує реалізації.");
        }
    }
}
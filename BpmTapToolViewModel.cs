using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace BpmTapTool
{
    // 履歴アイテムモデル
    public class BpmHistoryItem : INotifyPropertyChanged
    {
        private int _id;
        private string? _name;
        private string? _note;
        private bool _isPinned;

        public int Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public DateTime Timestamp { get; set; }
        public int AverageBpm { get; set; }
        public int InstantBpm { get; set; }
        public int TapCount { get; set; }

        public string? Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string? Note
        {
            get => _note;
            set { _note = value; OnPropertyChanged(); }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set { _isPinned = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string DisplayText => $"{Timestamp:HH:mm:ss} | {AverageBpm} BPM (瞬間:{InstantBpm}) | {TapCount}回";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // ViewModel
    public class BpmTapToolViewModel : INotifyPropertyChanged
    {
        private readonly List<DateTime> _tapTimes = new();
        private readonly TimeSpan _interval = TimeSpan.FromMilliseconds(1500);
        private DispatcherTimer _resultUpdateTimer;

        private int _instantBpm;
        public int InstantBpm
        {
            get => _instantBpm;
            private set { _instantBpm = value; OnPropertyChanged(); OnPropertyChanged(nameof(InstantBpmDisplay)); }
        }
        public string InstantBpmDisplay => $"瞬間BPM: {InstantBpm}";

        private int _averageBpm;
        public int AverageBpm
        {
            get => _averageBpm;
            private set { _averageBpm = value; OnPropertyChanged(); OnPropertyChanged(nameof(AverageBpmDisplay)); }
        }
        public string AverageBpmDisplay => $"平均BPM: {AverageBpm}";

        private int _tapCount;
        public int TapCount
        {
            get => _tapCount;
            private set { _tapCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(TapCountDisplay)); }
        }
        public string TapCountDisplay => $"タップ回数: {TapCount}";

        private string _tapButtonText = "🎵 タップしてBPMを測定 🎵";
        public string TapButtonText
        {
            get => _tapButtonText;
            set { _tapButtonText = value; OnPropertyChanged(); }
        }

        private ObservableCollection<BpmHistoryItem> _history = new();
        public ObservableCollection<BpmHistoryItem> History
        {
            get => _history;
            set { _history = value; OnPropertyChanged(); }
        }

        private BpmHistoryItem? _selectedHistoryItem;
        public BpmHistoryItem? SelectedHistoryItem
        {
            get => _selectedHistoryItem;
            set
            {
                if (_selectedHistoryItem != null)
                    _selectedHistoryItem.PropertyChanged -= OnHistoryItemPropertyChanged;
                _selectedHistoryItem = value;
                if (_selectedHistoryItem != null)
                    _selectedHistoryItem.PropertyChanged += OnHistoryItemPropertyChanged;
                OnPropertyChanged();
            }
        }

        public ICommand TapCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand DeleteHistoryCommand { get; }
        public ICommand PinHistoryCommand { get; }

        public BpmTapToolViewModel()
        {
            TapCommand = new RelayCommand(_ => AddTap());
            ResetCommand = new RelayCommand(_ => Reset());
            DeleteHistoryCommand = new RelayCommand(_ => DeleteSelectedHistory(), _ => SelectedHistoryItem != null);
            PinHistoryCommand = new RelayCommand(_ => TogglePinSelectedHistory(), _ => SelectedHistoryItem != null);

            _resultUpdateTimer = new DispatcherTimer { Interval = _interval };
            _resultUpdateTimer.Tick += OnTimedEvent;

            foreach (var item in JsonHistoryHelper.LoadAllHistory())
            {
                item.PropertyChanged += OnHistoryItemPropertyChanged;
                History.Add(item);
            }
        }

        private void OnHistoryItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is BpmHistoryItem item && item.Id > 0)
            {
                if (e.PropertyName == nameof(BpmHistoryItem.Name) ||
                    e.PropertyName == nameof(BpmHistoryItem.Note) ||
                    e.PropertyName == nameof(BpmHistoryItem.IsPinned))
                {
                    JsonHistoryHelper.UpdateHistory(item);
                }
            }
        }

        private void AddTap()
        {
            _resultUpdateTimer.Stop();
            _resultUpdateTimer.Start();

            _tapTimes.Add(DateTime.Now);
            TapCount = _tapTimes.Count;

            if (TapCount > 1 && (_tapTimes.Last() - _tapTimes[_tapTimes.Count - 2]).TotalMilliseconds > 3000)
            {
                Reset(keepLastTap: true);
                _tapTimes.Add(DateTime.Now);
                TapCount = _tapTimes.Count;
            }

            CalculateBpm();
        }

        private void CalculateBpm()
        {
            if (TapCount < 2) return;

            var lastInterval = (_tapTimes.Last() - _tapTimes[_tapTimes.Count - 2]).TotalMilliseconds / 1000.0;
            if (lastInterval > 0)
            {
                var instant = (int)Math.Round(60.0 / lastInterval);
                InstantBpm = Math.Clamp(instant, 40, 300);
            }

            const int maxTapsToUse = 8;
            var tapsToUse = _tapTimes.Skip(Math.Max(0, _tapTimes.Count - maxTapsToUse)).ToList();
            var intervals = new List<double>();
            for (int i = 1; i < tapsToUse.Count; i++)
            {
                intervals.Add((tapsToUse[i] - tapsToUse[i - 1]).TotalMilliseconds / 1000.0);
            }

            if (intervals.Count > 0)
            {
                var avgInterval = intervals.Average();
                var avg = (int)Math.Round(60.0 / avgInterval);
                AverageBpm = Math.Clamp(avg, 40, 300);
            }
        }

        private void AddCurrentToHistory()
        {
            if (TapCount < 2) return;

            var item = new BpmHistoryItem
            {
                Timestamp = DateTime.Now,
                AverageBpm = AverageBpm,
                InstantBpm = InstantBpm,
                TapCount = TapCount,
                Name = $"測定 {History.Count + 1}",
                Note = "",
                IsPinned = false
            };
            item.PropertyChanged += OnHistoryItemPropertyChanged;
            JsonHistoryHelper.AddHistory(item);
            History.Insert(0, item);
        }

        private void Reset(bool keepLastTap = false)
        {
            if (!keepLastTap && TapCount >= 2)
            {
                AddCurrentToHistory();
            }

            _tapTimes.Clear();
            if (!keepLastTap)
            {
                TapCount = 0;
                InstantBpm = 0;
                AverageBpm = 0;
                TapButtonText = "🎵 タップしてBPMを測定 🎵";
            }
            _resultUpdateTimer.Stop();
        }

        private void OnTimedEvent(object? sender, EventArgs e) => Reset();

        private void DeleteSelectedHistory()
        {
            if (SelectedHistoryItem != null && !SelectedHistoryItem.IsPinned)
            {
                JsonHistoryHelper.DeleteHistory(SelectedHistoryItem.Id);
                History.Remove(SelectedHistoryItem);
                SelectedHistoryItem = History.FirstOrDefault();
            }
            else if (SelectedHistoryItem?.IsPinned == true)
            {
                System.Windows.MessageBox.Show("ピン留めされた履歴は削除できません。", "情報", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void TogglePinSelectedHistory()
        {
            if (SelectedHistoryItem != null)
            {
                SelectedHistoryItem.IsPinned = !SelectedHistoryItem.IsPinned;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // ========== RelayCommand の実装 ==========
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
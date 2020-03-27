namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using MaterialDesignThemes.Wpf;
    using OnlyM.Core.Extensions;
    using OnlyM.Models;
    using OnlyM.Services.StartOffsetStorage;

    internal class StartOffsetViewModel : ViewModelBase
    {
        private const int MaxRecentItemsCount = 10;

        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Colors.DarkRed);
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Colors.DarkGreen);

        private readonly IStartOffsetStorageService _startOffsetStorageService;
        private List<int> _recentTimes;

        private int _chosenHours;
        private int _chosenMinutes;
        private int _chosenSeconds;

        private TimeSpan _maxStartTime;
        private string _mediaFileName;
        private int _mediaDurationSeconds;

        public StartOffsetViewModel(IStartOffsetStorageService startOffsetStorageService)
        {
            _startOffsetStorageService = startOffsetStorageService;

            OkCommand = new RelayCommand(Ok);
            CancelCommand = new RelayCommand(Cancel);
            RemoveRecentTimeCommand = new RelayCommand<int>(RemoveRecentTime);
        }

        public TimeSpan? Result { get; private set; }

        public RelayCommand OkCommand { get; set; }

        public RelayCommand CancelCommand { get; set; }

        public RelayCommand<int> RemoveRecentTimeCommand { get; set; }

        public IEnumerable<int> Hours => Enumerable.Range(0, 12);

        public IEnumerable<int> Minutes => Enumerable.Range(0, 59);

        public IEnumerable<int> Seconds => Enumerable.Range(0, 59);

        public bool IsTimeValid
        {
            get
            {
                var chosenTime = new TimeSpan(ChosenHours, ChosenMinutes, ChosenSeconds);
                return chosenTime < _maxStartTime && chosenTime >= TimeSpan.Zero;
            }
        }

        public int ChosenHours
        {
            get => _chosenHours;
            set
            {
                if (_chosenHours != value)
                {
                    _chosenHours = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ChosenTimeAsString));
                    RaisePropertyChanged(nameof(ChosenTimeBrush));
                    RaisePropertyChanged(nameof(IsTimeValid));
                }
            }
        }

        public int ChosenMinutes
        {
            get => _chosenMinutes;
            set
            {
                if (_chosenMinutes != value)
                {
                    _chosenMinutes = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ChosenTimeAsString));
                    RaisePropertyChanged(nameof(ChosenTimeBrush));
                    RaisePropertyChanged(nameof(IsTimeValid));
                }
            }
        }

        public int ChosenSeconds
        {
            get => _chosenSeconds;
            set
            {
                if (_chosenSeconds != value)
                {
                    _chosenSeconds = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ChosenTimeAsString));
                    RaisePropertyChanged(nameof(ChosenTimeBrush));
                    RaisePropertyChanged(nameof(IsTimeValid));
                }
            }
        }

        public string ChosenTimeAsString => GenerateTimeString();

        public Brush ChosenTimeBrush => IsTimeValid ? GreenBrush : RedBrush;

        public bool HasRecentTimes => _recentTimes != null && _recentTimes.Any();

        public IEnumerable<RecentTimesItem> RecentTimes
        {
            get
            {
                var result = new List<RecentTimesItem>();

                if (_recentTimes != null)
                {
                    foreach (var item in _recentTimes)
                    {
                        result.Add(new RecentTimesItem
                        {
                            Seconds = item,
                        });
                    }
                }

                if (result.Any())
                {
                    result.Add(new RecentTimesItem());  // "Clear"
                }

                return result;
            }
        }

        public RecentTimesItem ChosenRecentTime
        {
            get => null;
            set
            {
                if (value != null)
                {
                    if (value.Seconds == 0)
                    {
                        ClearRecentsList();
                    }
                    else
                    {
                        Result = TimeSpan.FromSeconds(value.Seconds);
                        DialogHost.CloseDialogCommand.Execute(null, null);
                    }
                }
            }
        }

        public void Init(string mediaFileName, int mediaDurationSeconds)
        {
            _mediaFileName = mediaFileName;
            _mediaDurationSeconds = mediaDurationSeconds;

            _recentTimes = _startOffsetStorageService.ReadOffsets(mediaFileName, mediaDurationSeconds).ToList();

            _maxStartTime = TimeSpan.FromSeconds(mediaDurationSeconds);
            
            ChosenHours = 0;
            ChosenMinutes = 0;
            ChosenSeconds = 0;

            RaisePropertyChanged(nameof(RecentTimes));
            RaisePropertyChanged(nameof(HasRecentTimes));
        }

        private void Cancel()
        {
            Result = null;
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void Ok()
        {
            Result = new TimeSpan(ChosenHours, ChosenMinutes, ChosenSeconds);

            StoreRecentTimes((int)Result.Value.TotalSeconds);

            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void StoreRecentTimes(int newlyEnteredTimeSeconds)
        {
            if (newlyEnteredTimeSeconds == 0)
            {
                return;
            }

            var recentTimes = new List<int>(_recentTimes);

            if (!recentTimes.Contains(newlyEnteredTimeSeconds))
            {
                recentTimes.Add(newlyEnteredTimeSeconds);
            }

            recentTimes.Sort();
            
            _startOffsetStorageService.Store(_mediaFileName, _mediaDurationSeconds, recentTimes.Take(MaxRecentItemsCount).ToArray());
        }

        private string GenerateTimeString()
        {
            var seconds = (ChosenHours * 60 * 60) + (ChosenMinutes * 60) + ChosenSeconds;
            return TimeSpan.FromSeconds(seconds).AsMediaDurationString();
        }

        private void ClearRecentsList()
        {
            _startOffsetStorageService.Store(_mediaFileName, _mediaDurationSeconds, null);
            _recentTimes = _startOffsetStorageService.ReadOffsets(_mediaFileName, _mediaDurationSeconds).ToList();

            RaisePropertyChanged(nameof(RecentTimes));
            RaisePropertyChanged(nameof(HasRecentTimes));
        }

        private void RemoveRecentTime(int timeSeconds)
        {
            _recentTimes.Remove(timeSeconds);

            _startOffsetStorageService.Store(_mediaFileName, _mediaDurationSeconds, _recentTimes);

            RaisePropertyChanged(nameof(RecentTimes));
            RaisePropertyChanged(nameof(HasRecentTimes));
        }
    }
}

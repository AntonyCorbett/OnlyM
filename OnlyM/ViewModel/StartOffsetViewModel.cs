namespace OnlyM.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Windows.Media;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using MaterialDesignThemes.Wpf;

    internal class StartOffsetViewModel : ViewModelBase
    {
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Colors.DarkRed);
        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Colors.DarkGreen);

        private int _chosenHours;
        private int _chosenMinutes;
        private int _chosenSeconds;

        private TimeSpan _maxStartTime;

        public StartOffsetViewModel()
        {
            OkCommand = new RelayCommand(Ok);
            CancelCommand = new RelayCommand(Cancel);
        }

        public TimeSpan MaxStartTime
        {
            get => _maxStartTime;
            set
            {
                if (_maxStartTime != value)
                {
                    _maxStartTime = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(ChosenTimeBrush));
                }
            }
        }

        public TimeSpan? Result { get; private set; }

        public RelayCommand OkCommand { get; set; }

        public RelayCommand CancelCommand { get; set; }

        public IEnumerable<int> Hours => Enumerable.Range(0, 12);

        public IEnumerable<int> Minutes => Enumerable.Range(0, 59);

        public IEnumerable<int> Seconds => Enumerable.Range(0, 59);

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
                }
            }
        }

        public string ChosenTimeAsString => GenerateTimeString();

        public Brush ChosenTimeBrush
        {
            get
            {
                var chosenTime = new TimeSpan(ChosenHours, ChosenMinutes, ChosenSeconds);
                return chosenTime >= MaxStartTime ? RedBrush : GreenBrush;
            }
        }

        private void Cancel()
        {
            Result = null;
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private void Ok()
        {
            Result = new TimeSpan(ChosenHours, ChosenMinutes, ChosenSeconds);
            DialogHost.CloseDialogCommand.Execute(null, null);
        }

        private string GenerateTimeString()
        {
            var seconds = (ChosenHours * 60 * 60) + (ChosenMinutes * 60) + ChosenSeconds;
            return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
        }
    }
}

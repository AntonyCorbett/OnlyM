namespace OnlyMSlideManager.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Media.Imaging;
    using System.Windows.Shapes;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using GalaSoft.MvvmLight.Messaging;
    using MaterialDesignThemes.Wpf;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using OnlyM.CoreSys;
    using OnlyM.CoreSys.Services.Snackbar;
    using OnlyM.CoreSys.Services.UI;
    using OnlyM.Slides;
    using OnlyMSlideManager.Helpers;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.PubSubMessages;
    using OnlyMSlideManager.Services;
    using OnlyMSlideManager.Services.DragAndDrop;
    using OnlyMSlideManager.Services.Options;
    using Serilog;

    public class MainViewModel : ViewModelBase
    {
        private const string AppName = @"O N L Y M  Slide Manager";
        private const int MaxSlideIndexNumber = 99;
        private const int MaxImageWidth = 1920;
        private const int MaxImageHeight = 1080;

        private readonly IDialogService _dialogService;
        private readonly IDragAndDropServiceCustom _dragAndDropServiceCustom;
        private readonly ISnackbarService _snackbarService;
        private readonly IUserInterfaceService _userInterfaceService;
        private readonly IOptionsService _optionsService;
        private readonly LanguageItem[] _languages;

        private string _defaultFileOpenFolder;
        private string _defaultFileSaveFolder;
        private string _lastSavedSlideshowSignature;
        private string _currentSlideshowPath;
        private SlideFileBuilder _currentSlideFileBuilder;
        private bool? _autoPlay;
        private bool? _autoClose;
        private bool? _loop;
        private int? _dwellTimeSeconds;
        private bool _busy;
        private bool _isProgressVisible;
        private double _progressPercentageValue;

        public MainViewModel(
            IDialogService dialogService, 
            IDragAndDropServiceCustom dragAndDropServiceCustom,
            ISnackbarService snackbarService,
            IUserInterfaceService userInterfaceService,
            IOptionsService optionsService)
        {
            _dialogService = dialogService;
            _dragAndDropServiceCustom = dragAndDropServiceCustom;
            _snackbarService = snackbarService;
            _userInterfaceService = userInterfaceService;
            _optionsService = optionsService;
            _languages = GetSupportedLanguages();

            AddDesignTimeItems();

            InitCommands();
            Messenger.Default.Register<ReorderMessage>(this, OnReorderMessage);
            Messenger.Default.Register<DropImagesMessage>(this, OnDropImageFilesMessage);

            _userInterfaceService.BusyStatusChangedEvent += HandleBusyStatusChangedEvent;

            if (!IsInDesignMode)
            {
                InitNewSlideshow(null);
            }
        }

        public bool IsProgressVisible
        {
            get => _isProgressVisible;
            set
            {
                if (_isProgressVisible != value)
                {
                    _isProgressVisible = value;
                    RaisePropertyChanged();
                }
            }
        }

        public double ProgressPercentageValue
        {
            get => _progressPercentageValue;
            set
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (_progressPercentageValue != value)
                {
                    _progressPercentageValue = value;
                    RaisePropertyChanged();
                }
            }
        }

        public SlideFileBuilder CurrentSlideFileBuilder
        {
            get => _currentSlideFileBuilder;
            set
            {
                if (_currentSlideFileBuilder != value)
                {
                    _currentSlideFileBuilder = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool? AutoPlay
        {
            get => _autoPlay;
            set
            {
                if (value != _autoPlay)
                {
                    _autoPlay = value;

                    if (value != null && _currentSlideFileBuilder != null)
                    {
                        _currentSlideFileBuilder.AutoPlay = value.Value;
                    }

                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(CanLoop));
                    RaisePropertyChanged(nameof(CanAutoClose));
                }
            }
        }

        public bool? AutoClose
        {
            get => _autoClose;
            set
            {
                if (value != _autoClose)
                {
                    _autoClose = value;

                    if (value != null && _currentSlideFileBuilder != null)
                    {
                        _currentSlideFileBuilder.AutoClose = value.Value;
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public bool? Loop
        {
            get => _loop;
            set
            {
                if (value != _loop)
                {
                    _loop = value;

                    if (value != null && _currentSlideFileBuilder != null)
                    {
                        _currentSlideFileBuilder.Loop = value.Value;
                    }

                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(CanAutoClose));
                }
            }
        }

        public int? DwellTimeSeconds
        {
            get => _dwellTimeSeconds;
            set
            {
                if (value != _dwellTimeSeconds)
                {
                    _dwellTimeSeconds = value;
                    if (_currentSlideFileBuilder != null)
                    {
                        if (value == null)
                        {
                            _currentSlideFileBuilder.DwellTimeMilliseconds = 0;
                        }
                        else
                        {
                            _currentSlideFileBuilder.DwellTimeMilliseconds = _dwellTimeSeconds.Value * 1000;
                        }
                    }

                    RaisePropertyChanged();
                }
            }
        }

        public bool HasSlides => SlideItems.Count > 1;

        public bool HasNoSlides => SlideItems.Count == 1;

        public ISnackbarMessageQueue TheSnackbarMessageQueue => _snackbarService.TheSnackbarMessageQueue;

        public bool IsDirty => CreateSlideshowSignature() != _lastSavedSlideshowSignature;

        public IEnumerable<LanguageItem> Languages => _languages;

        public bool CanLoop => AutoPlay == true;

        public bool CanAutoClose => AutoPlay == true && Loop == false;

        public string LanguageId
        {
            get => _optionsService.Culture;
            set
            {
                if (_optionsService.Culture != value)
                {
                    _optionsService.Culture = value;
                    RaisePropertyChanged();

                    _snackbarService.EnqueueWithOk(Properties.Resources.LANGUAGE_RESTART, Properties.Resources.OK);
                }
            }
        }

        public ObservableCollectionEx<SlideItem> SlideItems { get; } = new ObservableCollectionEx<SlideItem>();

        public RelayCommand<string> DeleteSlideCommand { get; set; }

        public RelayCommand NewFileCommand { get; set; }

        public RelayCommand OpenFileCommand { get; set; }

        public RelayCommand SaveFileCommand { get; set; }

        public RelayCommand SaveFileAsCommand { get; set; }

        public RelayCommand ClosedCommand { get; set; }

        public RelayCommand ClosingCommand { get; set; }

        public RelayCommand CancelClosingCommand { get; set; }

        public string CurrentSlideshowPath
        {
            get => _currentSlideshowPath;
            set
            {
                if (_currentSlideshowPath == null || _currentSlideshowPath != value)
                {
                    _currentSlideshowPath = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(MainWindowCaption));
                }
            }
        }

        public string MainWindowCaption
        {
            get
            {
                if (!string.IsNullOrEmpty(_currentSlideshowPath))
                {
                    return $"{System.IO.Path.GetFileNameWithoutExtension(_currentSlideshowPath)} - {AppName}";
                }

                return AppName;
            }
        }

        public bool Busy
        {
            get => _busy;
            set
            {
                if (_busy != value)
                {
                    _busy = value;
                    RaisePropertyChanged();
                    RaisePropertyChanged(nameof(IsNotBusy));
                }
            }
        }

        public bool IsNotBusy => !Busy;

        public void DragSourcePreviewMouseDown(Control card, Point position)
        {
            _dragAndDropServiceCustom.DragSourcePreviewMouseDown(card, position);
        }

        public void DragSourcePreviewMouseMove(Point position)
        {
            _dragAndDropServiceCustom.DragSourcePreviewMouseMove(position);
        }

        public void Drop(Rectangle rect, DragEventArgs e)
        {
            _dragAndDropServiceCustom.Drop(rect, e);
        }

        public void DragEnter(Rectangle rect, DragEventArgs e)
        {
            _dragAndDropServiceCustom.DragEnter(rect, e);
        }

        private void InitCommands()
        {
            DeleteSlideCommand = new RelayCommand<string>(DeleteSlide, CanDeleteSlide);
            NewFileCommand = new RelayCommand(NewFile, CanExecuteNewFile);
            OpenFileCommand = new RelayCommand(OpenFile, CanExecuteOpenFile);
            SaveFileCommand = new RelayCommand(DoSaveFile, CanExecuteSaveFile);
            SaveFileAsCommand = new RelayCommand(DoSaveFileAs, CanExecuteSaveAsFile);
            ClosedCommand = new RelayCommand(ExecuteClosed);
            ClosingCommand = new RelayCommand(ExecuteClosing, CanExecuteClosing);
            CancelClosingCommand = new RelayCommand(ExecuteCancelClosing);
        }

        private bool CanDeleteSlide(string slideName)
        {
            return !Busy;
        }

        private void DeleteSlide(string slideName)
        {
            var slide = GetSlideByName(slideName);
            if (slide != null)
            {
                SlideItems.Remove(slide);
                CurrentSlideFileBuilder.RemoveSlide(slide.Name);

                RaisePropertyChanged(nameof(HasSlides));
                RaisePropertyChanged(nameof(HasNoSlides));
                RaisePropertyChanged(nameof(IsDirty));
            }
        }

        private SlideItem GetSlideByName(string slideName)
        {
            return SlideItems.SingleOrDefault(x => slideName.Equals(x.Name, StringComparison.OrdinalIgnoreCase));
        }

        private bool CanExecuteOpenFile()
        {
            return !Busy;
        }

        private bool CanExecuteNewFile()
        {
            return !Busy;
        }

        private bool CanExecuteSaveAsFile()
        {
            if (string.IsNullOrEmpty(_currentSlideshowPath))
            {
                return false;
            }

            return !Busy;
        }

        private bool CanExecuteSaveFile()
        {
            if (string.IsNullOrEmpty(_currentSlideshowPath))
            {
                return !Busy;
            }

            return IsDirty && !Busy;
        }

        private async Task SaveFileAs()
        {
            using (var d = new CommonSaveFileDialog())
            {
                d.OverwritePrompt = true;
                d.AlwaysAppendDefaultExtension = true;
                d.IsExpandedMode = true;
                d.DefaultDirectory = _defaultFileSaveFolder ?? Helpers.FileUtils.GetPrivateSlideshowFolder();
                d.DefaultExtension = SlideFile.FileExtension;
                d.Filters.Add(new CommonFileDialogFilter(Properties.Resources.SLIDESHOW_FILE, $"*{SlideFile.FileExtension}"));
                d.Title = Properties.Resources.SAVE_SLIDESHOW_TITLE;

                var rv = d.ShowDialog();
                if (rv == CommonFileDialogResult.Ok)
                {
                    _defaultFileSaveFolder = System.IO.Path.GetDirectoryName(d.FileName);

                    await SaveFileInternal(d.FileName, true);
                    
                    InitNewSlideshow(d.FileName);
                }
            }
        }

        private async void DoSaveFile()
        {
            Keyboard.ClearFocus();
            await SaveFile();
        }

        private async void DoSaveFileAs()
        {
            Keyboard.ClearFocus();
            await SaveFileAs();
        }

        private async Task SaveFile()
        {
            try
            {
                if (string.IsNullOrEmpty(CurrentSlideshowPath))
                {
                    await SaveFileAs();
                }
                else
                {
                    await SaveFileInternal(CurrentSlideshowPath, true);
                    SaveSignature();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, $"Could not save file: {CurrentSlideshowPath}");
                _snackbarService.EnqueueWithOk(Properties.Resources.COULD_NOT_SAVE, Properties.Resources.OK);
            }
        }

        private Task SaveFileInternal(string path, bool showNotificationWhenComplete)
        {
            return Task.Run(() =>
            {
                using (_userInterfaceService.BeginBusy())
                {
                    IsProgressVisible = true;
                    try
                    {
                        CurrentSlideFileBuilder.Build(path, true);

                        if (showNotificationWhenComplete)
                        {
                            _snackbarService.EnqueueWithOk(Properties.Resources.SAVED_FILE, Properties.Resources.OK);
                        }
                    }
                    finally
                    {
                        IsProgressVisible = false;
                    }
                }
            });
        }

        private async void OpenFile()
        {
            Keyboard.ClearFocus();

            if (IsDirty)
            {
                var result = await _dialogService.ShouldSaveDirtyDataAsync().ConfigureAwait(true);
                if (result == true)
                {
                    await SaveFileInternal(CurrentSlideshowPath, false);
                }
                else if (result == null)
                {
                    return;
                }
            }

            using (var d = new CommonOpenFileDialog())
            {
                d.DefaultDirectory = _defaultFileOpenFolder ?? Helpers.FileUtils.GetPrivateSlideshowFolder();
                d.DefaultExtension = SlideFile.FileExtension;
                d.Filters.Add(new CommonFileDialogFilter(Properties.Resources.SLIDESHOW_FILE, $"*{SlideFile.FileExtension}"));
                d.Title = Properties.Resources.OPEN_SLIDESHOW_TITLE;

                var rv = d.ShowDialog();
                if (rv == CommonFileDialogResult.Ok)
                {
                    _defaultFileOpenFolder = System.IO.Path.GetDirectoryName(d.FileName);
                    InitNewSlideshow(d.FileName);
                }
            }
        }

        private BitmapSource CreateBitmapImage(string filePath)
        {
            var image = GraphicsUtils.GetImageAutoRotatedIfRequired(filePath);
            return GraphicsUtils.Downsize(image, MaxImageWidth, MaxImageHeight);
        }

        private void GenerateSlideItems()
        {
            using (new ObservableCollectionSuppression<SlideItem>(SlideItems))
            {
                SlideItems.Clear();

                if (_currentSlideFileBuilder != null)
                {
                    int slideIndex = 1;

                    foreach (var slide in _currentSlideFileBuilder.GetSlides())
                    {
                        var image = slide.Image ?? CreateBitmapImage(slide.OriginalFilePath);

                        var newSlide = new SlideItem
                        {
                            Name = slide.ArchiveEntryName,
                            OriginalFilePath = slide.OriginalFilePath,
                            Image = image,
                            FadeInForward = slide.FadeInForward,
                            FadeInReverse = slide.FadeInReverse,
                            FadeOutForward = slide.FadeOutForward,
                            FadeOutReverse = slide.FadeOutReverse,
                            DwellTimeSeconds = slide.DwellTimeMilliseconds == 0
                                ? (int?)null
                                : slide.DwellTimeMilliseconds / 1000,
                            DropZoneId = Guid.NewGuid().ToString(),
                            SlideIndex = slideIndex == MaxSlideIndexNumber ? MaxSlideIndexNumber : slideIndex++
                        };

                        newSlide.SlideItemModifiedEvent += HandleSlideItemModifiedEvent;
                        SlideItems.Add(newSlide);
                    }
                }

                AddEndMarker();
            }

            RaisePropertyChanged(nameof(HasSlides));
            RaisePropertyChanged(nameof(HasNoSlides));
        }

        private void HandleSlideItemModifiedEvent(object sender, EventArgs e)
        {
            if (sender is SlideItem item)
            {
                var slide = _currentSlideFileBuilder.GetSlide(item.Name);
                if (slide != null)
                {
                    slide.FadeInForward = item.FadeInForward;
                    slide.FadeInReverse = item.FadeInReverse;
                    slide.FadeOutForward = item.FadeOutForward;
                    slide.FadeOutReverse = item.FadeOutReverse;

                    if (item.DwellTimeSeconds == null)
                    {
                        slide.DwellTimeMilliseconds = 0;
                    }
                    else
                    {
                        slide.DwellTimeMilliseconds = item.DwellTimeSeconds.Value * 1000;
                    }

                    RaisePropertyChanged(nameof(IsDirty));
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        private void AddEndMarker()
        {
            SlideItems.Add(new SlideItem
            {
                IsEndMarker = true
            });
        }

        private async void NewFile()
        {
            if (IsDirty)
            {
                var result = await _dialogService.ShouldSaveDirtyDataAsync().ConfigureAwait(true);
                if (result == true)
                {
                    await SaveFile();
                }
                else if (result == null)
                {
                    return;
                }
            }

            InitNewSlideshow(null);
        }

        private void InitNewSlideshow(string optionalPathToExistingSlideshow)
        {
            var builder = new SlideFileBuilder(MaxImageWidth, MaxImageHeight);
            if (!string.IsNullOrEmpty(optionalPathToExistingSlideshow))
            {
                builder.Load(optionalPathToExistingSlideshow);
            }

            if (CurrentSlideFileBuilder != null)
            {
                CurrentSlideFileBuilder.BuildProgressEvent -= HandleBuildProgressEvent;
            }

            CurrentSlideFileBuilder = builder;
            CurrentSlideFileBuilder.BuildProgressEvent += HandleBuildProgressEvent;

            CurrentSlideshowPath = optionalPathToExistingSlideshow;

            AutoPlay = CurrentSlideFileBuilder.AutoPlay;
            AutoClose = CurrentSlideFileBuilder.AutoClose;
            Loop = CurrentSlideFileBuilder.Loop;

            if (CurrentSlideFileBuilder.DwellTimeMilliseconds == 0)
            {
                DwellTimeSeconds = null;
            }
            else
            {
                DwellTimeSeconds = CurrentSlideFileBuilder.DwellTimeMilliseconds / 1000;
            }

            SaveSignature();

            GenerateSlideItems();
        }

        private void HandleBuildProgressEvent(object sender, OnlyM.Slides.Models.BuildProgressEventArgs e)
        {
            ProgressPercentageValue = e.PercentageComplete;
        }

        private void AddDesignTimeItems()
        {
            if (IsInDesignMode)
            {
                var slides = DesignTimeSlideCreation.GenerateSlides(7);

                foreach (var slide in slides)
                {
                    SlideItems.Add(slide);
                }
            }
        }

        private string CreateSlideshowSignature()
        {
            return _currentSlideFileBuilder?.CreateSignature();
        }

        private void SaveSignature()
        {
            _lastSavedSlideshowSignature = CreateSlideshowSignature();
            RaisePropertyChanged(nameof(IsDirty));

            CommandManager.InvalidateRequerySuggested();
        }

        private void OnReorderMessage(ReorderMessage message)
        {
            var newOrder = new List<SlideItem>();

            int slideIndex = 1;

            foreach (var slide in SlideItems)
            {
                if (slide.DropZoneId == message.TargetId)
                {
                    message.SourceItem.SlideIndex = slideIndex == MaxSlideIndexNumber
                        ? MaxSlideIndexNumber
                        : slideIndex++;

                    newOrder.Add(message.SourceItem);
                }

                if (slide != message.SourceItem)
                {
                    slide.SlideIndex = slideIndex == MaxSlideIndexNumber
                        ? MaxSlideIndexNumber
                        : slideIndex++;

                    newOrder.Add(slide);
                }
            }

            using (new ObservableCollectionSuppression<SlideItem>(SlideItems))
            {
                SlideItems.Clear();

                foreach (var slide in newOrder)
                {
                    SlideItems.Add(slide);
                }
            }

            CurrentSlideFileBuilder.SyncSlideOrder(newOrder.Select(x => x.Name));
            
            RaisePropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
        }

        private void ExecuteClosed()
        {
        }

        private void ExecuteClosing()
        {
        }

        private bool CanExecuteClosing()
        {
            return !_dialogService.IsDialogVisible() && !IsDirty && !Busy;
        }

        private async void ExecuteCancelClosing()
        {
            if (_dialogService.IsDialogVisible() || Busy)
            {
                return;
            }

            var rv = await _dialogService.ShouldSaveDirtyDataAsync().ConfigureAwait(true);
            if (rv == true)
            {
                await SaveFile();
            }

            if (rv != null)
            {
                // User answered "No". Make the data not dirty by saving current sig...
                SaveSignature();
                Messenger.Default.Send(new CloseAppMessage());
            }
        }

        private void HandleBusyStatusChangedEvent(object sender, EventArgs e)
        {
            Busy = _userInterfaceService.IsBusy();
            CommandManager.InvalidateRequerySuggested();
        }

        private async void OnDropImageFilesMessage(DropImagesMessage message)
        {
            if (!Busy)
            {
                using (_userInterfaceService.BeginBusy())
                {
                    try
                    {
                        var fileCount = await AddImages(message.FileList, message.TargetId);
                        GenerateSlideItems();

                        switch (fileCount)
                        {
                            case 0:
                                _snackbarService.EnqueueWithOk(Properties.Resources.NO_SLIDES_CREATED, Properties.Resources.OK);
                                break;

                            case 1:
                                _snackbarService.EnqueueWithOk(Properties.Resources.SLIDE_CREATED, Properties.Resources.OK);
                                break;

                            default:
                                var msg = string.Format(Properties.Resources.X_SLIDES_CREATED, fileCount);
                                _snackbarService.EnqueueWithOk(msg, Properties.Resources.OK);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Logger.Warning(ex, "Could not add all images");
                        _snackbarService.EnqueueWithOk(Properties.Resources.ERROR_ADDING_IMAGES, Properties.Resources.OK);
                    }
                }
            }
        }

        private Task<int> AddImages(List<string> files, string messageTargetId)
        {
            return Task.Run(() =>
            {
                Thread.Sleep(500);

                var count = 0;
                if (CurrentSlideFileBuilder != null)
                {
                    var dropTargetSlide = SlideItems.SingleOrDefault(x => x.DropZoneId == messageTargetId);
                    var dropTargetSlideIndex = SlideItems.IndexOf(dropTargetSlide);

                    foreach (var file in files)
                    {
                        CurrentSlideFileBuilder.InsertSlide(
                            dropTargetSlideIndex++,
                            file,
                            true,
                            true,
                            true,
                            true);

                        ++count;
                    }
                }
                
                return count;
            });
        }

        private LanguageItem[] GetSupportedLanguages()
        {
            var result = new List<LanguageItem>();

            var subFolders = Directory.GetDirectories(AppDomain.CurrentDomain.BaseDirectory);

            foreach (var folder in subFolders)
            {
                if (!string.IsNullOrEmpty(folder))
                {
                    try
                    {
                        var c = new CultureInfo(System.IO.Path.GetFileNameWithoutExtension(folder));
                        result.Add(new LanguageItem
                        {
                            LanguageId = c.Name,
                            LanguageName = c.EnglishName
                        });
                    }
                    catch (CultureNotFoundException)
                    {
                        // expected
                    }
                }
            }

            // the native language
            {
                var c = new CultureInfo(System.IO.Path.GetFileNameWithoutExtension("en-GB"));
                result.Add(new LanguageItem
                {
                    LanguageId = c.Name,
                    LanguageName = c.EnglishName
                });
            }

            result.Sort((x, y) => string.Compare(x.LanguageName, y.LanguageName, StringComparison.Ordinal));

            return result.ToArray();
        }
    }
}
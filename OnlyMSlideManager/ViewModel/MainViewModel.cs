namespace OnlyMSlideManager.ViewModel
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Input;
    using System.Windows.Shapes;
    using GalaSoft.MvvmLight;
    using GalaSoft.MvvmLight.CommandWpf;
    using GalaSoft.MvvmLight.Messaging;
    using Microsoft.WindowsAPICodePack.Dialogs;
    using OnlyM.Slides;
    using OnlyMSlideManager.Helpers;
    using OnlyMSlideManager.Models;
    using OnlyMSlideManager.PubSubMessages;
    using OnlyMSlideManager.Services;
    using OnlyMSlideManager.Services.DragAndDrop;

    public class MainViewModel : ViewModelBase
    {
        private const string AppName = @"O N L Y M  Slideshow Manager";

        private readonly IDialogService _dialogService;
        private readonly IDragAndDropServiceCustom _dragAndDropServiceCustom;

        private string _defaultFileOpenFolder;
        private string _defaultFileSaveFolder;
        private string _lastSavedSlideshowSignature;
        private string _currentSlideshowPath;
        private SlideFileBuilder _currentSlideFileBuilder;
        private bool? _autoPlay;
        private bool? _loop;

        public MainViewModel(IDialogService dialogService, IDragAndDropServiceCustom dragAndDropServiceCustom)
        {
            _dialogService = dialogService;
            _dragAndDropServiceCustom = dragAndDropServiceCustom;

            AddDesignTimeItems();

            CreateCommands();
            Messenger.Default.Register<ReorderMessage>(this, OnReorderMessage);
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
                }
            }
        }

        public bool IsDirty => CreateSlideshowSignature() != _lastSavedSlideshowSignature;

        public ObservableCollection<SlideItem> SlideItems { get; } = new ObservableCollection<SlideItem>();

        public RelayCommand NewFileCommand { get; set; }

        public RelayCommand OpenFileCommand { get; set; }

        public RelayCommand SaveFileCommand { get; set; }

        public RelayCommand SaveFileAsCommand { get; set; }

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

        public void DragSourcePreviewMouseDown(Control card, Point position)
        {
            _dragAndDropServiceCustom.DragSourcePreviewMouseDown(card, position);
        }

        public void DragSourcePreviewMouseMove(Point position)
        {
            _dragAndDropServiceCustom.DragSourcePreviewMouseMove(position);
        }

        public void Drop(Rectangle rect)
        {
            _dragAndDropServiceCustom.Drop(rect);
        }

        private void CreateCommands()
        {
            NewFileCommand = new RelayCommand(NewFile);
            OpenFileCommand = new RelayCommand(OpenFile);
            SaveFileCommand = new RelayCommand(SaveFile, CanExecuteSaveFile);
            SaveFileAsCommand = new RelayCommand(SaveFileAs, CanExecuteSaveAsFile);
        }

        private bool CanExecuteSaveAsFile()
        {
            if (string.IsNullOrEmpty(_currentSlideshowPath))
            {
                return false;
            }

            return SlideItems.Count > 1;
        }

        private bool CanExecuteSaveFile()
        {
            if (string.IsNullOrEmpty(_currentSlideshowPath))
            {
                return SlideItems.Count > 1;
            }

            return IsDirty;
        }

        private void SaveFileAs()
        {
            using (var d = new CommonSaveFileDialog())
            {
                d.OverwritePrompt = true;
                d.AlwaysAppendDefaultExtension = true;
                d.IsExpandedMode = true;
                d.DefaultDirectory = _defaultFileSaveFolder ?? FileUtils.GetPrivateSlideshowFolder();
                d.DefaultExtension = SlideFile.FileExtension;
                d.Filters.Add(new CommonFileDialogFilter(Properties.Resources.SLIDESHOW_FILE, $"*{SlideFile.FileExtension}"));
                d.Title = Properties.Resources.SAVE_SLIDESHOW_TITLE;

                var rv = d.ShowDialog();
                if (rv == CommonFileDialogResult.Ok)
                {
                    _defaultFileSaveFolder = System.IO.Path.GetDirectoryName(d.FileName);
                    var themePath = d.FileName;

                    CurrentSlideFileBuilder.Build(d.FileName, true);

                    InitNewSlideshow(d.FileName);
                }
            }
        }

        private void SaveFile()
        {
            SaveFileInternal();
            SaveSignature();
        }

        private void SaveFileInternal()
        {
            CurrentSlideFileBuilder.Build(CurrentSlideshowPath, true);
        }

        private async void OpenFile()
        {
            if (IsDirty)
            {
                var result = await _dialogService.ShouldSaveDirtyDataAsync().ConfigureAwait(true);
                if (result == true)
                {
                    SaveFileInternal();
                }
                else if (result == null)
                {
                    return;
                }
            }

            using (var d = new CommonOpenFileDialog())
            {
                d.DefaultDirectory = _defaultFileOpenFolder ?? FileUtils.GetPrivateSlideshowFolder();
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

        private void GenerateSlideItems()
        {
            SlideItems.Clear();

            if (_currentSlideFileBuilder != null)
            {
                foreach (var slide in _currentSlideFileBuilder.GetSlides())
                {
                    SlideItems.Add(new SlideItem
                    {
                        Name = slide.ArchiveEntryName,
                        OriginalFilePath = slide.OriginalFilePath,
                        Image = slide.Image,
                        FadeInForward = slide.FadeInForward,
                        FadeInReverse = slide.FadeInReverse,
                        FadeOutForward = slide.FadeOutForward,
                        FadeOutReverse = slide.FadeOutReverse,
                        DwellTimeMilliseconds = slide.DwellTimeMilliseconds,
                        DropZoneId = Guid.NewGuid().ToString()
                    });
                }
            }

            AddEndMarker();
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
                    SaveFile();
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
            CurrentSlideFileBuilder = new SlideFileBuilder(optionalPathToExistingSlideshow);
            CurrentSlideshowPath = optionalPathToExistingSlideshow;

            AutoPlay = CurrentSlideFileBuilder.AutoPlay;
            Loop = CurrentSlideFileBuilder.Loop;

            SaveSignature();
            GenerateSlideItems();
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
            else
            {
                GenerateSlideItems();
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

            foreach (var slide in SlideItems)
            {
                if (slide.DropZoneId == message.TargetId)
                {
                    newOrder.Add(message.SourceItem);
                }

                if (slide != message.SourceItem)
                {
                    newOrder.Add(slide);
                }
            }

            SlideItems.Clear();

            foreach (var slide in newOrder)
            {
                SlideItems.Add(slide);
            }

            CurrentSlideFileBuilder.SyncSlideOrder(newOrder.Select(x => x.Name));
            
            RaisePropertyChanged(nameof(IsDirty));
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
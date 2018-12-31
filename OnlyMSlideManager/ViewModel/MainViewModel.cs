namespace OnlyMSlideManager.ViewModel
{
    using System.Collections.ObjectModel;
    using GalaSoft.MvvmLight;
    using OnlyMSlideManager.Helpers;
    using OnlyMSlideManager.Models;

    public class MainViewModel : ViewModelBase
    {
        public MainViewModel()
        {
            AddDesignTimeItems();
        }

        public ObservableCollection<SlideItem> SlideItems { get; } = new ObservableCollection<SlideItem>();

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
    }
}
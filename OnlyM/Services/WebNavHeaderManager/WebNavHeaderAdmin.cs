namespace OnlyM.Services.WebNavHeaderManager
{
    using System;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media.Animation;
    using GalaSoft.MvvmLight.Threading;

    internal class WebNavHeaderAdmin
    {
        private const int WebHeaderHeight = 76;
        private const int WebHeaderActionPixels = 5;
        private readonly Grid _webNavHeader;
        private WebNavHeaderStatus _webHeaderStatus;

        public WebNavHeaderAdmin(Grid webNavHeader)
        {
            _webNavHeader = webNavHeader;
        }

        public void MouseMove(Point pos)
        {
            switch (_webHeaderStatus)
            {
                case WebNavHeaderStatus.NotVisible:
                    if (pos.Y < WebHeaderActionPixels)
                    {
                        ShowWebNavHeader();
                    }

                    break;

                case WebNavHeaderStatus.Visible:
                    if (pos.Y > WebHeaderHeight)
                    {
                        HideWebNavHeader();
                    }

                    break;
            }
        }

        public void PreviewWebNavHeader()
        {
            AnimateWebNavHeader(
                WebNavHeaderStatus.Showing,
                WebNavHeaderStatus.InPreview,
                0,
                WebHeaderHeight,
                () =>
                {
                    // completed animation
                    Task.Delay(3000).ContinueWith(t =>
                    {
                        DispatcherHelper.CheckBeginInvokeOnUI(HideWebNavHeader);
                    });
                });
        }

        private void HideWebNavHeader()
        {
            AnimateWebNavHeader(
                WebNavHeaderStatus.Hiding,
                WebNavHeaderStatus.NotVisible,
                WebHeaderHeight,
                0);
        }

        private void ShowWebNavHeader()
        {
            AnimateWebNavHeader(
                WebNavHeaderStatus.Showing,
                WebNavHeaderStatus.Visible,
                0,
                WebHeaderHeight);
        }

        private void AnimateWebNavHeader(
            WebNavHeaderStatus startStatus,
            WebNavHeaderStatus endStatus,
            double from, 
            double to,
            Action onCompleted = null)
        {
            _webHeaderStatus = startStatus;

            var anim = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(0.5)
            };

            anim.Completed += (sender, args) =>
            {
                _webHeaderStatus = endStatus;
                onCompleted?.Invoke();
            };

            _webNavHeader.BeginAnimation(FrameworkElement.HeightProperty, anim);
        }
    }
}

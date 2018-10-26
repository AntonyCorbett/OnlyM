namespace OnlyM.Core.Subtitles
{
    using System;
    using System.Windows.Threading;

    public class SubtitleProvider
    {
        private readonly SubtitleFile _file;
        private readonly DispatcherTimer _timer;
        private readonly DateTime _videoStartTime;

        private SubtitleEntry _currentSubtitle;
        private SubtitleStatus _currentStatus;
        private bool _stopped;

        public SubtitleProvider(string srtFilePath, TimeSpan videoPlayHead)
        {
            if (!string.IsNullOrEmpty(srtFilePath))
            {
                _file = new SubtitleFile(srtFilePath);

                if (_file.Count > 0)
                {
                    _timer = new DispatcherTimer();
                    _timer.Tick += HandleTimerTick;

                    _videoStartTime = DateTime.UtcNow - videoPlayHead;

                    OnSubtitleEvent(SubtitleStatus.NotShowing, null);

                    QueueNextSubtitle();
                }
            }
        }

        public event EventHandler<SubtitleEventArgs> SubtitleEvent;

        public int Count => _file?.Count ?? 0;

        public void Stop()
        {
            _stopped = true;
            OnSubtitleEvent(SubtitleStatus.NotShowing, null);
        }

        private void QueueNextSubtitle()
        {
            var videoPlaybackTime = DateTime.UtcNow - _videoStartTime;

            _currentSubtitle = _file?.GetNext();
            while (_currentSubtitle != null && _currentSubtitle.Timing.End < videoPlaybackTime)
            {
                _currentSubtitle = _file?.GetNext();
            }

            if (_currentSubtitle == null)
            {
                OnSubtitleEvent(SubtitleStatus.NotShowing, null);
            }
            else
            {
                ConfigureTimer(videoPlaybackTime);
            }
        }

        private void ConfigureTimer(TimeSpan videoPlaybackTime)
        {
            TimeSpan intervalToFire;

            if (_currentStatus == SubtitleStatus.Showing)
            {
                intervalToFire = _currentSubtitle.Timing.End - videoPlaybackTime;
            }
            else
            {
                intervalToFire = _currentSubtitle.Timing.Start - videoPlaybackTime;
            }

            if (intervalToFire < TimeSpan.Zero)
            {
                intervalToFire = TimeSpan.Zero;
            }

            if (_timer != null)
            {
                _timer.Interval = intervalToFire;
                _timer.Start();
            }
        }

        private void HandleTimerTick(object sender, EventArgs e)
        {
            if (_timer != null && sender == _timer)
            {
                _timer?.Stop();

                if (_currentStatus == SubtitleStatus.NotShowing)
                {
                    OnSubtitleEvent(SubtitleStatus.Showing, _currentSubtitle.Text);
                    ConfigureTimer(DateTime.UtcNow - _videoStartTime);
                }
                else
                {
                    OnSubtitleEvent(SubtitleStatus.NotShowing, null);
                    QueueNextSubtitle();
                }
            }
        }

        private void OnSubtitleEvent(SubtitleStatus status, string subtitleText)
        {
            if (!_stopped || status == SubtitleStatus.NotShowing)
            {
                _currentStatus = status;
                SubtitleEvent?.Invoke(this, new SubtitleEventArgs { Status = status, Text = subtitleText });
            }
        }
    }
}

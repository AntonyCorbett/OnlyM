namespace OnlyM.Core.Utils
{
    using System.Collections.ObjectModel;
    using System.Collections.Specialized;

    public class ObservableCollectionEx<T> : ObservableCollection<T>
    {
        private bool _notificationSupressed;
        private bool _supressNotification;

        public bool SupressNotification
        {
            get => _supressNotification;
            set
            {
                _supressNotification = value;

                if (_supressNotification == false && _notificationSupressed)
                {
                    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    _notificationSupressed = false;
                }
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            if (SupressNotification)
            {
                _notificationSupressed = true;
                return;
            }

            base.OnCollectionChanged(e);
        }
    }
}

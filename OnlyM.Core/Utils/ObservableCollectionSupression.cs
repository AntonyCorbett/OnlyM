namespace OnlyM.Core.Utils
{
    using System;

    public class ObservableCollectionSupression<T> : IDisposable
    {
        private readonly ObservableCollectionEx<T> _collection;

        public ObservableCollectionSupression(ObservableCollectionEx<T> collection)
        {
            _collection = collection;
            _collection.SupressNotification = true;
        }

        public void Dispose()
        {
            _collection.SupressNotification = false;
        }
    }
}

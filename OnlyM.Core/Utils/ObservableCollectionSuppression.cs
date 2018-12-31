namespace OnlyM.Core.Utils
{
    using System;

    public sealed class ObservableCollectionSuppression<T> : IDisposable
    {
        private readonly ObservableCollectionEx<T> _collection;

        public ObservableCollectionSuppression(ObservableCollectionEx<T> collection)
        {
            _collection = collection;
            _collection.SuppressNotification = true;
        }

        public void Dispose()
        {
            _collection.SuppressNotification = false;
        }
    }
}

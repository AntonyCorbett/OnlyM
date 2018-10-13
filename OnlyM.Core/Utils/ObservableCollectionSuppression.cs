namespace OnlyM.Core.Utils
{
    using System;

    public sealed class ObservableCollectionSuppression<T> : IDisposable
    {
        private readonly ObservableCollectionEx<T> _collection;

        public ObservableCollectionSuppression(ObservableCollectionEx<T> collection)
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

namespace OnlyMSlideManager.Helpers
{
    using System;
    using System.Collections.Generic;
    using OnlyM.Slides.Models;

    internal class SlideBuilderBatchHelper
    {
        private readonly IReadOnlyList<Slide> _slides;
        private readonly int _batchSize;

        private int _currentIndex;

        public SlideBuilderBatchHelper(IReadOnlyList<Slide> slides, int batchSize)
        {
            _slides = slides;
            _batchSize = batchSize;
        }

        public IReadOnlyList<Slide> GetBatch()
        {
            if (_currentIndex >= _slides.Count)
            {
                return null;
            }

            var result = new List<Slide>();

            var count = 0;
            while (_currentIndex < _slides.Count && count < _batchSize)
            {
                result.Add(_slides[_currentIndex]);

                ++_currentIndex;
                ++count;
            }

            return result;
        }

        public int GetBatchCount()
        {
            return (int)Math.Ceiling(_slides.Count / (double)_batchSize);
        }
    }
}

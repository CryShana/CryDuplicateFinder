using OpenCvSharp;

using System;

namespace CryDuplicateFinder.Algorithms
{
    public class TemplateDuplicateChecker : IDuplicateChecker
    {
        Mat img;
        string original;
        const int MaxDimension = 700;

        public double CalculateSimiliarityTo(FileEntry file)
        {
            throw new NotImplementedException();
        }

        public void LoadImage(FileEntry file)
        {
            original = file.Path;

            //var isCached = cache.TryGetValue(original, out _);
            //if (!isCached) img = GetImage(file);
            img = GetImage(file);
        }

        Mat GetImage(FileEntry file)
        {
            var m = CvHelpers.OpenImage(file.Path, ImreadModes.Grayscale);

            file.Width = m.Width;
            file.Height = m.Height;
            CvHelpers.Limit(m, m, MaxDimension);
            return m;
        }

        public void Dispose()
        {
            img?.Dispose();
        }

        public Mat GetLoadedImage() => img;

        public double GetMinRequiredSimilarity() => 0.5;
    }
}

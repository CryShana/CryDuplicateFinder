using OpenCvSharp;

using System;

namespace CryDuplicateFinder.Algorithms
{
    public class TemplateDuplicateChecker : IDuplicateChecker
    {
        Mat img;
        const int MaxDimension = 700;

        public double CalculateSimiliarityTo(string image)
        {
            throw new NotImplementedException();
        }

        public void LoadImage(string image) => img = GetImage(image);

        Mat GetImage(string image)
        {
            var m = new Mat(image);
            CvHelpers.Limit(m, m, MaxDimension);
            return m;
        }

        public void Dispose()
        {
            img?.Dispose();
        }
    }
}

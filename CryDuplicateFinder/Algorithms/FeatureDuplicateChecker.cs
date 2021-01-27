using OpenCvSharp;

using System.Collections.Concurrent;
using System.Linq;

namespace CryDuplicateFinder.Algorithms
{
    public class FeatureDuplicateChecker : IDuplicateChecker
    {
        static int MaxCacheCapacity = 500_000;
        static ConcurrentDictionary<string, Mat> cache = new();

        Mat img;
        const int MaxDimension = 400;

        public double CalculateSimiliarityTo(string image)
        {
            var isCached = cache.TryGetValue(image, out Mat descriptors2);

            using var descriptors = new Mat();
            using (var orb = ORB.Create())
            {
                orb.DetectAndCompute(img, null, out KeyPoint[] imgKeypoints, descriptors);

                if (!isCached)
                {
                    using var img2 = GetImage(image);

                    descriptors2 = new Mat();
                    orb.DetectAndCompute(img2, null, out KeyPoint[] imgKeypoints2, descriptors2);
                    cache.TryAdd(image, descriptors2);
                }
            }

            var matcher = new BFMatcher(NormTypes.Hamming, true);
            var matches = matcher.Match(descriptors, descriptors2);
            var mean = matches.Average(x => x.Distance);

            // non-matches have means around 55 - 80
            // matches have means around 0 - 40

            // MAPPING
            // 0... 100% similarity
            // 20... 90% similarity
            // 30... 80% similarity
            // 40... 70% similarity
            // 50... 60% similarity
            // 60... 50% similarity
            // 70... 40% similarity
            // 80... 30% similarity
            // 90... 20% similarity
            // 90+... 0% similarity

            if (mean > 90) return 0; // above 90 is 0%
            else if (mean > 20) return getInterpolatedValue(mean, 20, 90, 0.9, 0.2); // between 20-90 is 90% to 20%
            else return getInterpolatedValue(mean, 0, 20, 1, 0.9);
        }

        double getInterpolatedValue(double value, double min, double max, double intmin, double intmax)
        {
            var factor = (value - min) / (max - min);
            var target = intmin + factor * (intmax - intmin);
            return target;
        }

        public void LoadImage(string image) => img = GetImage(image);

        Mat GetImage(string image)
        {
            var m = CvHelpers.OpenImage(image, ImreadModes.Grayscale);
            CvHelpers.Limit(m, m, MaxDimension);
            return m;
        }

        public void Dispose()
        {
            img?.Dispose();
        }

        public static void ClearCache()
        {
            cache.Clear();
        }
    }
}

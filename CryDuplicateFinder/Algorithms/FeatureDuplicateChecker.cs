using OpenCvSharp;
using OpenCvSharp.Flann;

using System.Linq;
using System.Collections.Concurrent;
using System;

namespace CryDuplicateFinder.Algorithms
{
    public class FeatureDuplicateChecker : IDuplicateChecker
    {
        static int MaxCacheCapacity = 500_000;
        static ConcurrentDictionary<string, Mat> cache = new();
        static ConcurrentDictionary<string, (int[] r, int[] g, int[] b, int pixels)> cacheHistograms = new();

        Mat img;
        string original;
        const int MaxDimension = 300;

        public double CalculateSimiliarityTo(string image)
        {
            var isCached = cache.TryGetValue(image, out Mat descriptors2);
            var isCachedOriginal = cache.TryGetValue(original, out Mat dstp);
            Mat descriptors = null;

            // only compute if no value is cached
            using (var orb = ORB.Create(
                nFeatures: 500,
                scaleFactor: 1.2f,
                nLevels: 8,
                edgeThreshold: 31,
                firstLevel: 0,
                wtaK: 2,
                scoreType: ORBScoreType.Harris,
                patchSize: 31,
                fastThreshold: 20))
            {
                // check if original image had cached descriptors
                if (!isCachedOriginal)
                {
                    descriptors = new Mat();
                    orb.DetectAndCompute(img, null, out _, descriptors);
                    if (cache.Count < MaxCacheCapacity) cache.TryAdd(original, descriptors);
                }
                else descriptors = dstp;

                // check if target image is cached
                if (!isCached)
                {
                    using var img2 = GetImage(image);

                    descriptors2 = new Mat();
                    orb.DetectAndCompute(img2, null, out _, descriptors2);
                    if (cache.Count < MaxCacheCapacity) cache.TryAdd(image, descriptors2);
                }
            }

            var matcher = new BFMatcher(NormTypes.Hamming, true);
            //var matcher = new FlannBasedMatcher(new LshIndexParams(20, 10, 0), new SearchParams());
            var matches = matcher.Match(descriptors, descriptors2);
            var mean = matches.Average(x => x.Distance);

            // use histogram comparison
            const int histogramGroups = 16;

            var isCached1H = cacheHistograms.TryGetValue(original, out var h1);
            if (!isCached1H)
            {
                (h1.b, h1.g, h1.r) = HistogramDuplicateChecker.GetHistogramGroups(img, histogramGroups);
                h1.pixels = img.Width * img.Height;
            }

            var isCached2H = cacheHistograms.TryGetValue(image, out var h2);
            if (!isCached2H)
            {
                using var img2 = GetImage(image);
                (h2.b, h2.g, h2.r) = HistogramDuplicateChecker.GetHistogramGroups(img2, histogramGroups);
                h2.pixels = img2.Width * img2.Height;
            }

            var diff1 = HistogramDuplicateChecker.ComputerHistogramDifferences(h1, h2);
            var diff2 = HistogramDuplicateChecker.ComputerHistogramDifferences(h2, h1);

            var sim1 = HistogramDuplicateChecker.GetSimilarityFromDifferences(diff1);
            var sim2 = HistogramDuplicateChecker.GetSimilarityFromDifferences(diff2);
            var sim_histogram = Math.Max(sim1, sim2);

            // cache it if there is space
            if (!isCached1H && cacheHistograms.Count < MaxCacheCapacity) cacheHistograms.TryAdd(original, h1);
            if (!isCached2H && cacheHistograms.Count < MaxCacheCapacity) cacheHistograms.TryAdd(image, h2);

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

            var sim_orb = 0.0;
            if (mean > 90) sim_orb = 0; // above 90 is 0%
            else if (mean > 20) sim_orb = getInterpolatedValue(mean, 20, 90, 0.9, 0.2); // between 20-90 is 90% to 20%
            else sim_orb = getInterpolatedValue(mean, 0, 20, 1, 0.9);

            // add weighted histogram score
            var final_sim = 0.95 * sim_orb + 0.1 * sim_histogram;
            if (final_sim > 1) final_sim = 1;

            return final_sim;
        }

        double getInterpolatedValue(double value, double min, double max, double intmin, double intmax)
        {
            var factor = (value - min) / (max - min);
            var target = intmin + factor * (intmax - intmin);
            return target;
        }

        public void LoadImage(string image)
        {
            original = image;

            var isCached = cache.TryGetValue(original, out _);
            if (!isCached) img = GetImage(image);
        }

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

        public double GetMinRequiredSimilarity() => 0.64;
    }
}

using OpenCvSharp;

using System;
using System.Collections.Concurrent;

namespace CryDuplicateFinder.Algorithms
{
    /// <summary>
    /// Check for duplicates by comparing histograms. This class caches results of processes images. Call ClearCache() to clear it.
    /// </summary>
    public class HistogramDuplicateChecker : IDuplicateChecker
    {
        static int MaxCacheCapacity = 500_000;
        static ConcurrentDictionary<string, (int[] r, int[] g, int[] b, int pixels)> cache = new();

        Mat img;
        string original;
        const int MaxDimension = 100;

        public double CalculateSimiliarityTo(string image)
        {
            const int histogramGroups = 16;

            var isCached1 = cache.TryGetValue(original, out var h1);
            if (!isCached1)
            {
                (h1.b, h1.g, h1.r) = GetHistogramGroups(img, histogramGroups);
                h1.pixels = img.Width * img.Height;
            }

            var isCached2 = cache.TryGetValue(image, out var h2);
            if (!isCached2)
            {
                using var img2 = GetImage(image);
                (h2.b, h2.g, h2.r) = GetHistogramGroups(img2, histogramGroups);
                h2.pixels = img2.Width * img2.Height;
            }

            var diff1 = ComputerHistogramDifferences((h1.b, h1.g, h1.r), (h2.b, h2.g, h2.r));
            var diff2 = ComputerHistogramDifferences((h2.b, h2.g, h2.r), (h1.b, h1.g, h1.r));

            var sim1 = GetSimilarityFromDifferences(h1.pixels, diff1);
            var sim2 = GetSimilarityFromDifferences(h2.pixels, diff2);

            // cache it if there is space
            if (!isCached1 && cache.Count < MaxCacheCapacity) cache.TryAdd(original, h1);
            if (!isCached2 && cache.Count < MaxCacheCapacity) cache.TryAdd(image, h2);

            return Math.Max(sim1, sim2);
        }

        (int[] b, int[] g, int[] r) GetHistogramGroups(Mat m, int groupCount)
        {
            using var mat3 = new Mat<Vec3b>(m);
            var indexer = mat3.GetIndexer();

            // histogram color groups
            var br = new int[groupCount];
            var gr = new int[groupCount];
            var rr = new int[groupCount];
            var step = 255 / groupCount + 1;

            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                {
                    Vec3b color = indexer[y, x];
                    byte b = color.Item0;
                    byte g = color.Item1;
                    byte r = color.Item2;

                    br[b / step]++;
                    gr[g / step]++;
                    rr[r / step]++;
                }

            return (br, gr, rr);
        }

        (int b, int g, int r) ComputerHistogramDifferences((int[] b, int[] g, int[] r) h1, (int[] b, int[] g, int[] r) h2)
        {
            int b = 0;
            int g = 0;
            int r = 0;

            for (int i = 0; i < h1.b.Length; i++)
            {
                b += Math.Abs(h2.b[i] - h1.b[i]);
                g += Math.Abs(h2.g[i] - h1.g[i]);
                r += Math.Abs(h2.r[i] - h1.r[i]);
            }

            return (b, g, r);
        }

        double GetSimilarityFromDifferences(int pixels, (int b, int g, int r) differences)
        {
            var meanDiff = (differences.b + differences.g + differences.r) / 3.0;
            var similarity = 1 - (meanDiff / pixels);
            if (similarity < 0) similarity = 0;
            return similarity;
        }

        public void LoadImage(string image)
        {
            original = image;

            var isCached = cache.TryGetValue(image, out _);
            if (!isCached) img = GetImage(image);
        }

        Mat GetImage(string image)
        {
            var m = CvHelpers.OpenImage(image, ImreadModes.Color);
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

        public double GetMinRequiredSimilarity() => 0.77;
    }
}

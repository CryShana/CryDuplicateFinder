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

        public double CalculateSimiliarityTo(FileEntry file)
        {
            const int histogramGroups = 16;

            var isCached1 = cache.TryGetValue(original, out var h1);
            if (!isCached1)
            {
                (h1.b, h1.g, h1.r) = GetHistogramGroups(img, histogramGroups);
                h1.pixels = img.Width * img.Height;
            }

            var isCached2 = cache.TryGetValue(file.Path, out var h2);
            if (!isCached2)
            {
                using var img2 = GetImage(file);
                (h2.b, h2.g, h2.r) = GetHistogramGroups(img2, histogramGroups);
                h2.pixels = img2.Width * img2.Height;
            }

            var diff1 = ComputerHistogramDifferences(h1, h2);
            var diff2 = ComputerHistogramDifferences(h2, h1);

            var sim1 = GetSimilarityFromDifferences(diff1);
            var sim2 = GetSimilarityFromDifferences(diff2);

            // cache it if there is space
            if (!isCached1 && cache.Count < MaxCacheCapacity) cache.TryAdd(original, h1);
            if (!isCached2 && cache.Count < MaxCacheCapacity) cache.TryAdd(file.Path, h2);

            return Math.Max(sim1, sim2);
        }

        public static (int[] b, int[] g, int[] r) GetHistogramGroups(Mat m, int groupCount)
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

        public static (double b, double g, double r) ComputerHistogramDifferences((int[] b, int[] g, int[] r, int pixels) h1, (int[] b, int[] g, int[] r, int pixels) h2)
        {
            double b = 0;
            double g = 0;
            double r = 0;

            for (int i = 0; i < h1.b.Length; i++)
            {
                // subtract NORMALIZED pixel counts
                b += Math.Abs(((double)h2.b[i] / h2.pixels) - ((double)h1.b[i] / h1.pixels));
                g += Math.Abs(((double)h2.g[i] / h2.pixels) - ((double)h1.g[i] / h1.pixels));
                r += Math.Abs(((double)h2.r[i] / h2.pixels) - ((double)h1.r[i] / h1.pixels));
            }

            return (b, g, r);
        }

        public static double GetSimilarityFromDifferences((double b, double g, double r) differences)
        {
            var meanDiff = (differences.b + differences.g + differences.r) / 3.0;
            var similarity = 1 - meanDiff;
            if (similarity < 0) similarity = 0;
            return similarity;
        }

        public void LoadImage(FileEntry file)
        {
            original = file.Path;

            var isCached = cache.TryGetValue(original, out _);
            if (!isCached) img = GetImage(file);

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

        public static void ClearCache()
        {
            cache.Clear();
        }

        public double GetMinRequiredSimilarity() => 0.87;

        public Mat GetLoadedImage() => img;
    }
}

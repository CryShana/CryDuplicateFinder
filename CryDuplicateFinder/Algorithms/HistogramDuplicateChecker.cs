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
        const int MaxDimension = 150;

        public double CalculateSimiliarityTo(string image)
        {        
            const int histogramGroups = 16;

            var h1 = GetHistogramGroups(img, histogramGroups);
            var isCached = cache.TryGetValue(image, out var h2);
            if (!isCached)
            {
                using var img2 = GetImage(image);
                (h2.b, h2.g, h2.r) = GetHistogramGroups(img2, histogramGroups);
                h2.pixels = img2.Width * img2.Height;
            }

            var diff1 = ComputerHistogramDifferences(h1, (h2.b, h2.g, h2.r));
            var diff2 = ComputerHistogramDifferences((h2.b, h2.g, h2.r), h1);

            var sim1 = GetSimilarityFromDifferences(img.Width * img.Height, diff1);
            var sim2 = GetSimilarityFromDifferences(h2.pixels, diff2);

            // cache it if there is space
            if (!isCached && cache.Count < MaxCacheCapacity) cache.TryAdd(image, h2);

            return Math.Max(sim1, sim2);
        }

        (int[] b, int[] g, int[] r) GetHistogramGroups(Mat m, int groupCount)
        {
            var indexer = m.GetGenericIndexer<Vec3b>();

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

        public void LoadImage(string image) => img = GetImage(image);
        
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
    }
}

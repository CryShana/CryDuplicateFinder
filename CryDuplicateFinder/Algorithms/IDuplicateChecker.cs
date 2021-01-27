using System;

namespace CryDuplicateFinder.Algorithms
{
    /// <summary>
    /// Object that implements duplicate checking. Is threadsafe.
    /// </summary>
    interface IDuplicateChecker : IDisposable
    {
        /// <summary>
        /// Load the main image
        /// </summary>
        void LoadImage(string image);

        /// <summary>
        /// Compare main image to specified image and return similarity
        /// </summary>
        double CalculateSimiliarityTo(string image);
    }

    public enum DuplicateCheckingMode
    {
        Histogram,
        Features
    }
}

using OpenCvSharp;

using System;
using System.IO;

namespace CryDuplicateFinder.Algorithms
{
    public class CvHelpers
    {
        public static void Limit(Mat src, Mat to, int maxDimension)
        {
            int width, height;
            if (src.Width > src.Height)
            {
                width = maxDimension;
                height = (width * src.Height) / src.Width;
            }
            else
            {
                height = maxDimension;
                width = (height * src.Width) / src.Height;
            }

            if (Math.Max(src.Width, src.Height) > maxDimension) Cv2.Resize(src, to, new(width, height));
        }

        public static Mat OpenImage(string path, ImreadModes mode = ImreadModes.Color)
        {
            var stream = new MemoryStream(File.ReadAllBytes(path));
            return Mat.FromStream(stream, mode);
        }
    }
}

using UnityEngine;

namespace XRScan.Core
{
    /// <summary>
    /// Bounding-box conversion for the CV server's box contract (cv-server README, "Boxes"): each box is
    /// [x1, y1, x2, y2], the top-left then the bottom-right corner, as fractions (0-1) of the width and height
    /// of the image that was sent, with the origin at the image's top-left corner and y pointing down.
    /// Pixel boxes from older servers are still accepted when the image size is known.
    /// </summary>
    public static class BoundingBoxUtils
    {
        public static bool TryNormalize(float[] bbox, int imageWidth, int imageHeight, out Rect normalized)
        {
            normalized = Rect.zero;
            if (bbox == null || bbox.Length < 4)
            {
                return false;
            }

            float x1 = bbox[0];
            float y1 = bbox[1];
            float x2 = bbox[2];
            float y2 = bbox[3];

            bool alreadyNormalized = x2 <= 1f && y2 <= 1f;
            if (!alreadyNormalized)
            {
                if (imageWidth <= 0 || imageHeight <= 0)
                {
                    return false;
                }

                x1 /= imageWidth;
                x2 /= imageWidth;
                y1 /= imageHeight;
                y2 /= imageHeight;
            }

            float minX = Mathf.Min(x1, x2);
            float maxX = Mathf.Max(x1, x2);
            float minY = Mathf.Min(y1, y2);
            float maxY = Mathf.Max(y1, y2);

            minX = Mathf.Clamp01(minX);
            maxX = Mathf.Clamp01(maxX);
            minY = Mathf.Clamp01(minY);
            maxY = Mathf.Clamp01(maxY);

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 0f || height <= 0f)
            {
                return false;
            }

            normalized = new Rect(minX, minY, width, height);
            return true;
        }

        /// <summary>
        /// The centre of a normalized box as a viewport point. The box's origin is the image's top-left corner;
        /// a Unity viewport's is the bottom-left, so y is flipped.
        /// </summary>
        public static Vector2 CenterInViewport(Rect normalized) =>
            new Vector2(normalized.center.x, 1f - normalized.center.y);

        /// <summary>The centre of a normalized box in pixels of the image (origin top-left, y down).</summary>
        public static Vector2 CenterInPixels(Rect normalized, int imageWidth, int imageHeight) =>
            new Vector2(normalized.center.x * imageWidth, normalized.center.y * imageHeight);

        public static Rect NormalizedToPixels(Rect normalized, int imageWidth, int imageHeight)
        {
            if (imageWidth <= 0 || imageHeight <= 0)
            {
                return Rect.zero;
            }

            float x = normalized.x * imageWidth;
            float y = normalized.y * imageHeight;
            float w = normalized.width * imageWidth;
            float h = normalized.height * imageHeight;
            return new Rect(x, y, w, h);
        }
    }
}

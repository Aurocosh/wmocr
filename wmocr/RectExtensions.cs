using Windows.Foundation;

namespace wmocr
{
    internal static class RectExtensions
    {
        public static Rect Inflate(this Rect rect, float amount)
        {
            return new Rect(
                rect.X - amount,
                rect.Y - amount,
                rect.Width + (2 * amount),
                rect.Height + (2 * amount)
            );
        }

        public static Rect Inflate(this Rect rect, float width, float height)
        {
            return new Rect(
                rect.X - width,
                rect.Y - height,
                rect.Width + (2 * width),
                rect.Height + (2 * height)
            );
        }
    }
}

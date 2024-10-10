using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace wm_ocr_cli
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

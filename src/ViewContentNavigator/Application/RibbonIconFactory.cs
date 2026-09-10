using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ViewContentNavigator.Application
{
    internal static class RibbonIconFactory
    {
        private static readonly Color[] RowColors =
        {
            Color.FromRgb(0xE8, 0x1A, 0x1A),
            Color.FromRgb(0x2E, 0xB8, 0x2E),
            Color.FromRgb(0x1E, 0x8F, 0xE8),
        };

        public static BitmapSource CreateLarge() => Render(32);

        public static BitmapSource CreateSmall() => Render(16);

        private static BitmapSource Render(int size)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var scale = size / 32.0;

                var rowHeight = 7 * scale;
                var gap = 3 * scale;
                var left = 4 * scale;
                var swatchWidth = 7 * scale;
                var barRight = 28 * scale;
                var top = 3 * scale;

                var barBrush = new SolidColorBrush(Color.FromRgb(0xD8, 0xDD, 0xE2));
                barBrush.Freeze();

                for (var i = 0; i < 3; i++)
                {
                    var y = top + i * (rowHeight + gap);

                    var swatch = new SolidColorBrush(RowColors[i]);
                    swatch.Freeze();
                    dc.DrawRectangle(swatch, null, new Rect(left, y, swatchWidth, rowHeight));

                    dc.DrawRectangle(barBrush, null,
                        new Rect(left + swatchWidth + 2 * scale, y + 1 * scale,
                            barRight - (left + swatchWidth + 2 * scale), rowHeight - 2 * scale));
                }
            }

            var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }
    }
}

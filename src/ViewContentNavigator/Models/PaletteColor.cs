using System.Collections.Generic;
using WpfColor = System.Windows.Media.Color;

namespace ViewContentNavigator.Models
{
    public sealed class PaletteColor
    {
        public PaletteColor(string name, byte r, byte g, byte b)
        {
            Name = name;
            R = r;
            G = g;
            B = b;
        }

        public string Name { get; }
        public byte R { get; }
        public byte G { get; }
        public byte B { get; }

        public WpfColor ToMediaColor() => WpfColor.FromRgb(R, G, B);

        public static IReadOnlyList<PaletteColor> DefaultPalette { get; } = new[]
        {
            new PaletteColor("Красный",    0xE8, 0x1A, 0x1A),
            new PaletteColor("Оранжевый",  0xF5, 0x7C, 0x00),
            new PaletteColor("Жёлтый",     0xF5, 0xD3, 0x00),
            new PaletteColor("Зелёный",    0x2E, 0xB8, 0x2E),
            new PaletteColor("Бирюзовый",  0x00, 0xB8, 0xB8),
            new PaletteColor("Синий",      0x1E, 0x8F, 0xE8),
            new PaletteColor("Фиолетовый", 0x7B, 0x2F, 0xE8),
            new PaletteColor("Пурпурный",  0xB8, 0x00, 0xB8),
            new PaletteColor("Розовый",    0xF5, 0x3D, 0x9E),
            new PaletteColor("Чёрный",     0x22, 0x22, 0x22),
            new PaletteColor("Белый",      0xFF, 0xFF, 0xFF),
        };
    }
}

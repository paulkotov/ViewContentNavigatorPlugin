using RevitColor = Autodesk.Revit.DB.Color;
using MediaColor = System.Windows.Media.Color;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.ViewModels
{
    internal static class ColorMapper
    {
        public static RevitColor ToRevit(MediaColor c) => new RevitColor(c.R, c.G, c.B);

        public static MediaColor ToMedia(SerializableColor c) => MediaColor.FromRgb(c.R, c.G, c.B);

        public static SerializableColor ToSerializable(MediaColor? c) =>
            c.HasValue ? SerializableColor.From(c.Value.R, c.Value.G, c.Value.B) : SerializableColor.None();
    }
}

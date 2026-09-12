using System.Collections.Generic;

namespace ViewContentNavigator.Models
{
    public sealed class NavigatorSettings
    {
        public string DocumentTitle { get; set; }
        public string ViewName { get; set; }
        public bool ShowInstances { get; set; }
        public bool AutoApply { get; set; } = true;
        public List<CategorySetting> Categories { get; set; } = new List<CategorySetting>();
    }

    public sealed class CategorySetting
    {
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public SerializableColor Color { get; set; }
        public int Opacity { get; set; } = 100;
        public List<FamilySetting> Families { get; set; } = new List<FamilySetting>();
    }

    public sealed class FamilySetting
    {
        public string Name { get; set; }
        public bool Visible { get; set; } = true;
        public SerializableColor Color { get; set; }
        public int Opacity { get; set; } = 100;

        public List<InstanceSetting> Instances { get; set; } = new List<InstanceSetting>();
    }

    public sealed class InstanceSetting
    {
        public int Id { get; set; }
        public bool Visible { get; set; } = true;
        public SerializableColor Color { get; set; }
        public int Opacity { get; set; } = 100;
    }

    public sealed class SerializableColor
    {
        public bool HasValue { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public static SerializableColor From(byte r, byte g, byte b) =>
            new SerializableColor { HasValue = true, R = r, G = g, B = b };

        public static SerializableColor None() => new SerializableColor { HasValue = false };
    }
}

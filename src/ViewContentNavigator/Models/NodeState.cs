using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ViewContentNavigator.Models
{
    public sealed class NodeState
    {
        public NodeState(IReadOnlyList<ElementId> elementIds, bool visible, Color color, int opacity = 100)
        {
            ElementIds = elementIds;
            Visible = visible;
            Color = color;
            Opacity = ClampOpacity(opacity);
        }

        public IReadOnlyList<ElementId> ElementIds { get; }
        public bool Visible { get; }
        public Color Color { get; }

        /// <summary>0–100, where 100 is fully opaque.</summary>
        public int Opacity { get; }

        public static int ClampOpacity(int opacity)
        {
            if (opacity < 0) return 0;
            if (opacity > 100) return 100;
            return opacity;
        }

        public static int ToTransparency(int opacity) => 100 - ClampOpacity(opacity);
    }
}

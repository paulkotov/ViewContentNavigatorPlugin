using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ViewContentNavigator.Models
{
    public sealed class NodeState
    {
        public NodeState(IReadOnlyList<ElementId> elementIds, bool visible, Color color)
        {
            ElementIds = elementIds;
            Visible = visible;
            Color = color;
        }

        public IReadOnlyList<ElementId> ElementIds { get; }
        public bool Visible { get; }
        public Color Color { get; }
    }
}

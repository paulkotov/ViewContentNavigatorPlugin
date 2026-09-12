using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.Services
{
    public interface IViewContentService
    {
        View3D CreateNavigatorView(Document doc);

        ViewSnapshot Scan(Document doc, View view);

        void SetVisibility(Document doc, View view, IReadOnlyList<ElementId> elementIds, bool visible);

        void SetColor(Document doc, View view, IReadOnlyList<ElementId> elementIds, Color color);

        void ResetColor(Document doc, View view, IReadOnlyList<ElementId> elementIds);

        void SetOpacity(Document doc, View view, IReadOnlyList<ElementId> elementIds, int opacity);

        void ApplyAll(Document doc, View view, IReadOnlyList<NodeState> states);

        void SaveView(Document doc, View view, string name);

        void DeleteView(UIApplication uiApp, ElementId viewId);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.Services
{
    public sealed class ViewContentService : IViewContentService
    {
        private const string TempViewPrefix = "VCN — Навигатор";

        public View3D CreateNavigatorView(Document doc)
        {
            var vft = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

            if (vft == null)
                throw new InvalidOperationException("В проекте нет типа 3D-вида (ViewFamilyType).");

            using (var tx = new Transaction(doc, "Создание вида навигатора"))
            {
                tx.Start();

                var view = View3D.CreateIsometric(doc, vft.Id);
                view.Name = MakeUniqueViewName(doc, $"{TempViewPrefix} {DateTime.Now:HH-mm-ss}");
                view.DetailLevel = ViewDetailLevel.Fine;
                view.DisplayStyle = DisplayStyle.Shading;

                tx.Commit();
                return view;
            }
        }

        public ViewSnapshot Scan(Document doc, View view)
        {
            var byCategory = new Dictionary<ElementId, CategoryBucket>(new ElementIdComparer());

            var collector = new FilteredElementCollector(doc)
                .WhereElementIsNotElementType();

            foreach (var element in collector)
            {
                if (element.ViewSpecific)
                    continue;

                var category = ResolveTopLevelCategory(element);
                if (category == null || category.CategoryType != CategoryType.Model)
                    continue;

                if (!byCategory.TryGetValue(category.Id, out var bucket))
                {
                    bucket = new CategoryBucket(category.Id, category.Name);
                    byCategory.Add(category.Id, bucket);
                }

                var familyName = ResolveFamilyName(doc, element);
                bucket.Add(familyName, new InstanceSnapshot(element.Id, DescribeInstance(element)));
            }

            var categories = byCategory.Values
                .OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase)
                .Select(b => b.ToSnapshot())
                .ToList();

            return new ViewSnapshot(view.Id, view.Name, categories);
        }

        public void SetVisibility(Document doc, View view, IReadOnlyList<ElementId> elementIds, bool visible)
        {
            if (elementIds == null || elementIds.Count == 0) return;

            using (var tx = new Transaction(doc, visible ? "Показать элементы" : "Скрыть элементы"))
            {
                tx.Start();
                ApplyVisibility(view, elementIds, visible);
                tx.Commit();
            }
        }

        public void SetColor(Document doc, View view, IReadOnlyList<ElementId> elementIds, Color color)
        {
            if (elementIds == null || elementIds.Count == 0) return;

            var ogs = BuildColorOverride(doc, color);
            using (var tx = new Transaction(doc, "Цвет элементов"))
            {
                tx.Start();
                foreach (var id in elementIds)
                    TrySetOverride(view, id, ogs);
                tx.Commit();
            }
        }

        public void ResetColor(Document doc, View view, IReadOnlyList<ElementId> elementIds)
        {
            if (elementIds == null || elementIds.Count == 0) return;

            var empty = new OverrideGraphicSettings();
            using (var tx = new Transaction(doc, "Сброс цвета"))
            {
                tx.Start();
                foreach (var id in elementIds)
                    TrySetOverride(view, id, empty);
                tx.Commit();
            }
        }

        public void ApplyAll(Document doc, View view, IReadOnlyList<NodeState> states)
        {
            if (states == null || states.Count == 0) return;

            var solidFill = GetSolidFillPatternId(doc);
            var empty = new OverrideGraphicSettings();

            using (var tx = new Transaction(doc, "Применение настроек навигатора"))
            {
                tx.Start();
                foreach (var state in states)
                {
                    if (state.ElementIds == null || state.ElementIds.Count == 0)
                        continue;

                    ApplyVisibility(view, state.ElementIds, state.Visible);

                    var ogs = state.Color == null ? empty : BuildColorOverride(state.Color, solidFill);
                    foreach (var id in state.ElementIds)
                        TrySetOverride(view, id, ogs);
                }
                tx.Commit();
            }
        }

        public void SaveView(Document doc, View view, string name)
        {
            using (var tx = new Transaction(doc, "Сохранение вида навигатора"))
            {
                tx.Start();
                var target = string.IsNullOrWhiteSpace(name) ? view.Name : name.Trim();
                view.Name = MakeUniqueViewName(doc, target, view.Id);
                tx.Commit();
            }
        }

        public void DeleteView(Document doc, ElementId viewId)
        {
            if (viewId == null || viewId.IntegerValue == ElementId.InvalidElementId.IntegerValue)
                return;

            if (!(doc.GetElement(viewId) is View))
                return;

            using (var tx = new Transaction(doc, "Удаление временного вида"))
            {
                tx.Start();
                doc.Delete(viewId);
                tx.Commit();
            }
        }

        private static void ApplyVisibility(View view, IReadOnlyList<ElementId> ids, bool visible)
        {
            if (visible)
            {
                var hidden = ids.Where(id => IsElementHidden(view, id)).ToList();
                if (hidden.Count > 0)
                    view.UnhideElements(hidden);
            }
            else
            {
                var hideable = ids
                    .Select(id => view.Document.GetElement(id))
                    .Where(e => e != null && e.CanBeHidden(view))
                    .Select(e => e.Id)
                    .ToList();
                if (hideable.Count > 0)
                    view.HideElements(hideable);
            }
        }

        private static bool IsElementHidden(View view, ElementId id)
        {
            var e = view.Document.GetElement(id);
            return e != null && e.IsHidden(view);
        }

        private static void TrySetOverride(View view, ElementId id, OverrideGraphicSettings ogs)
        {
            try
            {
                view.SetElementOverrides(id, ogs);
            }
            catch { }
        }

        private OverrideGraphicSettings BuildColorOverride(Document doc, Color color) =>
            BuildColorOverride(color, GetSolidFillPatternId(doc));

        private static OverrideGraphicSettings BuildColorOverride(Color color, ElementId solidFillPatternId)
        {
            var ogs = new OverrideGraphicSettings();
            ogs.SetProjectionLineColor(color);
            ogs.SetCutLineColor(color);

            if (solidFillPatternId != null &&
                solidFillPatternId.IntegerValue != ElementId.InvalidElementId.IntegerValue)
            {
                ogs.SetSurfaceForegroundPatternVisible(true);
                ogs.SetSurfaceForegroundPatternId(solidFillPatternId);
                ogs.SetSurfaceForegroundPatternColor(color);
                ogs.SetCutForegroundPatternVisible(true);
                ogs.SetCutForegroundPatternId(solidFillPatternId);
                ogs.SetCutForegroundPatternColor(color);
            }

            return ogs;
        }

        private static ElementId GetSolidFillPatternId(Document doc)
        {
            var solid = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(fp => fp.GetFillPattern()?.IsSolidFill == true);
            return solid?.Id ?? ElementId.InvalidElementId;
        }

        private static Category ResolveTopLevelCategory(Element element)
        {
            var category = element.Category;
            if (category == null) return null;
            return category.Parent ?? category;
        }

        private static string ResolveFamilyName(Document doc, Element element)
        {
            var typeId = element.GetTypeId();
            if (typeId != null && typeId.IntegerValue != ElementId.InvalidElementId.IntegerValue)
            {
                if (doc.GetElement(typeId) is ElementType type && !string.IsNullOrEmpty(type.FamilyName))
                    return type.FamilyName;
            }

            if (element is FamilyInstance fi && fi.Symbol?.FamilyName is string fn && !string.IsNullOrEmpty(fn))
                return fn;

            return "<Без семейства>";
        }

        private static string DescribeInstance(Element element)
        {
            var name = element.Name;
            if (string.IsNullOrEmpty(name))
                name = element.Category?.Name ?? "Элемент";
            return $"{name} [{element.Id.IntegerValue}]";
        }

        private static string MakeUniqueViewName(Document doc, string desired, ElementId ignore = null)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => ignore == null || v.Id.IntegerValue != ignore.IntegerValue)
                .Select(v => v.Name)
                .ToList();

            var name = desired;
            var i = 1;
            while (existing.Contains(name))
                name = $"{desired} ({++i})";
            return name;
        }

        private sealed class CategoryBucket
        {
            private readonly Dictionary<string, List<InstanceSnapshot>> _families =
                new Dictionary<string, List<InstanceSnapshot>>(StringComparer.CurrentCultureIgnoreCase);

            public CategoryBucket(ElementId id, string name)
            {
                Id = id;
                Name = name;
            }

            public ElementId Id { get; }
            public string Name { get; }

            public void Add(string familyName, InstanceSnapshot instance)
            {
                if (!_families.TryGetValue(familyName, out var list))
                {
                    list = new List<InstanceSnapshot>();
                    _families.Add(familyName, list);
                }
                list.Add(instance);
            }

            public CategorySnapshot ToSnapshot()
            {
                var families = _families
                    .OrderBy(kv => kv.Key, StringComparer.CurrentCultureIgnoreCase)
                    .Select(kv => new FamilySnapshot(
                        kv.Key,
                        kv.Value.OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ToList()))
                    .ToList();
                return new CategorySnapshot(Id, Name, families);
            }
        }

        private sealed class ElementIdComparer : IEqualityComparer<ElementId>
        {
            public bool Equals(ElementId x, ElementId y) => x?.IntegerValue == y?.IntegerValue;
            public int GetHashCode(ElementId obj) => obj?.IntegerValue ?? 0;
        }
    }
}

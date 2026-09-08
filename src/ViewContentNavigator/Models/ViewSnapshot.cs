using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace ViewContentNavigator.Models
{
    public sealed class ViewSnapshot
    {
        public ViewSnapshot(ElementId viewId, string viewName, IReadOnlyList<CategorySnapshot> categories)
        {
            ViewId = viewId;
            ViewName = viewName;
            Categories = categories;
        }

        public ElementId ViewId { get; }
        public string ViewName { get; }
        public IReadOnlyList<CategorySnapshot> Categories { get; }
    }

    public sealed class CategorySnapshot
    {
        public CategorySnapshot(ElementId categoryId, string name, IReadOnlyList<FamilySnapshot> families)
        {
            CategoryId = categoryId;
            Name = name;
            Families = families;
        }

        public ElementId CategoryId { get; }
        public string Name { get; }
        public IReadOnlyList<FamilySnapshot> Families { get; }
    }

    public sealed class FamilySnapshot
    {
        public FamilySnapshot(string name, IReadOnlyList<InstanceSnapshot> instances)
        {
            Name = name;
            Instances = instances;
        }

        public string Name { get; }
        public IReadOnlyList<InstanceSnapshot> Instances { get; }
    }

    public sealed class InstanceSnapshot
    {
        public InstanceSnapshot(ElementId id, string name)
        {
            Id = id;
            Name = name;
        }

        public ElementId Id { get; }
        public string Name { get; }
    }
}

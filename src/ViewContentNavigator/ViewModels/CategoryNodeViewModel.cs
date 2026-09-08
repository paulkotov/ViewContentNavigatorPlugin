using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.ViewModels
{
    public sealed class CategoryNodeViewModel : TreeNodeViewModel
    {
        private readonly List<ElementId> _ids;

        public CategoryNodeViewModel(INodeChangeSink sink, CategorySnapshot snapshot)
            : base(sink, parent: null, name: snapshot.Name)
        {
            CategoryId = snapshot.CategoryId;

            foreach (var family in snapshot.Families)
                Children.Add(new FamilyNodeViewModel(sink, this, family));

            _ids = Distinct(Children.SelectMany(c => c.ElementIds));
        }

        public ElementId CategoryId { get; }

        public override NodeKind Kind => NodeKind.Category;
        public override IReadOnlyList<ElementId> ElementIds => _ids;
        public override int Count => Children.Sum(c => c.Count);

        public IEnumerable<FamilyNodeViewModel> Families => Children.OfType<FamilyNodeViewModel>();
    }
}

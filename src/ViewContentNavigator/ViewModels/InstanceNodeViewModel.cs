using System.Collections.Generic;
using Autodesk.Revit.DB;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.ViewModels
{
    public sealed class InstanceNodeViewModel : TreeNodeViewModel
    {
        private readonly IReadOnlyList<ElementId> _ids;

        public InstanceNodeViewModel(INodeChangeSink sink, TreeNodeViewModel parent, InstanceSnapshot snapshot)
            : base(sink, parent, snapshot.Name)
        {
            ElementIdValue = snapshot.Id.IntegerValue;
            _ids = new[] { snapshot.Id };
        }

        public int ElementIdValue { get; }

        public override NodeKind Kind => NodeKind.Instance;
        public override IReadOnlyList<ElementId> ElementIds => _ids;
        public override int Count => 1;
    }
}

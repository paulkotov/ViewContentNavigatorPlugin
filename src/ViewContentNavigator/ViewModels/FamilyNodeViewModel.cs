using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.ViewModels
{
    public sealed class FamilyNodeViewModel : TreeNodeViewModel
    {
        private readonly IReadOnlyList<InstanceSnapshot> _instances;
        private readonly List<ElementId> _ids;
        private List<InstanceNodeViewModel> _instanceNodes;

        public FamilyNodeViewModel(INodeChangeSink sink, TreeNodeViewModel parent, FamilySnapshot snapshot)
            : base(sink, parent, snapshot.Name)
        {
            _instances = snapshot.Instances;
            _ids = Distinct(_instances.Select(i => i.Id));
        }

        public override NodeKind Kind => NodeKind.Family;
        public override IReadOnlyList<ElementId> ElementIds => _ids;
        public override int Count => _instances.Count;

        public IReadOnlyList<InstanceNodeViewModel> BuiltInstances => _instanceNodes;

        public void EnsureInstancesBuilt()
        {
            if (_instanceNodes != null)
                return;

            _instanceNodes = new List<InstanceNodeViewModel>(_instances.Count);
            foreach (var instance in _instances)
            {
                var node = new InstanceNodeViewModel(Sink, this, instance);
                node.SetColorSilently(Color);
                node.SetOpacitySilently(Opacity);
                // Экземпляры наследуют состояние семейства. Не пересчитываем родителя:
                // на этом шаге узлы ещё не добавлены в Children, и RecalculateCheckState
                // считал бы состояние по пустому/частичному списку, ломая галочку
                // семейства (и каскадом — категории) при включении «Экземпляров».
                node.SetCheckedSilently(IsChecked ?? true, updateParent: false);
                _instanceNodes.Add(node);
                Children.Add(node);
            }
        }

        public InstanceNodeViewModel FindInstance(int elementIdValue) =>
            _instanceNodes?.FirstOrDefault(n => n.ElementIdValue == elementIdValue);
    }
}

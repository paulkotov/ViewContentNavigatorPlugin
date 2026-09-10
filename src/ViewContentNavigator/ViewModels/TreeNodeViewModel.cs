using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using ViewContentNavigator.Mvvm;
using MediaColor = System.Windows.Media.Color;

namespace ViewContentNavigator.ViewModels
{
    public enum NodeKind
    {
        Category,
        Family,
        Instance
    }

    public abstract class TreeNodeViewModel : ObservableObject
    {
        private readonly INodeChangeSink _sink;

        private bool? _isChecked = true;
        private MediaColor? _color;
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isVisibleInTree = true;

        protected TreeNodeViewModel(INodeChangeSink sink, TreeNodeViewModel parent, string name)
        {
            _sink = sink;
            Parent = parent;
            Name = name;
            Children = new ObservableCollection<TreeNodeViewModel>();
        }

        protected INodeChangeSink Sink => _sink;

        public TreeNodeViewModel Parent { get; }
        public string Name { get; }
        public ObservableCollection<TreeNodeViewModel> Children { get; }

        public abstract NodeKind Kind { get; }

        public abstract IReadOnlyList<ElementId> ElementIds { get; }

        public abstract int Count { get; }

        public bool? IsChecked
        {
            get => _isChecked;
            set
            {
                if (SetCheckedState(value, updateChildren: true, updateParent: true) &&
                    !_sink.SuppressNotifications)
                {
                    _sink.RequestVisibility(this, value ?? true);
                }
            }
        }

        private bool SetCheckedState(bool? value, bool updateChildren, bool updateParent)
        {
            if (value == _isChecked)
                return false;

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Children)
                    child.SetCheckedState(_isChecked, updateChildren: true, updateParent: false);
            }

            if (updateParent)
                Parent?.RecalculateCheckState();

            OnPropertyChanged(nameof(IsChecked));
            return true;
        }

        private void RecalculateCheckState()
        {
            bool? state = null;
            var first = true;
            foreach (var child in Children)
            {
                if (first)
                {
                    state = child.IsChecked;
                    first = false;
                }
                else if (state != child.IsChecked)
                {
                    state = null;
                    break;
                }
            }

            SetCheckedState(state, updateChildren: false, updateParent: true);
        }

        public void SetCheckedSilently(bool value)
        {
            SetCheckedState(value, updateChildren: true, updateParent: true);
        }

        public MediaColor? Color
        {
            get => _color;
            set => SetColorState(value, notify: !_sink.SuppressNotifications, cascade: true);
        }

        public bool HasColor => _color.HasValue;

        private void SetColorState(MediaColor? value, bool notify, bool cascade)
        {
            var changed = _color != value;
            _color = value;

            if (cascade)
            {
                foreach (var child in Children)
                    child.SetColorState(value, notify: false, cascade: true);
            }

            if (changed)
            {
                OnPropertyChanged(nameof(Color));
                OnPropertyChanged(nameof(HasColor));
            }

            if (notify)
                _sink.RequestColor(this, value);
        }

        public void SetColorSilently(MediaColor? value) => SetColorState(value, notify: false, cascade: true);

        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public bool IsVisibleInTree
        {
            get => _isVisibleInTree;
            private set => SetProperty(ref _isVisibleInTree, value);
        }

        public bool ApplyFilter(string filter, bool showInstances)
        {
            if (Kind == NodeKind.Instance && !showInstances)
            {
                IsVisibleInTree = false;
                return false;
            }

            var selfMatch = string.IsNullOrWhiteSpace(filter) ||
                            Name.IndexOf(filter, System.StringComparison.CurrentCultureIgnoreCase) >= 0;

            var childMatch = false;
            foreach (var child in Children)
                childMatch |= child.ApplyFilter(filter, showInstances);

            IsVisibleInTree = selfMatch || childMatch;

            if (!string.IsNullOrWhiteSpace(filter) && childMatch)
                IsExpanded = true;

            return IsVisibleInTree;
        }

        protected static List<ElementId> Distinct(IEnumerable<ElementId> ids) =>
            ids.GroupBy(id => id.IntegerValue).Select(g => g.First()).ToList();
    }
}

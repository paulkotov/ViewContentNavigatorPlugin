using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using ViewContentNavigator.Models;
using ViewContentNavigator.Mvvm;
using ViewContentNavigator.Revit;
using ViewContentNavigator.Services;
using RevitColor = Autodesk.Revit.DB.Color;
using MediaColor = System.Windows.Media.Color;

namespace ViewContentNavigator.ViewModels
{
    public sealed class NavigatorViewModel : ObservableObject, INodeChangeSink
    {
        private readonly IRevitTask _revitTask;
        private readonly IViewContentService _service;
        private readonly IDocumentSettingsStore _store;
        private readonly Document _doc;
        private readonly string _documentTitle;

        private ElementId _viewId;
        private bool _viewSaved;
        private bool _suppress;

        private string _viewName;
        private string _filter = string.Empty;
        private bool _showInstances;
        private bool _autoApply = true;
        private bool _isBusy;
        private TreeNodeViewModel _selectedNode;
        private PaletteColor _selectedPaletteColor;
        private int _shownCount;
        private int _hiddenCount;
        private string _statusText = string.Empty;

        public NavigatorViewModel(
            IRevitTask revitTask,
            IViewContentService service,
            IDocumentSettingsStore store,
            Document doc,
            View3D view,
            ViewSnapshot snapshot,
            NavigatorSettings savedSettings)
        {
            _revitTask = revitTask;
            _service = service;
            _store = store;
            _doc = doc;
            _documentTitle = doc.Title;
            _viewId = view.Id;
            _viewName = snapshot.ViewName;

            Categories = new ObservableCollection<CategoryNodeViewModel>(
                snapshot.Categories.Select(c => new CategoryNodeViewModel(this, c)));

            Palette = PaletteColor.DefaultPalette;
            _selectedPaletteColor = Palette.FirstOrDefault();

            RefreshCommand = new AsyncRelayCommand(RefreshAsync);
            HideCommand = new RelayCommand(_ => ApplySelected(false), _ => SelectedNode != null);
            ShowCommand = new RelayCommand(_ => ApplySelected(true), _ => SelectedNode != null);
            ApplyColorCommand = new RelayCommand(OnApplyColor, _ => SelectedNode != null);
            PickColorCommand = new RelayCommand(_ => PickCustomColor(), _ => SelectedNode != null);
            ResetColorCommand = new RelayCommand(_ => ResetSelectedColor(), _ => SelectedNode != null);
            ResetAllCommand = new AsyncRelayCommand(ResetAllAsync, () => Categories.Count > 0);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            SaveViewCommand = new AsyncRelayCommand(SaveViewAsync);
            ClearFilterCommand = new RelayCommand(_ => Filter = string.Empty);

            if (savedSettings != null)
            {
                ApplySavedSettings(savedSettings);
                _ = SyncViewWithTreeAsync();
            }

            UpdateStatus();
        }

        public ObservableCollection<CategoryNodeViewModel> Categories { get; }
        public IReadOnlyList<PaletteColor> Palette { get; }

        public string ViewName
        {
            get => _viewName;
            private set => SetProperty(ref _viewName, value);
        }

        public string Filter
        {
            get => _filter;
            set
            {
                if (SetProperty(ref _filter, value))
                    ApplyFilterToTree();
            }
        }

        public bool ShowInstances
        {
            get => _showInstances;
            set
            {
                if (!SetProperty(ref _showInstances, value))
                    return;

                if (value)
                    RunSuppressed(() =>
                    {
                        foreach (var family in Categories.SelectMany(c => c.Families))
                            family.EnsureInstancesBuilt();
                    });

                ApplyFilterToTree();
            }
        }

        public bool AutoApply
        {
            get => _autoApply;
            set => SetProperty(ref _autoApply, value);
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        public TreeNodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    UpdateStatus();
                    System.Windows.Input.CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public PaletteColor SelectedPaletteColor
        {
            get => _selectedPaletteColor;
            set => SetProperty(ref _selectedPaletteColor, value);
        }

        public int ShownCount
        {
            get => _shownCount;
            private set => SetProperty(ref _shownCount, value);
        }

        public int HiddenCount
        {
            get => _hiddenCount;
            private set => SetProperty(ref _hiddenCount, value);
        }

        public string StatusText
        {
            get => _statusText;
            private set => SetProperty(ref _statusText, value);
        }

        public AsyncRelayCommand RefreshCommand { get; }
        public RelayCommand HideCommand { get; }
        public RelayCommand ShowCommand { get; }
        public RelayCommand ApplyColorCommand { get; }
        public RelayCommand PickColorCommand { get; }
        public RelayCommand ResetColorCommand { get; }
        public AsyncRelayCommand ResetAllCommand { get; }
        public AsyncRelayCommand SaveSettingsCommand { get; }
        public AsyncRelayCommand SaveViewCommand { get; }
        public RelayCommand ClearFilterCommand { get; }

        bool INodeChangeSink.SuppressNotifications => _suppress;

        void INodeChangeSink.RequestVisibility(TreeNodeViewModel node, bool visible)
        {
            if (AutoApply)
                _ = PushVisibilityAsync(node, visible);
            else
                UpdateStatus();
        }

        void INodeChangeSink.RequestColor(TreeNodeViewModel node, MediaColor? color)
        {
            if (!AutoApply)
                return;

            if (color.HasValue)
                _ = PushColorAsync(node, color.Value);
            else
                _ = PushResetColorAsync(node);
        }

        private void ApplySelected(bool visible)
        {
            var node = SelectedNode;
            if (node == null) return;

            node.SetCheckedSilently(visible);
            _ = PushVisibilityAsync(node, visible);
        }

        private void OnApplyColor(object parameter)
        {
            var node = SelectedNode;
            if (node == null) return;

            var palette = parameter as PaletteColor ?? SelectedPaletteColor;
            if (palette == null) return;

            SelectedPaletteColor = palette;
            var media = palette.ToMediaColor();

            node.SetColorSilently(media);
            _ = PushColorAsync(node, media);
        }

        private void PickCustomColor()
        {
            var node = SelectedNode;
            if (node == null) return;

            using (var dialog = new System.Windows.Forms.ColorDialog { FullOpen = true, AnyColor = true })
            {
                if (node.Color.HasValue)
                    dialog.Color = System.Drawing.Color.FromArgb(node.Color.Value.R, node.Color.Value.G, node.Color.Value.B);

                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                    return;

                var media = MediaColor.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B);
                node.SetColorSilently(media);
                _ = PushColorAsync(node, media);
            }
        }

        private void ResetSelectedColor()
        {
            var node = SelectedNode;
            if (node == null) return;

            node.SetColorSilently(null);
            _ = PushResetColorAsync(node);
        }

        private async Task ResetAllAsync()
        {
            var confirm = MessageBox.Show(
                "Показать все элементы и сбросить все цвета на этом виде?",
                "Сбросить всё",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (confirm != MessageBoxResult.Yes)
                return;

            RunSuppressed(() =>
            {
                foreach (var category in Categories)
                {
                    category.SetCheckedSilently(true);
                    category.SetColorSilently(null);
                }
            });

            await SyncViewWithTreeAsync();
            UpdateStatus();
        }

        private async Task RefreshAsync()
        {
            var settings = BuildSettings();

            var snapshot = await _revitTask.Run(app =>
            {
                var view = ResolveView();
                return view == null ? null : _service.Scan(_doc, view);
            });

            if (snapshot == null)
                return;

            RunSuppressed(() =>
            {
                Categories.Clear();
                foreach (var category in snapshot.Categories)
                    Categories.Add(new CategoryNodeViewModel(this, category));

                if (_showInstances)
                    foreach (var family in Categories.SelectMany(c => c.Families))
                        family.EnsureInstancesBuilt();
            });

            ViewName = snapshot.ViewName;
            ApplySavedSettings(settings);
            ApplyFilterToTree();
            await SyncViewWithTreeAsync();
            UpdateStatus();
        }

        private async Task SaveSettingsAsync()
        {
            var settings = BuildSettings();

            await _revitTask.Run(app => _store.Save(_doc, settings));
        }

        private async Task SaveViewAsync()
        {
            var name = $"Навигатор видимости — {_documentTitle}";
            var settings = BuildSettings();

            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.SaveView(_doc, view, name);

                _store.Save(_doc, settings);
            });

            _viewSaved = true;
            ViewName = name;
        }

        private async Task PushVisibilityAsync(TreeNodeViewModel node, bool visible)
        {
            var ids = node.ElementIds;
            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.SetVisibility(_doc, view, ids, visible);
            });
            UpdateStatus();
        }

        private async Task PushColorAsync(TreeNodeViewModel node, MediaColor color)
        {
            var ids = node.ElementIds;
            var revitColor = ColorMapper.ToRevit(color);
            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.SetColor(_doc, view, ids, revitColor);
            });
        }

        private async Task PushResetColorAsync(TreeNodeViewModel node)
        {
            var ids = node.ElementIds;
            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.ResetColor(_doc, view, ids);
            });
        }

        private Task SyncViewWithTreeAsync()
        {
            var states = new List<NodeState>();
            foreach (var family in Categories.SelectMany(c => c.Families))
            {
                if (family.BuiltInstances != null)
                {
                    foreach (var instance in family.BuiltInstances)
                    {
                        var instColor = instance.Color.HasValue ? ColorMapper.ToRevit(instance.Color.Value) : null;
                        states.Add(new NodeState(instance.ElementIds, instance.IsChecked != false, instColor));
                    }
                }
                else
                {
                    var color = family.Color.HasValue ? ColorMapper.ToRevit(family.Color.Value) : null;
                    states.Add(new NodeState(family.ElementIds, family.IsChecked != false, color));
                }
            }

            return _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.ApplyAll(_doc, view, states);
            });
        }

        private void ApplySavedSettings(NavigatorSettings settings)
        {
            if (settings == null) return;

            RunSuppressed(() =>
            {
                AutoApply = settings.AutoApply;

                var categoriesByName = GroupUnique(Categories, c => c.Name);

                foreach (var cs in settings.Categories)
                {
                    if (!categoriesByName.TryGetValue(cs.Name, out var categoryNode))
                        continue;

                    if (cs.Color != null && cs.Color.HasValue)
                        categoryNode.SetColorSilently(ColorMapper.ToMedia(cs.Color));

                    var familiesByName = GroupUnique(categoryNode.Families, f => f.Name);
                    foreach (var fs in cs.Families)
                    {
                        if (!familiesByName.TryGetValue(fs.Name, out var familyNode))
                            continue;

                        familyNode.SetCheckedSilently(fs.Visible);
                        if (fs.Color != null && fs.Color.HasValue)
                            familyNode.SetColorSilently(ColorMapper.ToMedia(fs.Color));

                        if (fs.Instances != null && fs.Instances.Count > 0)
                        {
                            familyNode.EnsureInstancesBuilt();
                            foreach (var isetting in fs.Instances)
                            {
                                var instanceNode = familyNode.FindInstance(isetting.Id);
                                if (instanceNode == null)
                                    continue;

                                instanceNode.SetCheckedSilently(isetting.Visible);
                                if (isetting.Color != null && isetting.Color.HasValue)
                                    instanceNode.SetColorSilently(ColorMapper.ToMedia(isetting.Color));
                            }
                        }
                    }
                }

                _showInstances = settings.ShowInstances;
                OnPropertyChanged(nameof(ShowInstances));
                if (_showInstances)
                    foreach (var family in Categories.SelectMany(c => c.Families))
                        family.EnsureInstancesBuilt();

                ApplyFilterToTree();
            });
        }

        private NavigatorSettings BuildSettings()
        {
            var settings = new NavigatorSettings
            {
                DocumentTitle = _documentTitle,
                ViewName = ViewName,
                ShowInstances = ShowInstances,
                AutoApply = AutoApply
            };

            foreach (var category in Categories)
            {
                var cs = new CategorySetting
                {
                    Name = category.Name,
                    Visible = category.IsChecked != false,
                    Color = ColorMapper.ToSerializable(category.Color)
                };

                foreach (var family in category.Families)
                {
                    var fs = new FamilySetting
                    {
                        Name = family.Name,
                        Visible = family.IsChecked != false,
                        Color = ColorMapper.ToSerializable(family.Color)
                    };

                    if (family.BuiltInstances != null)
                    {
                        foreach (var instance in family.BuiltInstances)
                        {
                            if (instance.IsChecked != false && !instance.HasColor)
                                continue;

                            fs.Instances.Add(new InstanceSetting
                            {
                                Id = instance.ElementIdValue,
                                Visible = instance.IsChecked != false,
                                Color = ColorMapper.ToSerializable(instance.Color)
                            });
                        }
                    }

                    cs.Families.Add(fs);
                }

                settings.Categories.Add(cs);
            }

            return settings;
        }

        private static Dictionary<string, T> GroupUnique<T>(IEnumerable<T> items, Func<T, string> keySelector)
        {
            var result = new Dictionary<string, T>(StringComparer.CurrentCultureIgnoreCase);
            foreach (var item in items)
            {
                var key = keySelector(item);
                if (!result.ContainsKey(key))
                    result.Add(key, item);
            }
            return result;
        }

        private void RunSuppressed(Action action)
        {
            var previous = _suppress;
            _suppress = true;
            try
            {
                action();
            }
            finally
            {
                _suppress = previous;
            }
        }

        private View ResolveView()
        {
            return _doc.GetElement(_viewId) as View;
        }

        private void ApplyFilterToTree()
        {
            foreach (var category in Categories)
                category.ApplyFilter(_filter, _showInstances);
        }

        private void UpdateStatus()
        {
            var shown = 0;
            var hidden = 0;

            foreach (var family in Categories.SelectMany(c => c.Families))
            {
                if (family.BuiltInstances == null)
                {
                    if (family.IsChecked == true) shown += family.Count;
                    else hidden += family.Count;
                }
                else
                {
                    foreach (var instance in family.BuiltInstances)
                    {
                        if (instance.IsChecked == true) shown++;
                        else hidden++;
                    }
                }
            }

            ShownCount = shown;
            HiddenCount = hidden;
            StatusText = DescribeSelection();
        }

        private string DescribeSelection()
        {
            var node = SelectedNode;
            if (node == null)
                return $"Категорий: {Categories.Count}";

            switch (node.Kind)
            {
                case NodeKind.Category:
                    return $"Категория: {node.Name} · экземпляров: {node.Count}";
                case NodeKind.Family:
                    return $"Семейство: {node.Name} · экземпляров: {node.Count}";
                default:
                    return $"Экземпляр: {node.Name}";
            }
        }

        public void OnWindowClosing()
        {
            if (_viewSaved)
                return;

            var viewId = _viewId;
            _ = _revitTask.Run(app => _service.DeleteView(_doc, viewId));
        }
    }
}

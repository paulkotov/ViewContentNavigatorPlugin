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
        private readonly Document _doc;
        private readonly string _documentTitle;

        private ElementId _viewId;
        private bool _viewSaved;
        private bool _suppress;

        private string _viewName;
        private string _viewNameInput;
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
            Document doc,
            View3D view,
            ViewSnapshot snapshot)
        {
            _revitTask = revitTask;
            _service = service;
            _doc = doc;
            _documentTitle = doc.Title;
            _viewId = view.Id;
            _viewName = snapshot.ViewName;
            _viewNameInput = snapshot.ViewName;

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
            SaveViewAsCommand = new AsyncRelayCommand(
                SaveViewAsAsync,
                () => !string.IsNullOrWhiteSpace(ViewNameInput));
            ClearFilterCommand = new RelayCommand(_ => Filter = string.Empty);
            ExpandAllCommand = new RelayCommand(_ => ExpandCollapseAll(true));
            CollapseAllCommand = new RelayCommand(_ => ExpandCollapseAll(false));
            SelectAllCommand = new RelayCommand(_ => SelectDeselectAll(true));
            DeselectAllCommand = new RelayCommand(_ => SelectDeselectAll(false));

            // Новый временный вид всегда открывается в исходном состоянии:
            // все элементы видимы (все чекбоксы включены). Прошлые выборы
            // намеренно не восстанавливаются.
            UpdateStatus();
        }

        public ObservableCollection<CategoryNodeViewModel> Categories { get; }
        public IReadOnlyList<PaletteColor> Palette { get; }

        public string ViewName
        {
            get => _viewName;
            private set => SetProperty(ref _viewName, value);
        }

        // Редактируемое имя, под которым будет сохранён вид. По умолчанию —
        // временное имя вида навигатора.
        public string ViewNameInput
        {
            get => _viewNameInput;
            set => SetProperty(ref _viewNameInput, value);
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
            set
            {
                if (SetProperty(ref _autoApply, value) && value)
                {
                    // Включили «Авто» — сразу применяем накопленное состояние дерева к виду.
                    _ = SyncViewWithTreeAsync();
                }
            }
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
        public AsyncRelayCommand SaveViewAsCommand { get; }
        public RelayCommand ClearFilterCommand { get; }
        public RelayCommand ExpandAllCommand { get; }
        public RelayCommand CollapseAllCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand DeselectAllCommand { get; }

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

        void INodeChangeSink.RequestOpacity(TreeNodeViewModel node, int opacity)
        {
            if (AutoApply)
                _ = PushOpacityAsync(node, opacity);
            else
                UpdateStatus();
        }

        private void ApplySelected(bool visible)
        {
            var node = SelectedNode;
            if (node == null) return;

            node.SetCheckedSilently(visible);

            if (AutoApply)
                _ = PushVisibilityAsync(node, visible);
            else
                UpdateStatus();
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

            if (AutoApply)
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

                if (AutoApply)
                    _ = PushColorAsync(node, media);
            }
        }

        private void ResetSelectedColor()
        {
            var node = SelectedNode;
            if (node == null) return;

            node.SetColorSilently(null);

            if (AutoApply)
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
                    category.SetOpacitySilently(100);
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

        private async Task PushOpacityAsync(TreeNodeViewModel node, int opacity)
        {
            var ids = node.ElementIds;
            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.SetOpacity(_doc, view, ids, opacity);
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
                        states.Add(new NodeState(instance.ElementIds, instance.IsChecked != false, instColor, instance.Opacity));
                    }
                }
                else
                {
                    var color = family.Color.HasValue ? ColorMapper.ToRevit(family.Color.Value) : null;
                    states.Add(new NodeState(family.ElementIds, family.IsChecked != false, color, family.Opacity));
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
                    categoryNode.SetOpacitySilently(cs.Opacity);

                    var familiesByName = GroupUnique(categoryNode.Families, f => f.Name);
                    foreach (var fs in cs.Families)
                    {
                        if (!familiesByName.TryGetValue(fs.Name, out var familyNode))
                            continue;

                        familyNode.SetCheckedSilently(fs.Visible);
                        if (fs.Color != null && fs.Color.HasValue)
                            familyNode.SetColorSilently(ColorMapper.ToMedia(fs.Color));
                        familyNode.SetOpacitySilently(fs.Opacity);

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
                                instanceNode.SetOpacitySilently(isetting.Opacity);
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
                    Color = ColorMapper.ToSerializable(category.Color),
                    Opacity = category.Opacity
                };

                foreach (var family in category.Families)
                {
                    var fs = new FamilySetting
                    {
                        Name = family.Name,
                        Visible = family.IsChecked != false,
                        Color = ColorMapper.ToSerializable(family.Color),
                        Opacity = family.Opacity
                    };

                    if (family.BuiltInstances != null)
                    {
                        foreach (var instance in family.BuiltInstances)
                        {
                            if (instance.IsChecked != false && !instance.HasColor && !instance.HasCustomOpacity)
                                continue;

                            fs.Instances.Add(new InstanceSetting
                            {
                                Id = instance.ElementIdValue,
                                Visible = instance.IsChecked != false,
                                Color = ColorMapper.ToSerializable(instance.Color),
                                Opacity = instance.Opacity
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

        private void ExpandCollapseAll(bool expand)
        {
            RunSuppressed(() =>
            {
                foreach (var category in Categories)
                    category.IsExpanded = expand;
            });
        }

        private void SelectDeselectAll(bool select)
        {
            RunSuppressed(() =>
            {
                foreach (var category in Categories)
                {
                    category.SetCheckedSilently(select);
                    foreach (var family in category.Families)
                    {
                        family.SetCheckedSilently(select);
                        if (family.BuiltInstances != null)
                            foreach (var instance in family.BuiltInstances)
                                instance.SetCheckedSilently(select);
                    }
                }
            });

            if (AutoApply)
                _ = SyncViewWithTreeAsync();

            UpdateStatus();
        }

        private async Task SaveViewAsAsync()
        {
            var name = (ViewNameInput ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(name))
                return;

            await _revitTask.Run(app =>
            {
                var view = ResolveView();
                if (view != null)
                    _service.SaveView(_doc, view, name);
            });

            // Вид сохранён под заданным именем — при закрытии окна подтверждение
            // спрашивать не нужно (ShouldPromptOnClose станет false).
            _viewSaved = true;
            ViewName = name;
            ViewNameInput = name;
        }

        // Нужно ли спрашивать пользователя при закрытии окна: только если вид ещё
        // существует и не был сохранён.
        public bool ShouldPromptOnClose =>
            !_viewSaved
            && _viewId != null
            && _viewId.IntegerValue != ElementId.InvalidElementId.IntegerValue;

        public void DeleteTemporaryView()
        {
            if (!ShouldPromptOnClose)
                return;

            var viewId = _viewId;

            // ВАЖНО: не блокируем поток через .Wait() — окно закрывается на главном
            // потоке Revit, а ExternalEvent выполняется на нём же. Блокировка привела бы
            // к дедлоку. Просто ставим задачу в очередь — она выполнится, когда Revit
            // освободит главный поток (сразу после закрытия окна).
            _ = _revitTask.Run(app => _service.DeleteView(app, viewId));
        }
    }
}

using System.Windows;
using ViewContentNavigator.ViewModels;

namespace ViewContentNavigator.Views
{
    public partial class NavigatorWindow : Window
    {
        private readonly NavigatorViewModel _viewModel;

        public NavigatorWindow(NavigatorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;

            Tree.SelectedItemChanged += (_, e) =>
                _viewModel.SelectedNode = e.NewValue as TreeNodeViewModel;

            Closing += (_, __) => _viewModel.OnWindowClosing();
        }
    }
}

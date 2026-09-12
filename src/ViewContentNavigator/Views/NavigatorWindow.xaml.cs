using System.ComponentModel;
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

            Closing += OnWindowClosing;
        }

        private void OnWindowClosing(object sender, CancelEventArgs e)
        {
            // Если вид уже сохранён/удалён — просто закрываем окно.
            if (!_viewModel.ShouldPromptOnClose)
                return;

            var result = MessageBox.Show(
                this,
                "Удалить временный вид навигатора?\n\n" +
                "«Да» — вид будет удалён из проекта.\n" +
                "«Нет» — вид останется в обозревателе проекта.",
                "Навигатор содержимого",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            switch (result)
            {
                case MessageBoxResult.Cancel:
                    // Отменяем закрытие окна.
                    e.Cancel = true;
                    break;

                case MessageBoxResult.Yes:
                    _viewModel.DeleteTemporaryView();
                    break;

                // MessageBoxResult.No — оставляем вид как есть, окно закрывается.
            }
        }
    }
}

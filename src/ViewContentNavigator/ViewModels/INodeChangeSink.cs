using System.Windows.Media;

namespace ViewContentNavigator.ViewModels
{
    public interface INodeChangeSink
    {
        bool SuppressNotifications { get; }

        void RequestVisibility(TreeNodeViewModel node, bool visible);

        void RequestColor(TreeNodeViewModel node, Color? color);
    }
}

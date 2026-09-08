using System;
using System.Reflection;
using Autodesk.Revit.UI;
using ViewContentNavigator.Commands;

namespace ViewContentNavigator.Application
{
    public sealed class NavigatorApplication : IExternalApplication
    {
        private const string TabName = "Навигатор";
        private const string PanelName = "Содержимое вида";

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                try
                {
                    application.CreateRibbonTab(TabName);
                }
                catch { }

                var panel = application.CreateRibbonPanel(TabName, PanelName);
                var assemblyPath = Assembly.GetExecutingAssembly().Location;

                var buttonData = new PushButtonData(
                    "VCN_ShowNavigator",
                    "Навигатор\nсодержимого",
                    assemblyPath,
                    typeof(ShowNavigatorCommand).FullName)
                {
                    ToolTip = "Создать временный 3D-вид и открыть навигатор категорий и элементов модели.",
                    LongDescription =
                        "Строит дерево «Категория → Семейство → Экземпляр» по содержимому вида, " +
                        "позволяет скрывать/показывать и раскрашивать элементы на лету, " +
                        "а также сохранять настройки и вид.",
                    AvailabilityClassName = typeof(DocumentAvailability).FullName
                };

                try
                {
                    buttonData.LargeImage = RibbonIconFactory.CreateLarge();
                    buttonData.Image = RibbonIconFactory.CreateSmall();
                }
                catch { }

                panel.AddItem(buttonData);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Навигатор содержимого", "Ошибка инициализации:\n" + ex);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
    }
}

using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using ViewContentNavigator.Revit;
using ViewContentNavigator.Services;
using ViewContentNavigator.ViewModels;
using ViewContentNavigator.Views;

namespace ViewContentNavigator.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public sealed class ShowNavigatorCommand : IExternalCommand
    {
        private static NavigatorWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                if (_window != null)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc?.Document == null)
                {
                    message = "Нет активного документа.";
                    return Result.Cancelled;
                }

                var doc = uiDoc.Document;

                IViewContentService service = new ViewContentService();
                IRevitTask revitTask = new RevitTask();

                var view = service.CreateNavigatorView(doc);
                uiDoc.ActiveView = view;

                var snapshot = service.Scan(doc, view);
                var viewModel = new NavigatorViewModel(revitTask, service, doc, view, snapshot);

                _window = new NavigatorWindow(viewModel);
                new WindowInteropHelper(_window) { Owner = uiApp.MainWindowHandle };
                _window.Closed += (_, __) => _window = null;
                _window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Навигатор содержимого", "Не удалось открыть навигатор:\n" + ex);
                return Result.Failed;
            }
        }
    }
}

using Autodesk.Revit.DB;
using ViewContentNavigator.Models;

namespace ViewContentNavigator.Services
{
    public interface IDocumentSettingsStore
    {
        NavigatorSettings Load(Document doc);

        void Save(Document doc, NavigatorSettings settings);
    }
}

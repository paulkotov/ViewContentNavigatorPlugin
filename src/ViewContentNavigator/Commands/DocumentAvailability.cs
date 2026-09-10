using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ViewContentNavigator.Commands
{
    public sealed class DocumentAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication applicationData, CategorySet selectedCategories)
        {
            return applicationData?.ActiveUIDocument?.Document != null;
        }
    }
}

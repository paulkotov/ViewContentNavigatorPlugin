using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace ViewContentNavigator.Revit
{
    public interface IRevitTask
    {
        Task<T> Run<T>(Func<UIApplication, T> func);

        Task Run(Action<UIApplication> action);
    }
}

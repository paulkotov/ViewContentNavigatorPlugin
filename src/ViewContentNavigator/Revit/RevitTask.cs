using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace ViewContentNavigator.Revit
{
    public sealed class RevitTask : IRevitTask
    {
        private readonly ExternalEvent _externalEvent;
        private readonly RevitTaskHandler _handler;

        public RevitTask()
        {
            _handler = new RevitTaskHandler();
            _externalEvent = ExternalEvent.Create(_handler);
        }

        public Task<T> Run<T>(Func<UIApplication, T> func)
        {
            if (func == null) throw new ArgumentNullException(nameof(func));

            var tcs = new TaskCompletionSource<T>();
            _handler.Enqueue(app =>
            {
                try
                {
                    tcs.SetResult(func(app));
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            _externalEvent.Raise();
            return tcs.Task;
        }

        public Task Run(Action<UIApplication> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            return Run<object>(app =>
            {
                action(app);
                return null;
            });
        }

        private sealed class RevitTaskHandler : IExternalEventHandler
        {
            private readonly ConcurrentQueue<Action<UIApplication>> _jobs =
                new ConcurrentQueue<Action<UIApplication>>();

            public void Enqueue(Action<UIApplication> job) => _jobs.Enqueue(job);

            public void Execute(UIApplication app)
            {
                while (_jobs.TryDequeue(out var job))
                {
                    job(app);
                }
            }

            public string GetName() => "ViewContentNavigator.RevitTask";
        }
    }
}

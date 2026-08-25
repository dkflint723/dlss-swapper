using System;
using System.Threading.Tasks;

namespace DLSS_Swapper;

internal static class UiThread
{
    /// <summary>
    /// Marshals an action onto the UI thread, or runs it inline when there is no application to
    /// marshal to.
    /// </summary>
    /// <remarks>
    /// App.CurrentApp is Application.Current cast to App, so it is null whenever no application is
    /// running. That is the case under test, where calling App.CurrentApp.RunOnUIThread directly
    /// throws before the dispatcher is ever consulted. Running inline is the correct behaviour
    /// there: with no UI thread in existence, there is nothing to marshal to and no thread
    /// affinity to respect. The running app never takes this path.
    /// </remarks>
    internal static bool Run(Action action)
    {
        var app = App.CurrentApp;
        if (app is null)
        {
            action();
            return true;
        }

        return app.RunOnUIThread(action);
    }

    /// <summary>
    /// The same for work that has to be awaited.
    /// </summary>
    /// <remarks>
    /// Game.ProcessGame's finally block called App.CurrentApp.RunOnUIThreadAsync straight, which
    /// dereferences a null whenever no application is running - so the scan could not be driven from
    /// a test at all, and the same call would throw on the way out of the app. Both go through here
    /// now, for the reason on Run above.
    /// </remarks>
    internal static Task RunAsync(Func<Task> function)
    {
        var app = App.CurrentApp;
        if (app is null)
        {
            return function();
        }

        return app.RunOnUIThreadAsync(function);
    }
}

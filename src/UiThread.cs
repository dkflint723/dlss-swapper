using System;

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
}

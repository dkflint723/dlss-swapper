using System;
using System.Threading.Tasks;

namespace DLSS_Swapper;

/// <summary>
/// Whatever can marshal work onto the thread that draws.
/// </summary>
/// <remarks>
/// An interface rather than a direct call to App, because the data layer this is used from is
/// compiled without the app now - the command line reaches a Game and a swap through it, and there
/// is no Application there to ask.
/// </remarks>
internal interface IUiDispatcher
{
    bool Run(Action action);

    Task RunAsync(Func<Task> function);

    /// <summary>
    /// Queues work for the next chance the UI gets, returning false when it cannot be queued.
    /// </summary>
    /// <remarks>
    /// Separate from Run because the caller wants to know whether the work was taken rather than
    /// whether it happened: it coalesces, and something that could not be queued has to be done
    /// immediately instead. False covers both a window that does not exist yet and a queue that is
    /// shutting down, which the caller treats the same way.
    /// </remarks>
    bool TryEnqueue(Action action);
}

internal static class UiThread
{
    /// <summary>
    /// Set by the app while it starts. Null everywhere else, which is the case that matters.
    /// </summary>
    internal static IUiDispatcher? Dispatcher { get; set; }

    /// <summary>
    /// Marshals an action onto the UI thread, or runs it inline when there is nothing to marshal to.
    /// </summary>
    /// <remarks>
    /// This used to read App.CurrentApp, which is Application.Current cast to App and so null
    /// whenever no application is running. The behaviour is unchanged and the reasoning still
    /// holds: with no UI thread in existence there is nothing to marshal to and no thread affinity
    /// to respect, so running inline is correct. That is the path taken under test and by the
    /// command line; the running app never takes it.
    /// </remarks>
    internal static bool Run(Action action)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
        {
            action();
            return true;
        }

        return dispatcher.Run(action);
    }

    /// <summary>
    /// The same for work that has to be awaited.
    /// </summary>
    /// <remarks>
    /// Game.ProcessGame's finally block called through to the application directly once, which
    /// dereferenced a null whenever none was running - so the scan could not be driven from a test
    /// at all, and the same call would throw on the way out of the app. Both go through here now,
    /// for the reason on Run above.
    /// </remarks>
    internal static Task RunAsync(Func<Task> function)
    {
        var dispatcher = Dispatcher;
        if (dispatcher is null)
        {
            return function();
        }

        return dispatcher.RunAsync(function);
    }
}

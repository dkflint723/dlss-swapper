using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;

namespace DLSS_Swapper.UserControls;

/// <summary>
/// Every swap this game has been through.
/// </summary>
/// <remarks>
/// <para>
/// The rows are read after the control is on screen, which is fine, but nothing about how they
/// arrived used to survive that. They landed in a <c>List</c>, which raises nothing when it gains
/// items, behind an <c>ItemsSource</c> bound OneTime and two visibility bindings that were also
/// OneTime - so the dialog decided there was no history before the query had returned, and then had
/// no way to change its mind. A game with a long history usually showed "No history".
/// </para>
/// <para>
/// It also read the database without taking the mutex, the only one of the app's connection users
/// that did.
/// </para>
/// </remarks>
public partial class GameHistoryControlModel : ObservableObject
{
    public GameHistoryControlModelTranslationProperties TranslationProperties { get; } = new GameHistoryControlModelTranslationProperties();

    public ObservableCollection<GameHistory> HistoryRows { get; } = new ObservableCollection<GameHistory>();

    /// <summary>
    /// Whether the query has come back, whatever it came back with.
    /// </summary>
    /// <remarks>
    /// Kept apart from the row count so an empty result and a query still running are not the same
    /// thing on screen: "No history" is an answer, and it should not be given before there is one.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HistoryVisibility))]
    [NotifyPropertyChangedFor(nameof(NoHistoryVisibility))]
    public partial bool HasLoaded { get; set; }

    public Visibility HistoryVisibility => HasLoaded && HistoryRows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoHistoryVisibility => HasLoaded && HistoryRows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public GameHistoryControlModel(GameHistoryControl control, Game game)
    {
        _ = LoadAsync(game);
    }

    /// <summary>
    /// Reads this game's history and puts it on screen.
    /// </summary>
    /// <remarks>
    /// Observed rather than fired and forgotten. The read was a bare <c>Task.Run</c> assigned to
    /// nothing, so a failure in it went nowhere at all and the dialog simply stayed empty.
    /// </remarks>
    async Task LoadAsync(Game game)
    {
        try
        {
            var historyRows = new System.Collections.Generic.List<GameHistory>();

            using (await Database.Instance.Mutex.LockAsync())
            {
                historyRows = await Database.Instance.Connection.Table<GameHistory>()
                    .Where(x => x.GameId == game.ID)
                    .ToListAsync()
                    .ConfigureAwait(false);
            }

            var ordered = historyRows.OrderByDescending(x => x.EventTime).ToList();

            App.CurrentApp.RunOnUIThread(() =>
            {
                foreach (var historyRow in ordered)
                {
                    HistoryRows.Add(historyRow);
                }

                HasLoaded = true;
            });
        }
        catch (Exception err)
        {
            Logger.Error(err, $"Could not read the history for {game.Title}.");

            App.CurrentApp.RunOnUIThread(() => HasLoaded = true);
        }
    }
}

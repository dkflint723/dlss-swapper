using System;
using SQLite;

namespace DLSS_Swapper.Data;

/// <summary>
/// One dll in one game that the user has pinned where it is, and why.
/// </summary>
/// <remarks>
/// <para>
/// The memory this app never had: you swap a game to the newest dll, it ghosts or crashes, you
/// roll back to the one that works — and nothing remembers any of it, so the next "update all"
/// cheerfully offers the bad version again. "Never update this game" exists but is all or
/// nothing: protecting one dll meant giving up updates for every other dll in the game.
/// </para>
/// <para>
/// A pin means: no batch moves this dll — not an update run, not a restore run. The picker on the
/// game's own page still can, because acting on that exact dll deliberately is how a pin gets
/// reconsidered. The reason is the user's own sentence to their future self, shown on the row.
/// </para>
/// <para>
/// Its own table rather than columns on the game or its assets: asset rows are deleted and
/// rewritten by every scan, and a pin has to outlive the scans of the game it protects.
/// </para>
/// </remarks>
[Table("game_dll_pin")]
public class GameDllPin
{
    [Indexed]
    [Column("game_id")]
    public string GameId { get; set; } = string.Empty;

    [Column("asset_type")]
    public GameAssetType AssetType { get; set; }

    /// <summary>Why this dll is held, in the user's words. May be empty; never invented.</summary>
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    [Column("pinned_at")]
    public DateTime PinnedAt { get; set; } = DateTime.MinValue;
}

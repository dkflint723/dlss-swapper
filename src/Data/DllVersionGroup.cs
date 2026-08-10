using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DLSS_Swapper.Data;

/// <summary>
/// One release line's worth of dll versions, under a heading.
/// </summary>
public class DllVersionGroup
{
    public required string Label { get; init; }

    public ObservableCollection<DLLRecord> Versions { get; } = new ObservableCollection<DLLRecord>();

    /// <summary>
    /// Splits an ordered list of versions into the groups the page shows.
    /// </summary>
    /// <param name="engineName">Names the heading, since a bare "310" says nothing on its own.</param>
    /// <remarks>
    /// The order given is kept. These records are already ranked by the manifest's own rules, and a
    /// second opinion here about which is newest is how a list ends up disagreeing with its
    /// headings.
    /// </remarks>
    public static List<DllVersionGroup> Build(IReadOnlyList<DLLRecord> orderedRecords, string engineName)
    {
        var lines = DllVersionLine.AssignLines(orderedRecords.Select(x => x.DisplayVersion).ToList());
        var distinctLines = lines.Distinct().ToList();

        // More lines than are shown separately means the last one is the rolled up tail, and its
        // heading has to say so rather than claiming to be just that line.
        var rolledUpKey = distinctLines.Count > DllVersionLine.SeparateLines
            ? distinctLines[^1]
            : null;

        var groups = new List<DllVersionGroup>();
        DllVersionGroup? current = null;
        var currentKey = (string?)null;

        for (var index = 0; index < orderedRecords.Count; index++)
        {
            var key = lines[index];

            if (current is null || key != currentKey)
            {
                current = new DllVersionGroup()
                {
                    Label = DllVersionLine.Label(engineName, key, key == rolledUpKey),
                };

                groups.Add(current);
                currentKey = key;
            }

            current.Versions.Add(orderedRecords[index]);
        }

        return groups;
    }
}

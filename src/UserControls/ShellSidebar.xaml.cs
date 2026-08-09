using System;
using DLSS_Swapper.Data;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

public sealed partial class ShellSidebar : UserControl
{
    /// <summary>Raised when a nav item is chosen. The window owns navigation, not the sidebar.</summary>
    public event EventHandler<ShellSection>? SectionInvoked;

    /// <summary>Raised by the backup card's link. Goes to Games, filtered to the games missing one.</summary>
    public event EventHandler? FixMissingBackupsInvoked;

    public ShellSidebarModel ViewModel { get; private set; }

    public ShellSidebar()
    {
        this.InitializeComponent();
        ViewModel = new ShellSidebarModel();

        // The library is empty when this is built, so the counts are taken again whenever it
        // changes rather than relying on every caller that loads games to say so.
        GameManager.Instance.GamesChanged += (sender, args) =>
        {
            UiThread.Run(ViewModel.Refresh);
        };
    }

    void NavItem_Invoked(object? sender, ShellSection section)
    {
        ViewModel.ActiveSection = section;
        SectionInvoked?.Invoke(this, section);
    }

    void FixTheOthers_Click(object sender, RoutedEventArgs e)
    {
        FixMissingBackupsInvoked?.Invoke(this, EventArgs.Empty);
    }
}

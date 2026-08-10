namespace TrayMenu;

/// <summary>
/// Context menu that does not appear on the Windows taskbar.
/// </summary>
internal sealed class NoTaskbarContextMenuStrip : ContextMenuStrip
{
    private const int WsExToolWindow = 0x00000080;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow;
            return cp;
        }
    }
}

/// <summary>
/// Nested drop-down that does not appear on the Windows taskbar.
/// </summary>
internal sealed class NoTaskbarDropDownMenu : ToolStripDropDownMenu
{
    private const int WsExToolWindow = 0x00000080;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WsExToolWindow;
            return cp;
        }
    }
}

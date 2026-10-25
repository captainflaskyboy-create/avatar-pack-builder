namespace AvatarPackBuilder.Models;

public sealed class GridSettings
{
    public int Rows { get; set; } = 3;
    public int Columns { get; set; } = 3;
    public int CellWidth { get; set; } = 512;
    public int CellHeight { get; set; } = 512;
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public int SpacingX { get; set; }
    public int SpacingY { get; set; }
}

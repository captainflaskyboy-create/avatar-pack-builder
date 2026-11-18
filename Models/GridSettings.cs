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

    public bool ContainsCell(int row, int column, int sheetWidth, int sheetHeight)
    {
        long x = OffsetX + (long)column * (CellWidth + (long)SpacingX);
        long y = OffsetY + (long)row * (CellHeight + (long)SpacingY);
        return x >= 0 && y >= 0 && x + CellWidth <= sheetWidth && y + CellHeight <= sheetHeight;
    }

    public string? GetBoundsError(int sheetWidth, int sheetHeight)
    {
        if (Rows <= 0 || Columns <= 0 || CellWidth <= 0 || CellHeight <= 0 ||
            OffsetX < 0 || OffsetY < 0 || SpacingX < 0 || SpacingY < 0)
            return "Rows, columns and cell sizes must be positive; offsets and gaps cannot be negative.";

        long right = OffsetX + (long)Columns * CellWidth + (Columns - 1L) * SpacingX;
        long bottom = OffsetY + (long)Rows * CellHeight + (Rows - 1L) * SpacingY;
        if (right <= sheetWidth && bottom <= sheetHeight)
            return null;

        string message = $"Grid extends to {right} x {bottom}px, but the sheet is {sheetWidth} x {sheetHeight}px. ";
        long maxWidth = (sheetWidth - (long)OffsetX - (Columns - 1L) * SpacingX) / Columns;
        long maxHeight = (sheetHeight - (long)OffsetY - (Rows - 1L) * SpacingY) / Rows;
        if (right > sheetWidth && maxWidth > 0)
            message += $"For {Columns} columns, Cell W must be at most {maxWidth}px. ";
        if (bottom > sheetHeight && maxHeight > 0)
            message += $"For {Rows} rows, Cell H must be at most {maxHeight}px. ";
        return message + "Adjust cell sizes, rows/columns, offsets or gaps so every cell fits.";
    }
}

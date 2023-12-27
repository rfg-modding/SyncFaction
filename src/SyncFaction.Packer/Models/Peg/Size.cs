namespace SyncFaction.Packer.Models.Peg;

public record Size(int Width, int Height)
{
    public override string ToString()
    {
        return $"({Width}x{Height})";
    }
}

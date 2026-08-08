namespace WSGameServer;

public sealed class Item(int itemId, int count)
{
    public int Id { get; init; } = itemId;
    public int Count { get; set; } = count;
}
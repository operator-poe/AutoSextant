namespace AutoSextant.PoEStack.Api.Response;

public class ItemGroupProperty
{
    public string key { get; set; }
    public string value { get; set; }
}

public class Itemgroup
{
    public string key { get; set; }
    public string displayName { get; set; }
    public ItemGroupProperty[]? properties { get; set; }
}
namespace DndHelper.Data.Open5e;

public sealed class Open5eOptions
{
    public string BaseUrl { get; set; } = "https://api.open5e.com/";

    public List<string> DocumentKeys { get; set; } = ["srd-2024"];

    public int PageSize { get; set; } = 100;
}
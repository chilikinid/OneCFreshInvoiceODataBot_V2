namespace OneCFreshInvoiceODataBot.Models;

public sealed class ODataEntity
{
    public string EntitySet { get; init; } = string.Empty;
    public string RefKey { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, object?> Raw { get; init; } = [];
}

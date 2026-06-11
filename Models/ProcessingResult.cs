namespace OneCFreshInvoiceODataBot.Models;

public sealed class ProcessingResult
{
    public string? ExcelRowNumbers { get; init; }
    public string CounterpartyInn { get; init; } = string.Empty;
    public string NomenclatureNames { get; init; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? InvoiceRefKey { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? RealizationRefKey { get; set; }
    public string? RealizationNumber { get; set; }
    public string? IssuedInvoiceRefKey { get; set; }
    public string? IssuedInvoiceNumber { get; set; }
    public string? Error { get; set; }
    public List<string> Attachments { get; } = [];
    public string? Number { get; internal set; }
}

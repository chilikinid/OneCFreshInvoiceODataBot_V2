namespace OneCFreshInvoiceODataBot.Models;

public class InvoiceItemData
{
    public int ExcelRowNumber { get; init; }
    public string NomenclatureName { get; init; } = string.Empty;
    public string NomenclatureDescription { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public string VatRate { get; init; } = string.Empty;
    public decimal Amount => Quantity * Price;
    public decimal? VatAmount { get; init; }
}

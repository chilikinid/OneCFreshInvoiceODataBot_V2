namespace OneCFreshInvoiceODataBot.Models;

public sealed class InvoiceData
{
    public DateTime DocumentDate { get; init; }
    public string CounterpartyInn { get; init; } = string.Empty;
    public string CounterpartyName { get; init; } = string.Empty;
    public string AgreementName { get; init; } = string.Empty;
    public string BankAccount { get; init; } = string.Empty;
    public string Number { get; init; } = string.Empty;
    public string ExcelRowNumbers => string.Join(", ", InvoiceItems.Select(i => i.ExcelRowNumber.ToString()));
    public List<InvoiceItemData> InvoiceItems { get;  } = [];

    public string HumanKey => $"строка {ExcelRowNumbers}, ИНН {CounterpartyInn}, номенклатура '{string.Join(", ", InvoiceItems.Select(i => i.NomenclatureName))}'";

    public string NomenclatureNames => string.Join(", ", InvoiceItems.Select(i => i.NomenclatureName).Distinct());
}

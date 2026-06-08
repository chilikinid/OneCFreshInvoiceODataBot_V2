using ClosedXML.Excel;

using OneCFreshInvoiceODataBot.Models;

using System.Globalization;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class ExcelInvoiceReader
{
    private static readonly string[] RequiredHeaders =
    [
        "Дата документа",
        "ИНН Контрагента",
        "Договор",
        "Номенклатура",
        "Количество",
        "Цена",
        "СтавкаНДС"
    ];

    public IReadOnlyList<InvoiceData> Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"Excel-файл не найден: {path}", path);

        using var workbook = new XLWorkbook(path);
        var worksheet = workbook.Worksheets.First();
        var used = worksheet.RangeUsed() ?? throw new InvalidOperationException("В Excel-файле нет данных.");

        var headerRow = used.FirstRowUsed();
        var headers = headerRow.CellsUsed()
            .ToDictionary(c => NormalizeHeader(c.GetString()), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

        foreach (var header in RequiredHeaders)
        {
            if (!headers.ContainsKey(NormalizeHeader(header)))
                throw new InvalidOperationException($"В Excel нет обязательной колонки: '{header}'.");
        }

        var result = new List<InvoiceData>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                continue;

            var rowNumber = row.RowNumber();
            var number = ReadString(row.Cell(headers[NormalizeHeader("Номер")]));
            var documentDate = ReadDate(row.Cell(headers[NormalizeHeader("Дата документа")]));
            var counterpartyInn = ReadInn(row.Cell(headers[NormalizeHeader("ИНН Контрагента")]));
            var counterpartyName = ReadFirstOptionalString(row, headers,
                "Контрагент",
                "Наименование Контрагента",
                "Наименование контрагента",
                "Контрагент - описание");
            if (string.IsNullOrWhiteSpace(counterpartyName))
                counterpartyName = counterpartyInn;

            var agreementName = ReadString(row.Cell(headers[NormalizeHeader("Договор")]));
            var bankAccount = ReadOptionalString(row, headers, "Банковский счет");

            var nomenclatureName = ReadString(row.Cell(headers[NormalizeHeader("Номенклатура")]));
            var nomenclatureDescription= ReadString(row.Cell(headers[NormalizeHeader("Номенклатура - описание")]));
            var quantity = ReadDecimal(row.Cell(headers[NormalizeHeader("Количество")]), "Количество", rowNumber);
            var price = ReadDecimal(row.Cell(headers[NormalizeHeader("Цена")]), "Цена", rowNumber);
            var vatRate = ReadString(row.Cell(headers[NormalizeHeader("СтавкаНДС")]));
            var vatAmount = ReadOptionalNullableDecimal(row, headers, "СуммаНДС", rowNumber);

            bool isNew = false;

            var invoice = result.FirstOrDefault(m => m.Number == number
                                                && m.DocumentDate == documentDate
                                                && m.CounterpartyInn == counterpartyInn
                                                && m.CounterpartyName == counterpartyName
                                                && m.AgreementName == agreementName
                                                && m.BankAccount == bankAccount
            );
            if (invoice == null)
            {
                invoice = new InvoiceData
                {
                    DocumentDate = documentDate,
                    CounterpartyInn = counterpartyInn,
                    CounterpartyName = counterpartyName,
                    AgreementName = agreementName,
                    BankAccount = bankAccount,
                    Number = number,
                };
                isNew = true;
            }
            var invoiceItem = new InvoiceItemData
            {
                ExcelRowNumber = rowNumber,
                NomenclatureName = nomenclatureName,
                NomenclatureDescription = nomenclatureDescription,
                Quantity = quantity,
                Price = price,
                VatRate = vatRate,
                VatAmount = vatAmount,
            };
            invoice.InvoiceItems.Add(invoiceItem);

            Validate(invoice);
            if (isNew)
            {
                result.Add(invoice);
            }
        }

        if (result.Count == 0)
            throw new InvalidOperationException("В Excel нет строк для обработки.");

        return result;
    }

    private static string NormalizeHeader(string value) => value.Trim().Replace("ё", "е", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(IXLCell cell) => cell.GetFormattedString().Trim();

    private static string ReadOptionalString(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        return headers.TryGetValue(NormalizeHeader(header), out var columnNumber)
            ? ReadString(row.Cell(columnNumber))
            : string.Empty;
    }

    private static string ReadFirstOptionalString(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] headerNames)
    {
        foreach (var header in headerNames)
        {
            var value = ReadOptionalString(row, headers, header);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string ReadInn(IXLCell cell)
    {
        var value = cell.GetFormattedString().Trim();
        value = new string([.. value.Where(char.IsDigit)]);
        return value;
    }

    private static DateTime ReadDate(IXLCell cell)
    {
        if (cell.TryGetValue<DateTime>(out var date))
            return date.Date;

        if (cell.TryGetValue<double>(out var serial))
            return DateTime.FromOADate(serial).Date;

        var text = cell.GetFormattedString().Trim();
        if (DateTime.TryParse(text, CultureInfo.GetCultureInfo("ru-RU"), DateTimeStyles.AssumeLocal, out var parsed))
            return parsed.Date;

        throw new InvalidOperationException($"Не удалось прочитать дату из ячейки {cell.Address}: '{text}'.");
    }

    private static decimal ReadDecimal(IXLCell cell, string fieldName, int rowNumber)
    {
        if (cell.TryGetValue<decimal>(out var value))
            return value;

        var text = cell.GetFormattedString().Trim().Replace(" ", "").Replace(',', '.');
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;

        throw new InvalidOperationException($"Строка {rowNumber}: поле '{fieldName}' должно быть числом. Значение: '{cell.GetFormattedString()}'.");
    }

    private static decimal? ReadNullableDecimal(IXLCell cell, string fieldName, int rowNumber)
    {
        if (cell.DataType == XLDataType.Blank || string.IsNullOrWhiteSpace(cell.GetFormattedString())) return null;

        if (cell.TryGetValue<decimal>(out var value))
            return value;

        var text = cell.GetFormattedString().Trim().Replace(" ", "").Replace(',', '.');
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;

        throw new InvalidOperationException($"Строка {rowNumber}: поле '{fieldName}' должно быть числом. Значение: '{cell.GetFormattedString()}'.");
    }

    private static decimal? ReadOptionalNullableDecimal(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string header,
        int rowNumber)
    {
        return headers.TryGetValue(NormalizeHeader(header), out var columnNumber)
            ? ReadNullableDecimal(row.Cell(columnNumber), header, rowNumber)
            : null;
    }


    private static void Validate(InvoiceData invoice)
    {
        var errors = new List<string>();
        if (invoice.DocumentDate == default) errors.Add("дата документа не заполнена");
        if (string.IsNullOrWhiteSpace(invoice.CounterpartyInn)) errors.Add("ИНН контрагента не заполнен");
        ////if (string.IsNullOrWhiteSpace(invoice.AgreementName)) errors.Add("договор не заполнен");
        foreach (var item in invoice.InvoiceItems)
        {
            if (string.IsNullOrWhiteSpace(item.NomenclatureName)) errors.Add("номенклатура не заполнена");
            if (item.Quantity <= 0) errors.Add("количество должно быть больше нуля");
            if (item.Price < 0) errors.Add("цена не может быть отрицательной");
            ////if (string.IsNullOrWhiteSpace(item.VatRate)) errors.Add("ставка НДС не заполнена");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException($"Строки {invoice.ExcelRowNumbers}: {string.Join("; ", errors)}.");
    }
}

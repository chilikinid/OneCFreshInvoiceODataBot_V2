using ClosedXML.Excel;

using OneCFreshInvoiceODataBot.Models;

using System.Globalization;
using System.Linq;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class ExcelInvoiceReader
{
    private static readonly string[] _requiredHeaders =
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
            .ToDictionary(c => _NormalizeHeader(c.GetString()), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
        var missingHeaders = _requiredHeaders
            .Where(header => !headers.ContainsKey(_NormalizeHeader(header)))
            .ToList();
        if (missingHeaders.Count > 0)
        {
            throw new InvalidOperationException($"В Excel нет обязательных колонок: {string.Join(", ", missingHeaders.Select(h => $"'{h}'"))}.");
        }

        var result = new List<InvoiceData>();
        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            if (row.CellsUsed().All(c => string.IsNullOrWhiteSpace(c.GetString())))
                continue;

            var rowNumber = row.RowNumber();
            var number = _ReadString(row.Cell(headers[_NormalizeHeader("Номер")]));
            var documentDate = _ReadDate(row.Cell(headers[_NormalizeHeader("Дата документа")]));
            var counterpartyInn = _ReadInn(row.Cell(headers[_NormalizeHeader("ИНН Контрагента")]));
            var counterpartyName = _ReadFirstOptionalString(row, headers,
                "Контрагент",
                "Наименование Контрагента",
                "Наименование контрагента",
                "Контрагент - описание");
            if (string.IsNullOrWhiteSpace(counterpartyName))
                counterpartyName = counterpartyInn;

            var agreementName = _ReadString(row.Cell(headers[_NormalizeHeader("Договор")]));
            var bankAccount = _ReadOptionalString(row, headers, "Банковский счет");

            var nomenclatureName = _ReadString(row.Cell(headers[_NormalizeHeader("Номенклатура")]));
            var nomenclatureDescription= _ReadString(row.Cell(headers[_NormalizeHeader("Номенклатура - описание")]));
            var quantity = _ReadDecimal(row.Cell(headers[_NormalizeHeader("Количество")]), "Количество", rowNumber);
            var price = _ReadDecimal(row.Cell(headers[_NormalizeHeader("Цена")]), "Цена", rowNumber);
            var vatRate = _ReadString(row.Cell(headers[_NormalizeHeader("СтавкаНДС")]));
            var vatAmount = _ReadOptionalNullableDecimal(row, headers, "СуммаНДС", rowNumber);

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

            _Validate(invoice);
            if (isNew)
            {
                result.Add(invoice);
            }
        }

        if (result.Count == 0)
            throw new InvalidOperationException("В Excel нет строк для обработки.");

        return result;
    }

    private static string _NormalizeHeader(string value) => value.Trim().Replace("ё", "е", StringComparison.OrdinalIgnoreCase);

    private static string _ReadString(IXLCell cell) => cell.GetFormattedString().Trim();

    private static string _ReadOptionalString(IXLRow row, IReadOnlyDictionary<string, int> headers, string header)
    {
        return headers.TryGetValue(_NormalizeHeader(header), out var columnNumber)
            ? _ReadString(row.Cell(columnNumber))
            : string.Empty;
    }

    private static string _ReadFirstOptionalString(IXLRow row, IReadOnlyDictionary<string, int> headers, params string[] headerNames)
    {
        foreach (var header in headerNames)
        {
            var value = _ReadOptionalString(row, headers, header);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string _ReadInn(IXLCell cell)
    {
        var value = cell.GetFormattedString().Trim();
        value = new string([.. value.Where(char.IsDigit)]);
        return value;
    }

    private static DateTime _ReadDate(IXLCell cell)
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

    private static decimal _ReadDecimal(IXLCell cell, string fieldName, int rowNumber)
    {
        if (cell.TryGetValue<decimal>(out var value))
            return value;

        var text = cell.GetFormattedString().Trim().Replace(" ", "").Replace(',', '.');
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;

        throw new InvalidOperationException($"Строка {rowNumber}: поле '{fieldName}' должно быть числом. Значение: '{cell.GetFormattedString()}'.");
    }

    private static decimal? _ReadNullableDecimal(IXLCell cell, string fieldName, int rowNumber)
    {
        if (cell.DataType == XLDataType.Blank || string.IsNullOrWhiteSpace(cell.GetFormattedString())) return null;

        if (cell.TryGetValue<decimal>(out var value))
            return value;

        var text = cell.GetFormattedString().Trim().Replace(" ", "").Replace(',', '.');
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value))
            return value;

        throw new InvalidOperationException($"Строка {rowNumber}: поле '{fieldName}' должно быть числом. Значение: '{cell.GetFormattedString()}'.");
    }

    private static decimal? _ReadOptionalNullableDecimal(
        IXLRow row,
        IReadOnlyDictionary<string, int> headers,
        string header,
        int rowNumber)
    {
        return headers.TryGetValue(_NormalizeHeader(header), out var columnNumber)
            ? _ReadNullableDecimal(row.Cell(columnNumber), header, rowNumber)
            : null;
    }


    private static void _Validate(InvoiceData invoice)
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

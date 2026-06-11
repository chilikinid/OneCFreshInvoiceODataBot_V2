using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using OneCFreshInvoiceODataBot.Models;

using System.Globalization;
using System.Text.RegularExpressions;

namespace OneCFreshInvoiceODataBot.Services;

public sealed record InvoicePrintFormRequest(
    InvoiceData Invoice,
    ODataEntity Organization,
    ODataEntity Counterparty,
    ODataEntity? Agreement,
    ODataEntity? BankAccount,
    string? DocumentNumber,
    string OutputDir);

public sealed record RealizationPrintFormRequest(
    string Title,
    InvoiceData Invoice,
    ODataEntity Organization,
    ODataEntity Counterparty,
    ODataEntity? Agreement,
    string? DocumentNumber,
    string OutputDir);

public sealed partial class DocxPrintFormGenerator
{
    private static readonly CultureInfo _ru = CultureInfo.GetCultureInfo("ru-RU");
    private const int PageWidth = 9360;
    private const string UnderscoreLine = "__________________";

    public static async Task<string> CreateInvoiceAsync(InvoicePrintFormRequest request, CancellationToken ct)
    {
        var invoice = request.Invoice;
        var organization = request.Organization;
        var counterparty = request.Counterparty;
        var agreement = request.Agreement;
        var bankAccount = request.BankAccount;
        var documentNumber = request.DocumentNumber;
        var outputDir = request.OutputDir;

        Directory.CreateDirectory(outputDir);
        var path = _BuildOutputPath(outputDir, "Счет", documentNumber ?? invoice.Number);

        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(
                _BuildPaymentDetailsTable(organization, bankAccount),
                _Paragraph($"Счет на оплату № {documentNumber ?? invoice.Number} от {_FormatDate(invoice.DocumentDate)} г.", bold: true, fontSize: 28),
                _Separator(),
                _Paragraph($"Поставщик (Исполнитель): {_FormatParty(organization)}", boldLabel: true),
                _Paragraph($"Покупатель (Заказчик): {_FormatParty(counterparty)}", boldLabel: true),
                _Paragraph($"Основание: {_FirstNonEmpty(agreement?.Description, invoice.AgreementName)}", boldLabel: true),
                _Space(),
                _BuildInvoiceItemsTable(invoice),
                _BuildInvoiceTotalsTable(invoice),
                _Paragraph($"Всего наименований {invoice.InvoiceItems.Count}, на сумму {_Money(_Total(invoice))} руб.", fontSize: 20),
                _Paragraph(_AmountInWords(_Total(invoice)), bold: true, fontSize: 20),
                _Space(),
                _BuildInvoiceSignatureTable(organization),
                _Section());

            mainPart.Document.Save();
        }

        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        return path;
    }

    public static async Task<string> CreateRealizationAsync(RealizationPrintFormRequest request, CancellationToken ct)
    {
        var title = request.Title;
        var invoice = request.Invoice;
        var organization = request.Organization;
        var counterparty = request.Counterparty;
        var agreement = request.Agreement;
        var documentNumber = request.DocumentNumber;
        var outputDir = request.OutputDir;

        Directory.CreateDirectory(outputDir);
        var isUpd = title.Contains("УПД", StringComparison.OrdinalIgnoreCase);
        var filePrefix = isUpd ? "УПД" : "Акт";
        var path = _BuildOutputPath(outputDir, filePrefix, documentNumber ?? invoice.Number);

        if (isUpd)
        {
            using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            _BuildUpdDocument(body, invoice, organization, counterparty, agreement, documentNumber);
            mainPart.Document.Save();
        }
        else
        {
            using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(
                _Paragraph($"Акт № {documentNumber ?? invoice.Number} от {_FormatDate(invoice.DocumentDate)} г.", bold: true, fontSize: 28, align: JustificationValues.Center),
                _Space());
            body.Append(_BuildPartyDetailsBlock("Исполнитель", organization).AsEnumerable());
            body.Append(_BuildPartyDetailsBlock("Заказчик", counterparty).AsEnumerable());
            body.Append(
                _Paragraph($"Основание: {_FirstNonEmpty(agreement?.Description, invoice.AgreementName)}", boldLabel: true),
                _Space(),
                _Paragraph("Исполнитель оказал, а Заказчик принял следующие услуги:", fontSize: 20),
                _BuildActItemsTable(invoice),
                _BuildActTotalsTable(invoice),
                _Paragraph($"Всего оказано услуг {invoice.InvoiceItems.Count}, на сумму {_Money(_Total(invoice))} руб.", fontSize: 20),
                _Paragraph(_AmountInWords(_Total(invoice)), bold: true, fontSize: 20),
                _Space(),
                _Paragraph("Вышеперечисленные услуги выполнены полностью и в срок. Заказчик претензий по объему, качеству и срокам оказания услуг не имеет."),
                _Space(),
                _BuildActSignatureTable(),
                _Section());

            mainPart.Document.Save();
        }

        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        return path;
    }

    private static Table _BuildPaymentDetailsTable(ODataEntity organization, ODataEntity? bankAccount)
    {
        var bankName = _FirstNonEmpty(_GetAnyOptional(bankAccount, "НаименованиеБанка", "Банк.Description", "Банк.Наименование", "Банк.НаименованиеБанка", "Банк", "БанкПолучателя", "Description"), _GetAny(organization, "НаименованиеБанка", "Банк", "БанкПолучателя"));
        var bic = _FirstNonEmpty(_GetAnyOptional(bankAccount, "БИК", "BIC", "БИКБанка", "Банк.БИК", "Банк.БИКБанка"), _GetAny(organization, "БИК", "BIC", "БИКБанка"));
        var correspondentAccount = _FirstNonEmpty(
            _GetAnyOptional(bankAccount, "КоррСчет", "КоррСчетБанка", "КорреспондентскийСчет", "КорреспондентскийСчетБанка", "Банк.КоррСчет", "Банк.КоррСчетБанка"),
            _GetAny(organization, "КоррСчет", "КорреспондентскийСчет", "КорреспондентскийСчетБанка"));
        var paymentAccount = _FirstNonEmpty(
            _GetAnyOptional(bankAccount, "НомерСчета", "РасчетныйСчет", "Счет", "Description"),
            _GetAny(organization, "НомерСчета", "РасчетныйСчет", "Счет"));

        var table = _FixedTable(2600, 2600, 900, 3260);
        table.Append(
            _Row(
                _CellWithRowspanBorder(bankName, width: 5200, colspan: 2, rowspanBottomBorder: false),
                _Cell("БИК", width: 900, bold: true),
                _Cell(bic, width: 3260)),
            _Row(
                _CellWithColspanFont("Банк получателя", width: 5200, colspan: 2, fontSize: 16),
                _Cell("Сч. №", width: 900, bold: true),
                _Cell(correspondentAccount, width: 3260)),
            _Row(
                _Cell($"ИНН {_Get(organization, "ИНН")}", width: 2600, bold: true),
                _Cell($"КПП {_Get(organization, "КПП")}", width: 2600, bold: true),
                _Cell("Сч. №", width: 900, bold: true),
                _Cell(paymentAccount, width: 3260)),
            _Row(
                _CellWithRowspanBorder(organization.Description, width: 5200, colspan: 2, rowspanBottomBorder: false),
                _Cell(string.Empty, width: 900),
                _Cell(string.Empty, width: 3260)),
            _Row(
                _CellWithColspanFont("Получатель", width: 5200, colspan: 2, fontSize: 16),
                _Cell(string.Empty, width: 900),
                _Cell(string.Empty, width: 3260)));
        return table;
    }

    private static Table _BuildInvoiceItemsTable(InvoiceData invoice)
    {
        var table = _FixedTable(520, 4520, 900, 760, 1680, 1680);
        table.AppendChild(_HeaderRow("№", "Товары (работы, услуги)", "Кол-во", "Ед.", "Цена", "Сумма"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            table.AppendChild(_Row(
                _Cell((i + 1).ToString(_ru), 520, align: JustificationValues.Center),
                _Cell(_ItemName(item), 4520),
                _Cell(item.Quantity.ToString("N2", _ru), 900, align: JustificationValues.Right),
                _Cell("шт.", 760, align: JustificationValues.Center),
                _Cell(_Money(item.Price), 1680, align: JustificationValues.Right),
                _Cell(_Money(item.Amount), 1680, align: JustificationValues.Right)));
        }

        return table;
    }

    private static Table _BuildInvoiceTotalsTable(InvoiceData invoice)
    {
        var table = _FixedTable(7000, 2360, bordered: false);
        table.Append(
            _Row(_Cell("Итого:", 7000, bold: true, bordered: false, align: JustificationValues.Right), _Cell(_Money(_Total(invoice)), 2360, bold: true, bordered: false, align: JustificationValues.Right)),
            _Row(_Cell("В том числе НДС:", 7000, bordered: false, align: JustificationValues.Right), _Cell(_VatText(invoice), 2360, bordered: false, align: JustificationValues.Right)),
            _Row(_Cell("Всего к оплате:", 7000, bold: true, bordered: false, align: JustificationValues.Right), _Cell(_Money(_Total(invoice)), 2360, bold: true, bordered: false, align: JustificationValues.Right)));
        return table;
    }

    private static Table _BuildInvoiceSignatureTable(ODataEntity organization)
    {
        var table = _FixedTable(1900, 3100, 1900, 2460, bordered: false);
        table.AppendChild(_Row(
            _Cell("Предприниматель", 1900, bordered: false),
            _Cell(UnderscoreLine, 3100, bordered: false, align: JustificationValues.Center),
            _Cell(organization.Description, 4360, bordered: false, colspan: 2)));
        return table;
    }

    private static Table _BuildActItemsTable(InvoiceData invoice)
    {
        var table = _FixedTable(520, 4520, 900, 760, 1680, 1680);
        table.AppendChild(_HeaderRow("№", "Наименование работ, услуг", "Кол-во", "Ед.", "Цена", "Сумма"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            table.AppendChild(_Row(
                _Cell((i + 1).ToString(_ru), 520, align: JustificationValues.Center),
                _Cell(_ItemName(item), 4520),
                _Cell(item.Quantity.ToString("N2", _ru), 900, align: JustificationValues.Right),
                _Cell("шт.", 760, align: JustificationValues.Center),
                _Cell(_Money(item.Price), 1680, align: JustificationValues.Right),
                _Cell(_Money(item.Amount), 1680, align: JustificationValues.Right)));
        }

        return table;
    }

    private static Table _BuildActTotalsTable(InvoiceData invoice)
    {
        var table = _FixedTable(7000, 2360, bordered: false);
        table.Append(
            _Row(_Cell("Итого:", 7000, bold: true, bordered: false, align: JustificationValues.Right), _Cell(_Money(_Total(invoice)), 2360, bold: true, bordered: false, align: JustificationValues.Right)),
            _Row(_Cell("В том числе НДС:", 7000, bordered: false, align: JustificationValues.Right), _Cell(_VatText(invoice), 2360, bordered: false, align: JustificationValues.Right)));
        return table;
    }

    private static Table _BuildActSignatureTable()
    {
        var table = _FixedTable(4380, 600, 4380, bordered: false);
        table.Append(
            _Row(
                _Cell("ИСПОЛНИТЕЛЬ", 4380, bold: true, bordered: false, align: JustificationValues.Center),
                _Cell(string.Empty, 600, bordered: false),
                _Cell("ЗАКАЗЧИК", 4380, bold: true, bordered: false, align: JustificationValues.Center)),
            _Row(
                _Cell(UnderscoreLine, 4380, bordered: false, align: JustificationValues.Center),
                _Cell(string.Empty, 600, bordered: false),
                _Cell(UnderscoreLine, 4380, bordered: false, align: JustificationValues.Center)));
        return table;
    }

    private static OpenXmlElement[] _BuildPartyDetailsBlock(string label, ODataEntity party)
    {
        var elements = new List<OpenXmlElement>
        {
            _Paragraph($"{label}: {party.Description}", boldLabel: true)
        };

        var innKpp = _InnKpp(party);
        if (!string.IsNullOrWhiteSpace(innKpp))
            elements.Add(_Paragraph($"ИНН/КПП: {innKpp}", boldLabel: true));

        var address = _GetAddress(party);
        if (!string.IsNullOrWhiteSpace(address))
            elements.Add(_Paragraph($"Адрес: {address}", boldLabel: true));

        return [.. elements];
    }

    private static void _BuildUpdDocument(
        Body body,
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber)
    {
        body.Append(
            _BuildUpdTitleTable(invoice, documentNumber),
            _BuildUpdPartiesTable(invoice, organization, counterparty, agreement, documentNumber),
            _BuildUpdItemsTable(invoice),
            _BuildUpdTransferTable(invoice, organization, counterparty, documentNumber),
            _LandscapeSection());
    }

    private static Table _BuildUpdTitleTable(InvoiceData invoice, string? documentNumber)
    {
        var table = _FixedTable(5200, 6800, 3200, bordered: false);
        table.Append(
            _Row(
                _CellWithFont("Универсальный передаточный документ", 5200, fontSize: 24, bold: true, bordered: false),
                _CellWithFont($"Счет-фактура № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy} г. (1)", 6800, fontSize: 20, bold: true, bordered: false),
                _CellWithFont("Приложение № 1 к постановлению Правительства РФ № 1137", 3200, fontSize: 14, bordered: false, align: JustificationValues.Right)),
            _Row(
                _CellWithFont("Статус: 1", 5200, fontSize: 22, bold: true, bordered: false),
                _CellWithFont("Исправление № -- от -- (1а)", 6800, fontSize: 18, bordered: false),
                _CellWithFont("Код вида товара (1б)", 3200, fontSize: 16, bordered: false, align: JustificationValues.Right)));
        return table;
    }

    private static Table _BuildUpdPartiesTable(
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber)
    {
        var table = _FixedTable(2900, 7400, 2600, 2900, bordered: true);
        table.Append(
            _Row(_Cell("Продавец:", 2900, bold: true), _Cell(organization.Description, 7400), _Cell("(2)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Адрес:", 2900, bold: true), _Cell(_GetAddress(organization), 7400), _Cell("(2а)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("ИНН/КПП продавца:", 2900, bold: true), _Cell(_InnKpp(organization), 7400), _Cell("(2б)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Грузоотправитель и его адрес:", 2900, bold: true), _Cell("он же", 7400), _Cell("(3)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Грузополучатель и его адрес:", 2900, bold: true), _Cell(counterparty.Description, 7400), _Cell("(4)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("К платежно-расчетному документу:", 2900, bold: true), _Cell("-", 7400), _Cell("(5)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Документ об отгрузке:", 2900, bold: true), _Cell($"УПД № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}", 7400), _Cell("(5а)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("К счету-фактуре, на основании которого составлен документ об отгрузке:", 2900, bold: true), _Cell("-", 7400), _Cell("(5б)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Покупатель:", 2900, bold: true), _Cell(counterparty.Description, 7400), _Cell("(6)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Адрес:", 2900, bold: true), _Cell(_GetAddress(counterparty), 7400), _Cell("(6а)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("ИНН/КПП покупателя:", 2900, bold: true), _Cell(_InnKpp(counterparty), 7400), _Cell("(6б)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)),
            _Row(_Cell("Валюта:", 2900, bold: true), _Cell("Российский рубль, 643", 7400), _Cell("(7)", 2600, align: JustificationValues.Center), _Cell(_FirstNonEmpty(agreement?.Description, invoice.AgreementName), 2900)),
            _Row(_Cell("Идентификатор государственного контракта, договора (соглашения):", 2900, bold: true), _Cell("-", 7400), _Cell("(8)", 2600, align: JustificationValues.Center), _Cell(string.Empty, 2900)));
        return table;
    }

    private static Table _BuildUpdItemsTable(InvoiceData invoice)
    {
        int[] widths = [380, 2450, 620, 620, 620, 560, 560, 760, 980, 1080, 860, 900, 1000, 1100, 680, 680];
        var table = _FixedTable(widths);
        table.Append(
            _HeaderRowWithWidths(widths, "№ п/п", "Наименование товара (описание выполненных работ, оказанных услуг)", "Код товара/работ", "Код вида товара", "Артикул", "Код", "Ед.", "Кол-во", "Цена", "Стоимость без НДС", "Акциз", "Ставка НДС", "Сумма НДС", "Стоимость с НДС", "Страна", "ГТД"),
            _HeaderRowWithWidths(widths, "А", "1", "1а", "1б", "1в", "2", "2а", "3", "4", "5", "6", "7", "8", "9", "10а", "11"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            var vat = _CalculateIncludedVat(item.Amount, item.VatRate);
            table.AppendChild(_Row(
                _Cell((i + 1).ToString(_ru), widths[0], align: JustificationValues.Center),
                _Cell(_ItemName(item), widths[1]),
                _Cell(string.Empty, widths[2]),
                _Cell(string.Empty, widths[3]),
                _Cell(string.Empty, widths[4]),
                _Cell("796", widths[5], align: JustificationValues.Center),
                _Cell("шт", widths[6], align: JustificationValues.Center),
                _Cell(item.Quantity.ToString("N2", _ru), widths[7], align: JustificationValues.Right),
                _Cell(_Money(item.Price), widths[8], align: JustificationValues.Right),
                _Cell(_Money(item.Amount - vat), widths[9], align: JustificationValues.Right),
                _Cell("без акциза", widths[10], align: JustificationValues.Center),
                _Cell(_VatRateText(item.VatRate), widths[11], align: JustificationValues.Center),
                _Cell(_Money(vat), widths[12], align: JustificationValues.Right),
                _Cell(_Money(item.Amount), widths[13], align: JustificationValues.Right),
                _Cell(string.Empty, widths[14]),
                _Cell(string.Empty, widths[15])));
        }

        table.AppendChild(_Row(
            _Cell(string.Empty, widths[0]),
            _Cell("Всего к оплате", widths[1] + widths[2] + widths[3] + widths[4] + widths[5] + widths[6] + widths[7] + widths[8], bold: true, colspan: 8),
            _Cell(_Money(_Total(invoice) - _TotalVat(invoice)), widths[9], bold: true, align: JustificationValues.Right),
            _Cell(string.Empty, widths[10]),
            _Cell(string.Empty, widths[11]),
            _Cell(_Money(_TotalVat(invoice)), widths[12], bold: true, align: JustificationValues.Right),
            _Cell(_Money(_Total(invoice)), widths[13], bold: true, align: JustificationValues.Right),
            _Cell(string.Empty, widths[14]),
            _Cell(string.Empty, widths[15])));
        return table;
    }

    private static Table _BuildUpdTransferTable(
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        string? documentNumber)
    {
        var table = _FixedTable(3700, 4100, 3700, 4100, bordered: true);
        table.Append(
            _Row(_Cell("Документ составлен на", 3700, bold: true), _Cell("1 листе", 4100), _Cell("Всего передано", 3700, bold: true), _Cell($"на сумму {_Money(_Total(invoice))} руб.", 4100)),
            _Row(_Cell("Основание передачи", 3700, bold: true), _Cell($"УПД № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}", 4100), _Cell("Дата отгрузки", 3700, bold: true), _Cell(invoice.DocumentDate.ToString("dd.MM.yyyy", _ru), 4100)),
            _Row(_Cell("Ответственный за правильность оформления факта хозяйственной жизни", 7800, bold: true, colspan: 2), _Cell("Ответственный за приемку", 7800, bold: true, colspan: 2)),
            _Row(_Cell("__________________ / " + organization.Description, 7800, colspan: 2), _Cell("__________________ / " + counterparty.Description, 7800, colspan: 2)),
            _Row(_Cell("Руководитель организации-продавца", 3700, bold: true), _Cell(UnderscoreLine, 4100), _Cell("Получил груз/услуги", 3700, bold: true), _Cell(UnderscoreLine, 4100)),
            _Row(_Cell("Главный бухгалтер", 3700, bold: true), _Cell(UnderscoreLine, 4100), _Cell("Дата получения", 3700, bold: true), _Cell(invoice.DocumentDate.ToString("dd.MM.yyyy", _ru), 4100)),
            _Row(_Cell("Отпуск груза произвел / услуги передал", 3700, bold: true), _Cell(UnderscoreLine, 4100), _Cell("Груз получил / услуги принял", 3700, bold: true), _Cell(UnderscoreLine, 4100)),
            _Row(_Cell("Основание полномочий представителя продавца", 3700, bold: true), _Cell("-", 4100), _Cell("Основание полномочий представителя покупателя", 3700, bold: true), _Cell("-", 4100)));
        return table;
    }

    private static void _FillUpd(
        WordprocessingDocument document,
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber)
    {
        var tables = document.MainDocumentPart?.Document.Body?.Descendants<Table>().ToList() ?? [];
        var titleTable = tables.ElementAtOrDefault(0);
        if (titleTable is not null)
        {
            _SetLabelValue(titleTable, "Счет-фактура №", documentNumber ?? invoice.Number);
            _SetCellText(titleTable, 0, 5, $"от {invoice.DocumentDate:dd.MM.yyyy} г.");
        }

        var partyTable = tables.ElementAtOrDefault(1);
        if (partyTable is not null)
        {
            _SetLabelValue(partyTable, "Продавец:", organization.Description);
            _SetLabelValue(partyTable, "Покупатель:", counterparty.Description);
            _SetLabelValue(partyTable, "Адрес:", _FirstNonEmpty(_GetAny(organization, "Адрес", "ЮридическийАдрес"), string.Empty));
            _SetCellText(partyTable, 1, 8, _FirstNonEmpty(_GetAny(counterparty, "Адрес", "ЮридическийАдрес"), string.Empty));
            _SetLabelValue(partyTable, "ИНН/КПП продавца:", _InnKpp(organization));
            _SetCellText(partyTable, 2, 8, _InnKpp(counterparty));
            _SetLabelValue(partyTable, "Идентификатор государственного контракта", agreement?.Description ?? invoice.AgreementName);
            _SetRowTextContaining(partyTable, "Документ об отгрузке", $"Документ об отгрузке: Универсальный передаточный документ, № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}");
        }

        _FillUpdItems(tables.ElementAtOrDefault(2), invoice);
    }

    private static void _FillUpdItems(Table? table, InvoiceData invoice)
    {
        if (table is null)
            return;

        var rows = table.Elements<TableRow>().ToList();
        var columnsIndex = _FindRowIndexContaining(table, "10а");
        var totalIndex = _FindRowIndexContaining(table, "Всего к оплате");
        if (columnsIndex < 0 || totalIndex < 0 || totalIndex <= columnsIndex)
            return;

        var template = rows.ElementAtOrDefault(columnsIndex + 1)?.CloneNode(true) as TableRow ?? _CreateRow(16);
        var totalRow = rows[totalIndex];
        foreach (var row in rows.Skip(columnsIndex + 1).Take(totalIndex - columnsIndex - 1).ToList())
            row.Remove();

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            var row = (TableRow)template.CloneNode(true);
            var vat = _CalculateIncludedVat(item.Amount, item.VatRate);
            _SetRowTexts(row,
                string.Empty,
                (i + 1).ToString(_ru),
                _ItemName(item),
                string.Empty,
                "796",
                "шт",
                item.Quantity.ToString("N2", _ru),
                _Money(item.Price),
                _Money(item.Amount - vat),
                "без акциза",
                _VatRateText(item.VatRate),
                _Money(vat),
                _Money(item.Amount),
                string.Empty,
                string.Empty,
                string.Empty);
            table.InsertBefore(row, totalRow);
        }

        _SetRowTexts(totalRow,
            string.Empty,
            "Всего к оплате",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            _Money(_Total(invoice) - _TotalVat(invoice)),
            string.Empty,
            string.Empty,
            _Money(_TotalVat(invoice)),
            _Money(_Total(invoice)),
            string.Empty,
            string.Empty,
            string.Empty);
    }
    private static TableProperties _FixedTableProperties(bool bordered)
        => new(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, new TableLayout { Type = TableLayoutValues.Fixed }, bordered ? _Borders() : _NoBorders());

    private static Table _FixedTable(params int[] widths) => _FixedTable(widths, bordered: true);

    private static Table _FixedTable(int[] widths, bool bordered)
    {
        var table = new Table();
        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed },
            bordered ? _Borders() : _NoBorders()));
        table.AppendChild(new TableGrid([.. widths.Select(width => new GridColumn { Width = width.ToString(_ru) })]));
        return table;
    }

    private static Table _FixedTable(int width1, int width2, bool bordered)
    {
        var table = new Table();
        table.AppendChild(_FixedTableProperties(bordered));
        table.AppendChild(new TableGrid(new GridColumn { Width = width1.ToString(_ru) }, new GridColumn { Width = width2.ToString(_ru) }));
        return table;
    }

    private static Table _FixedTable(int width1, int width2, int width3, bool bordered = true)
    {
        var table = new Table();
        table.AppendChild(_FixedTableProperties(bordered));
        table.AppendChild(new TableGrid(
            new GridColumn { Width = width1.ToString(_ru) },
            new GridColumn { Width = width2.ToString(_ru) },
            new GridColumn { Width = width3.ToString(_ru) }));
        return table;
    }

    private static Table _FixedTable(int width1, int width2, int width3, int width4, bool bordered = true)
    {
        var table = new Table();
        table.AppendChild(_FixedTableProperties(bordered));
        table.AppendChild(new TableGrid(
            new GridColumn { Width = width1.ToString(_ru) },
            new GridColumn { Width = width2.ToString(_ru) },
            new GridColumn { Width = width3.ToString(_ru) },
            new GridColumn { Width = width4.ToString(_ru) }));
        return table;
    }

    private static Table _FixedTable(int width1, int width2, int width3, int width4, int width5, int width6)
    {
        var table = new Table();
        table.AppendChild(_FixedTableProperties(bordered: true));
        table.AppendChild(new TableGrid(
            new GridColumn { Width = width1.ToString(_ru) },
            new GridColumn { Width = width2.ToString(_ru) },
            new GridColumn { Width = width3.ToString(_ru) },
            new GridColumn { Width = width4.ToString(_ru) },
            new GridColumn { Width = width5.ToString(_ru) },
            new GridColumn { Width = width6.ToString(_ru) }));
        return table;
    }

    private static TableBorders _Borders()
        => new(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });

    private static TableBorders _NoBorders()
        => new(
            new TopBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.None },
            new LeftBorder { Val = BorderValues.None },
            new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None },
            new InsideVerticalBorder { Val = BorderValues.None });

    private static TableRow _HeaderRow(params string[] values)
        => _Row([.. values.Select((value, index) => _Cell(value, _HeaderWidths(values.Length)[index], bold: true, shaded: true, align: JustificationValues.Center))]);

    private static TableRow _HeaderRowWithWidths(int[] widths, params string[] values)
        => _Row([.. values.Select((value, index) => _HeaderCell(value, widths[index], fontSize: 14))]);

    private static int[] _HeaderWidths(int count)
        => count == 6 ? [520, 4520, 900, 760, 1680, 1680] : [.. Enumerable.Repeat(PageWidth / count, count)];

    private static TableRow _Row(params TableCell[] cells) => new(cells);

    private static TableCell _Cell(
        string? text,
        int width,
        bool bold = false,
        bool shaded = false,
        bool bordered = true,
        int colspan = 1,
        JustificationValues? align = null)
        => _CreateCell(new CellOptions
        {
            Text = text,
            Width = width,
            Bold = bold,
            Shaded = shaded,
            Bordered = bordered,
            Colspan = colspan,
            Align = align
        });

    private static TableCell _CellWithFont(
        string? text,
        int width,
        int fontSize,
        bool bold = false,
        bool bordered = true,
        JustificationValues? align = null)
        => _CreateCell(new CellOptions
        {
            Text = text,
            Width = width,
            Bold = bold,
            Bordered = bordered,
            Align = align,
            FontSize = fontSize
        });

    private static TableCell _CellWithColspanFont(string? text, int width, int colspan, int fontSize)
        => _CreateCell(new CellOptions
        {
            Text = text,
            Width = width,
            Colspan = colspan,
            FontSize = fontSize
        });

    private static TableCell _CellWithRowspanBorder(string? text, int width, int colspan, bool rowspanBottomBorder)
        => _CreateCell(new CellOptions
        {
            Text = text,
            Width = width,
            Colspan = colspan,
            RowspanBottomBorder = rowspanBottomBorder
        });

    private static TableCell _HeaderCell(string? text, int width, int fontSize = 18)
        => _CreateCell(new CellOptions
        {
            Text = text,
            Width = width,
            Bold = true,
            Shaded = true,
            Align = JustificationValues.Center,
            FontSize = fontSize
        });

    private static TableCell _CreateCell(CellOptions options)
    {
        var properties = new TableCellProperties(
            new TableCellWidth { Width = options.Width.ToString(_ru), Type = TableWidthUnitValues.Dxa },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellMargin(
                new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa }));
        if (options.Colspan > 1)
            properties.AppendChild(new GridSpan { Val = options.Colspan });
        if (options.Shaded)
            properties.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "EDEDED" });
        if (!options.Bordered || !options.RowspanBottomBorder)
            properties.AppendChild(new TableCellBorders(new BottomBorder { Val = options.RowspanBottomBorder && options.Bordered ? BorderValues.Single : BorderValues.None }));

        return new TableCell(properties, _Paragraph(options.Text ?? string.Empty, options.Bold, options.FontSize, options.Align));
    }

    private sealed class CellOptions
    {
        public string? Text { get; init; }
        public int Width { get; init; }
        public bool Bold { get; init; }
        public bool Shaded { get; init; }
        public bool Bordered { get; init; } = true;
        public int Colspan { get; init; } = 1;
        public JustificationValues? Align { get; init; }
        public int FontSize { get; init; } = 18;
        public bool RowspanBottomBorder { get; init; } = true;
    }

    private static Paragraph _Paragraph(
        string text,
        bool bold = false,
        int fontSize = 20,
        JustificationValues? align = null,
        bool boldLabel = false)
    {
        var paragraph = new Paragraph();
        var properties = new ParagraphProperties(new SpacingBetweenLines { After = "80" });
        if (align is not null)
            properties.AppendChild(new Justification { Val = align });
        paragraph.AppendChild(properties);

        if (boldLabel && text.Contains(':'))
        {
            var index = text.IndexOf(':');
            paragraph.AppendChild(_Run(text[..(index + 1)], bold: true, fontSize));
            paragraph.AppendChild(_Run(text[(index + 1)..], bold: false, fontSize));
            return paragraph;
        }

        paragraph.AppendChild(_Run(text, bold, fontSize));
        return paragraph;
    }

    private static Run _Run(string text, bool bold, int fontSize)
    {
        var properties = new RunProperties(new FontSize { Val = fontSize.ToString(_ru) }, new FontSizeComplexScript { Val = fontSize.ToString(_ru) });
        if (bold)
            properties.AppendChild(new Bold());
        return new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph _Separator()
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 8 })));
        paragraph.AppendChild(new Run(new Text(string.Empty)));
        return paragraph;
    }

    private static Paragraph _Space() => _Paragraph(string.Empty, fontSize: 8);

    private static SectionProperties _Section()
        => new(
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin { Top = 850, Right = 850, Bottom = 850, Left = 850, Header = 0, Footer = 0, Gutter = 0 });

    private static SectionProperties _LandscapeSection()
        => new(
            new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
            new PageMargin { Top = 560, Right = 560, Bottom = 560, Left = 560, Header = 0, Footer = 0, Gutter = 0 });

    //////private static void _CopyTemplate(string templateFileName, string destination)
    //////{
    //////    var templatePath = _ResolveTemplate(templateFileName);
    //////    File.Copy(templatePath, destination, overwrite: true);
    //////}

    ////private static string _ResolveTemplate(string templateFileName)
    ////{
    ////    var candidates = new[]
    ////    {
    ////        Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName),
    ////        Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName),
    ////        Path.Combine(PathResolver.ResolveFromProjectRoot("Templates"), templateFileName)
    ////    };

    ////    var path = candidates.FirstOrDefault(File.Exists);
    ////    return path as string ?? throw new FileNotFoundException($"Не найден шаблон печатной формы '{templateFileName}'.", templateFileName);
    ////}

    private static string _BuildOutputPath(string outputDir, string filePrefix, string number)
    {
        var safeNumber = _SafeFileName(string.IsNullOrWhiteSpace(number) ? "без_номера" : number);
        return Path.Combine(outputDir, $"{filePrefix}_{safeNumber}_{DateTime.Now:yyyyMMddHHmmssfff}.docx");
    }

    private static void _SetLabelValue(Table table, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            for (var i = 0; i < cells.Count; i++)
            {
                if (!_CellText(cells[i]).Contains(label, StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = cells.Skip(i + 1).FirstOrDefault(cell => !_CellText(cell).Trim().StartsWith('('));
                if (target is not null)
                {
                    _SetCellText(target, value);
                    return;
                }

                _SetCellText(cells[i], $"{label} {value}");
                return;
            }
        }
    }

    private static void _SetRowTextContaining(Table table, string contains, string value)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            var cell = cells.FirstOrDefault(candidate => _CellText(candidate).Contains(contains, StringComparison.OrdinalIgnoreCase));
            if (cell is not null)
            {
                _SetCellText(cell, value);
                return;
            }
        }
    }

    private static void _SetCellText(Table table, int rowIndex, int cellIndex, string? value)
    {
        var cell = table.Elements<TableRow>().ElementAtOrDefault(rowIndex)?.Elements<TableCell>().ElementAtOrDefault(cellIndex);
        if (cell is not null)
            _SetCellText(cell, value ?? string.Empty);
    }

    private static void _SetCellText(TableCell cell, string value)
    {
        cell.RemoveAllChildren<Paragraph>();
        cell.AppendChild(_Paragraph(value, fontSize: 18));
    }

    private static void _SetRowTexts(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        while (cells.Count < values.Length)
        {
            var cell = new TableCell();
            row.AppendChild(cell);
            cells.Add(cell);
        }

        for (var i = 0; i < values.Length; i++)
            _SetCellText(cells[i], values[i]);
    }

    private static TableRow _CreateRow(int cellCount)
    {
        var row = new TableRow();
        for (var i = 0; i < cellCount; i++)
            row.AppendChild(new TableCell(new Paragraph()));
        return row;
    }

    private static int _FindRowIndexContaining(Table table, string text)
    {
        var rows = table.Elements<TableRow>().ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            if (_RowText(rows[i]).Contains(text, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string _RowText(TableRow row) => string.Join(" ", row.Elements<TableCell>().Select(_CellText));

    private static string _CellText(TableCell cell) => cell.InnerText ?? string.Empty;

    private static string _FormatParty(ODataEntity entity)
    {
        var inn = _Get(entity, "ИНН");
        var kpp = _Get(entity, "КПП");
        var parts = new List<string> { entity.Description };
        if (!string.IsNullOrWhiteSpace(inn))
            parts.Add($"ИНН {inn}");
        if (!string.IsNullOrWhiteSpace(kpp))
            parts.Add($"КПП {kpp}");
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string _InnKpp(ODataEntity entity)
    {
        var inn = _Get(entity, "ИНН");
        var kpp = _Get(entity, "КПП");
        return string.IsNullOrWhiteSpace(kpp) ? inn : $"{inn}/{kpp}";
    }

    private static string _Get(ODataEntity entity, string field)
    {
        if (field.Contains('.', StringComparison.Ordinal))
            return _GetNested(entity.Raw, field.Split('.', StringSplitOptions.RemoveEmptyEntries));

        return entity.Raw.TryGetValue(field, out var value) ? _FormatRawValue(value) : string.Empty;
    }

    private static string _GetNested(Dictionary<string, object?> raw, IReadOnlyList<string> path)
    {
        object? current = raw;
        foreach (var part in path)
        {
            if (current is Dictionary<string, object?> dictionary)
            {
                if (!dictionary.TryGetValue(part, out current))
                    return string.Empty;
                continue;
            }

            return string.Empty;
        }

        return _FormatRawValue(current);
    }

    private static string _FormatRawValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is Dictionary<string, object?> dictionary)
        {
            foreach (var field in new[] { "Description", "Наименование", "НаименованиеБанка", "Представление" })
            {
                if (dictionary.TryGetValue(field, out var nestedValue))
                {
                    var text = Convert.ToString(nestedValue, _ru);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            return string.Empty;
        }

        return Convert.ToString(value, _ru) ?? string.Empty;
    }

    private static string _GetAny(ODataEntity entity, params string[] fields)
    {
        foreach (var field in fields)
        {
            var value = _Get(entity, field);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string _GetAnyOptional(ODataEntity? entity, params string[] fields)
    {
        if (entity is null)
            return string.Empty;

        return _GetAny(entity, fields);
    }

    private static string _GetAddress(ODataEntity entity)
        => _GetAny(entity, "Адрес", "ЮридическийАдрес", "АдресСтрокой", "ПредставлениеАдреса", "ФактическийАдрес");

    private static decimal _Total(InvoiceData invoice) => invoice.InvoiceItems.Sum(item => item.Amount);

    private static decimal _TotalVat(InvoiceData invoice) => invoice.InvoiceItems.Sum(item => _CalculateIncludedVat(item.Amount, item.VatRate));

    private static string _VatText(InvoiceData invoice)
    {
        var vat = _TotalVat(invoice);
        return vat <= 0 ? "Без налога (НДС)" : _Money(vat);
    }

    private static string _ItemName(InvoiceItemData item) => _FirstNonEmpty(item.NomenclatureDescription, item.NomenclatureName);

    private static decimal _CalculateIncludedVat(decimal amount, string vatRate)
    {
        var percent = _ExtractVatPercent(vatRate);
        if (percent <= 0)
            return 0m;

        return Math.Round(amount * percent / (100m + percent), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal _ExtractVatPercent(string vatRate)
    {
        if (string.IsNullOrWhiteSpace(vatRate) || vatRate.Contains("без", StringComparison.OrdinalIgnoreCase))
            return 0m;

        var match = MyRegexDigit().Match(vatRate);
        return match.Success ? decimal.Parse(match.Value, CultureInfo.InvariantCulture) : 0m;
    }

    private static string _VatRateText(string vatRate)
    {
        if (string.IsNullOrWhiteSpace(vatRate) || vatRate.Contains("без", StringComparison.OrdinalIgnoreCase))
            return "без НДС";

        var percent = _ExtractVatPercent(vatRate);
        return percent <= 0 ? vatRate : $"{percent:0}%";
    }

    private static string _Money(decimal value) => value.ToString("N2", _ru);

    private static string _FormatDate(DateTime date) => date.ToString("dd MMMM yyyy", _ru);

    private static string _FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string _SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string([.. value.Select(ch => invalid.Contains(ch) ? '_' : ch)]);
        return string.IsNullOrWhiteSpace(cleaned) ? "без_номера" : cleaned;
    }

    private static string _AmountInWords(decimal amount)
    {
        var rubles = (long)Math.Floor(amount);
        var kopecks = (int)Math.Round((amount - rubles) * 100m, 0, MidpointRounding.AwayFromZero);
        if (kopecks == 100)
        {
            rubles++;
            kopecks = 0;
        }

        var words = rubles == 0 ? "ноль" : _NumberToWords(rubles);
        return $"{_Capitalize(words)} {_ChooseForm(rubles, "рубль", "рубля", "рублей")} {kopecks:00} {_ChooseForm(kopecks, "копейка", "копейки", "копеек")}";
    }

    private static string _NumberToWords(long value)
    {
        var groups = new[]
        {
            (Value: value / 1_000_000_000, Forms: ["миллиард", "миллиарда", "миллиардов"], Feminine: false),
            (Value: value / 1_000_000 % 1000, Forms: ["миллион", "миллиона", "миллионов"], Feminine: false),
            (Value: value / 1_000 % 1000, Forms: ["тысяча", "тысячи", "тысяч"], Feminine: true),
            (Value: value % 1000, Forms: Array.Empty<string>(), Feminine: false)
        };

        var parts = new List<string>();
        foreach (var (Value, Forms, Feminine) in groups)
        {
            if (Value == 0)
                continue;

            parts.Add(_HundredsToWords((int)Value, Feminine));
            if (Forms.Length > 0)
                parts.Add(_ChooseForm(Value, Forms[0], Forms[1], Forms[2]));
        }

        return string.Join(" ", parts);
    }

    private static string _HundredsToWords(int value, bool feminine)
    {
        string[] hundreds = ["", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот"];
        string[] tens = ["", "десять", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто"];
        string[] teens = ["десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать"];
        string[] ones = feminine
            ? ["", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"]
            : ["", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять"];

        var parts = new List<string>();
        if (value / 100 > 0)
            parts.Add(hundreds[value / 100]);

        var rest = value % 100;
        if (rest >= 10 && rest <= 19)
        {
            parts.Add(teens[rest - 10]);
        }
        else
        {
            if (rest / 10 > 0)
                parts.Add(tens[rest / 10]);
            if (rest % 10 > 0)
                parts.Add(ones[rest % 10]);
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string _ChooseForm(long value, string one, string two, string five)
    {
        var lastTwo = Math.Abs(value) % 100;
        if (lastTwo is >= 11 and <= 19)
            return five;

        return (Math.Abs(value) % 10) switch
        {
            1 => one,
            >= 2 and <= 4 => two,
            _ => five
        };
    }

    private static string _Capitalize(string value)
        => string.IsNullOrWhiteSpace(value) ? value : char.ToUpper(value[0], _ru) + value[1..];
    [GeneratedRegex(@"\d+")]
    private static partial Regex MyRegexDigit();
}

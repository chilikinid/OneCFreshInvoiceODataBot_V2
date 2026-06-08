using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

using OneCFreshInvoiceODataBot.Models;

using System.Globalization;
using System.Text.RegularExpressions;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class DocxPrintFormGenerator
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private const int PageWidth = 9360;

    public async Task<string> CreateInvoiceAsync(
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        ODataEntity? bankAccount,
        string? documentNumber,
        string outputDir,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var path = BuildOutputPath(outputDir, "Счет", documentNumber ?? invoice.Number);

        using (var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(
                BuildPaymentDetailsTable(organization, bankAccount),
                Paragraph($"Счет на оплату № {documentNumber ?? invoice.Number} от {FormatDate(invoice.DocumentDate)} г.", bold: true, fontSize: 28),
                Separator(),
                Paragraph($"Поставщик (Исполнитель): {FormatParty(organization)}", boldLabel: true),
                Paragraph($"Покупатель (Заказчик): {FormatParty(counterparty)}", boldLabel: true),
                Paragraph($"Основание: {FirstNonEmpty(agreement?.Description, invoice.AgreementName)}", boldLabel: true),
                Space(),
                BuildInvoiceItemsTable(invoice),
                BuildInvoiceTotalsTable(invoice),
                Paragraph($"Всего наименований {invoice.InvoiceItems.Count}, на сумму {Money(Total(invoice))} руб.", fontSize: 20),
                Paragraph(AmountInWords(Total(invoice)), bold: true, fontSize: 20),
                Space(),
                BuildInvoiceSignatureTable(organization),
                Section());

            mainPart.Document.Save();
        }

        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        return path;
    }

    public async Task<string> CreateRealizationAsync(
        string title,
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber,
        string outputDir,
        CancellationToken ct)
    {
        Directory.CreateDirectory(outputDir);
        var isUpd = title.Contains("УПД", StringComparison.OrdinalIgnoreCase);
        var filePrefix = isUpd ? "УПД" : "Акт";
        var path = BuildOutputPath(outputDir, filePrefix, documentNumber ?? invoice.Number);

        if (isUpd)
        {
            using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;
            BuildUpdDocument(body, invoice, organization, counterparty, agreement, documentNumber);
            mainPart.Document.Save();
        }
        else
        {
            using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
            var mainPart = document.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body!;

            body.Append(
                Paragraph($"Акт № {documentNumber ?? invoice.Number} от {FormatDate(invoice.DocumentDate)} г.", bold: true, fontSize: 28, align: JustificationValues.Center),
                Space());
            body.Append(BuildPartyDetailsBlock("Исполнитель", organization));
            body.Append(BuildPartyDetailsBlock("Заказчик", counterparty));
            body.Append(
                Paragraph($"Основание: {FirstNonEmpty(agreement?.Description, invoice.AgreementName)}", boldLabel: true),
                Space(),
                Paragraph($"Исполнитель оказал, а Заказчик принял следующие услуги:", fontSize: 20),
                BuildActItemsTable(invoice),
                BuildActTotalsTable(invoice),
                Paragraph($"Всего оказано услуг {invoice.InvoiceItems.Count}, на сумму {Money(Total(invoice))} руб.", fontSize: 20),
                Paragraph(AmountInWords(Total(invoice)), bold: true, fontSize: 20),
                Space(),
                Paragraph("Вышеперечисленные услуги выполнены полностью и в срок. Заказчик претензий по объему, качеству и срокам оказания услуг не имеет."),
                Space(),
                BuildActSignatureTable(),
                Section());

            mainPart.Document.Save();
        }

        await Task.CompletedTask;
        ct.ThrowIfCancellationRequested();
        return path;
    }

    private static Table BuildPaymentDetailsTable(ODataEntity organization, ODataEntity? bankAccount)
    {
        var bankName = FirstNonEmpty(GetAnyOptional(bankAccount, "НаименованиеБанка", "Банк.Description", "Банк.Наименование", "Банк.НаименованиеБанка", "Банк", "БанкПолучателя", "Description"), GetAny(organization, "НаименованиеБанка", "Банк", "БанкПолучателя"));
        var bic = FirstNonEmpty(GetAnyOptional(bankAccount, "БИК", "BIC", "БИКБанка", "Банк.БИК", "Банк.БИКБанка"), GetAny(organization, "БИК", "BIC", "БИКБанка"));
        var correspondentAccount = FirstNonEmpty(
            GetAnyOptional(bankAccount, "КоррСчет", "КоррСчетБанка", "КорреспондентскийСчет", "КорреспондентскийСчетБанка", "Банк.КоррСчет", "Банк.КоррСчетБанка"),
            GetAny(organization, "КоррСчет", "КорреспондентскийСчет", "КорреспондентскийСчетБанка"));
        var paymentAccount = FirstNonEmpty(
            GetAnyOptional(bankAccount, "НомерСчета", "РасчетныйСчет", "Счет", "Description"),
            GetAny(organization, "НомерСчета", "РасчетныйСчет", "Счет"));

        var table = FixedTable(2600, 2600, 900, 3260);
        table.Append(
            Row(
                Cell(bankName, width: 5200, colspan: 2, rowspanBottomBorder: false),
                Cell("БИК", width: 900, bold: true),
                Cell(bic, width: 3260)),
            Row(
                Cell("Банк получателя", width: 5200, colspan: 2, fontSize: 16),
                Cell("Сч. №", width: 900, bold: true),
                Cell(correspondentAccount, width: 3260)),
            Row(
                Cell($"ИНН {Get(organization, "ИНН")}", width: 2600, bold: true),
                Cell($"КПП {Get(organization, "КПП")}", width: 2600, bold: true),
                Cell("Сч. №", width: 900, bold: true),
                Cell(paymentAccount, width: 3260)),
            Row(
                Cell(organization.Description, width: 5200, colspan: 2, rowspanBottomBorder: false),
                Cell(string.Empty, width: 900),
                Cell(string.Empty, width: 3260)),
            Row(
                Cell("Получатель", width: 5200, colspan: 2, fontSize: 16),
                Cell(string.Empty, width: 900),
                Cell(string.Empty, width: 3260)));
        return table;
    }

    private static Table BuildInvoiceItemsTable(InvoiceData invoice)
    {
        var table = FixedTable(520, 4520, 900, 760, 1680, 1680);
        table.Append(HeaderRow("№", "Товары (работы, услуги)", "Кол-во", "Ед.", "Цена", "Сумма"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            table.Append(Row(
                Cell((i + 1).ToString(Ru), 520, align: JustificationValues.Center),
                Cell(ItemName(item), 4520),
                Cell(item.Quantity.ToString("N2", Ru), 900, align: JustificationValues.Right),
                Cell("шт.", 760, align: JustificationValues.Center),
                Cell(Money(item.Price), 1680, align: JustificationValues.Right),
                Cell(Money(item.Amount), 1680, align: JustificationValues.Right)));
        }

        return table;
    }

    private static Table BuildInvoiceTotalsTable(InvoiceData invoice)
    {
        var table = FixedTable(7000, 2360, bordered: false);
        table.Append(
            Row(Cell("Итого:", 7000, bold: true, align: JustificationValues.Right, bordered: false), Cell(Money(Total(invoice)), 2360, bold: true, align: JustificationValues.Right, bordered: false)),
            Row(Cell("В том числе НДС:", 7000, align: JustificationValues.Right, bordered: false), Cell(VatText(invoice), 2360, align: JustificationValues.Right, bordered: false)),
            Row(Cell("Всего к оплате:", 7000, bold: true, align: JustificationValues.Right, bordered: false), Cell(Money(Total(invoice)), 2360, bold: true, align: JustificationValues.Right, bordered: false)));
        return table;
    }

    private static Table BuildInvoiceSignatureTable(ODataEntity organization)
    {
        var table = FixedTable(1900, 3100, 1900, 2460, bordered: false);
        table.Append(Row(
            Cell("Предприниматель", 1900, bordered: false),
            Cell("__________________", 3100, align: JustificationValues.Center, bordered: false),
            Cell(organization.Description, 4360, colspan: 2, bordered: false)));
        return table;
    }

    private static Table BuildActItemsTable(InvoiceData invoice)
    {
        var table = FixedTable(520, 4520, 900, 760, 1680, 1680);
        table.Append(HeaderRow("№", "Наименование работ, услуг", "Кол-во", "Ед.", "Цена", "Сумма"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            table.Append(Row(
                Cell((i + 1).ToString(Ru), 520, align: JustificationValues.Center),
                Cell(ItemName(item), 4520),
                Cell(item.Quantity.ToString("N2", Ru), 900, align: JustificationValues.Right),
                Cell("шт.", 760, align: JustificationValues.Center),
                Cell(Money(item.Price), 1680, align: JustificationValues.Right),
                Cell(Money(item.Amount), 1680, align: JustificationValues.Right)));
        }

        return table;
    }

    private static Table BuildActTotalsTable(InvoiceData invoice)
    {
        var table = FixedTable(7000, 2360, bordered: false);
        table.Append(
            Row(Cell("Итого:", 7000, bold: true, align: JustificationValues.Right, bordered: false), Cell(Money(Total(invoice)), 2360, bold: true, align: JustificationValues.Right, bordered: false)),
            Row(Cell("В том числе НДС:", 7000, align: JustificationValues.Right, bordered: false), Cell(VatText(invoice), 2360, align: JustificationValues.Right, bordered: false)));
        return table;
    }

    private static Table BuildActSignatureTable()
    {
        var table = FixedTable(4380, 600, 4380, bordered: false);
        table.Append(
            Row(
                Cell("ИСПОЛНИТЕЛЬ", 4380, bold: true, align: JustificationValues.Center, bordered: false),
                Cell(string.Empty, 600, bordered: false),
                Cell("ЗАКАЗЧИК", 4380, bold: true, align: JustificationValues.Center, bordered: false)),
            Row(
                Cell("__________________", 4380, align: JustificationValues.Center, bordered: false),
                Cell(string.Empty, 600, bordered: false),
                Cell("__________________", 4380, align: JustificationValues.Center, bordered: false)));
        return table;
    }

    private static OpenXmlElement[] BuildPartyDetailsBlock(string label, ODataEntity party)
    {
        var elements = new List<OpenXmlElement>
        {
            Paragraph($"{label}: {party.Description}", boldLabel: true)
        };

        var innKpp = InnKpp(party);
        if (!string.IsNullOrWhiteSpace(innKpp))
            elements.Add(Paragraph($"ИНН/КПП: {innKpp}", boldLabel: true));

        var address = GetAddress(party);
        if (!string.IsNullOrWhiteSpace(address))
            elements.Add(Paragraph($"Адрес: {address}", boldLabel: true));

        return elements.ToArray();
    }

    private static void BuildUpdDocument(
        Body body,
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber)
    {
        body.Append(
            BuildUpdTitleTable(invoice, documentNumber),
            BuildUpdPartiesTable(invoice, organization, counterparty, agreement, documentNumber),
            BuildUpdItemsTable(invoice),
            BuildUpdTransferTable(invoice, organization, counterparty, documentNumber),
            LandscapeSection());
    }

    private static Table BuildUpdTitleTable(InvoiceData invoice, string? documentNumber)
    {
        var table = FixedTable(5200, 6800, 3200, bordered: false);
        table.Append(
            Row(
                Cell("Универсальный передаточный документ", 5200, bold: true, fontSize: 24, bordered: false),
                Cell($"Счет-фактура № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy} г. (1)", 6800, bold: true, fontSize: 20, bordered: false),
                Cell("Приложение № 1 к постановлению Правительства РФ № 1137", 3200, fontSize: 14, align: JustificationValues.Right, bordered: false)),
            Row(
                Cell("Статус: 1", 5200, bold: true, fontSize: 22, bordered: false),
                Cell("Исправление № -- от -- (1а)", 6800, fontSize: 18, bordered: false),
                Cell("Код вида товара (1б)", 3200, fontSize: 16, align: JustificationValues.Right, bordered: false)));
        return table;
    }

    private static Table BuildUpdPartiesTable(
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        ODataEntity? agreement,
        string? documentNumber)
    {
        var table = FixedTable(2900, 7400, 2600, 2900, bordered: true);
        table.Append(
            Row(Cell("Продавец:", 2900, bold: true), Cell(organization.Description, 7400), Cell("(2)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Адрес:", 2900, bold: true), Cell(GetAddress(organization), 7400), Cell("(2а)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("ИНН/КПП продавца:", 2900, bold: true), Cell(InnKpp(organization), 7400), Cell("(2б)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Грузоотправитель и его адрес:", 2900, bold: true), Cell("он же", 7400), Cell("(3)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Грузополучатель и его адрес:", 2900, bold: true), Cell(counterparty.Description, 7400), Cell("(4)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("К платежно-расчетному документу:", 2900, bold: true), Cell("-", 7400), Cell("(5)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Документ об отгрузке:", 2900, bold: true), Cell($"УПД № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}", 7400), Cell("(5а)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("К счету-фактуре, на основании которого составлен документ об отгрузке:", 2900, bold: true), Cell("-", 7400), Cell("(5б)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Покупатель:", 2900, bold: true), Cell(counterparty.Description, 7400), Cell("(6)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Адрес:", 2900, bold: true), Cell(GetAddress(counterparty), 7400), Cell("(6а)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("ИНН/КПП покупателя:", 2900, bold: true), Cell(InnKpp(counterparty), 7400), Cell("(6б)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)),
            Row(Cell("Валюта:", 2900, bold: true), Cell("Российский рубль, 643", 7400), Cell("(7)", 2600, align: JustificationValues.Center), Cell(FirstNonEmpty(agreement?.Description, invoice.AgreementName), 2900)),
            Row(Cell("Идентификатор государственного контракта, договора (соглашения):", 2900, bold: true), Cell("-", 7400), Cell("(8)", 2600, align: JustificationValues.Center), Cell(string.Empty, 2900)));
        return table;
    }

    private static Table BuildUpdItemsTable(InvoiceData invoice)
    {
        int[] widths = [380, 2450, 620, 620, 620, 560, 560, 760, 980, 1080, 860, 900, 1000, 1100, 680, 680];
        var table = FixedTable(widths);
        table.Append(
            HeaderRowWithWidths(widths, "№ п/п", "Наименование товара (описание выполненных работ, оказанных услуг)", "Код товара/работ", "Код вида товара", "Артикул", "Код", "Ед.", "Кол-во", "Цена", "Стоимость без НДС", "Акциз", "Ставка НДС", "Сумма НДС", "Стоимость с НДС", "Страна", "ГТД"),
            HeaderRowWithWidths(widths, "А", "1", "1а", "1б", "1в", "2", "2а", "3", "4", "5", "6", "7", "8", "9", "10а", "11"));

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            var vat = CalculateIncludedVat(item.Amount, item.VatRate);
            table.Append(Row(
                Cell((i + 1).ToString(Ru), widths[0], align: JustificationValues.Center),
                Cell(ItemName(item), widths[1]),
                Cell(string.Empty, widths[2]),
                Cell(string.Empty, widths[3]),
                Cell(string.Empty, widths[4]),
                Cell("796", widths[5], align: JustificationValues.Center),
                Cell("шт", widths[6], align: JustificationValues.Center),
                Cell(item.Quantity.ToString("N2", Ru), widths[7], align: JustificationValues.Right),
                Cell(Money(item.Price), widths[8], align: JustificationValues.Right),
                Cell(Money(item.Amount - vat), widths[9], align: JustificationValues.Right),
                Cell("без акциза", widths[10], align: JustificationValues.Center),
                Cell(VatRateText(item.VatRate), widths[11], align: JustificationValues.Center),
                Cell(Money(vat), widths[12], align: JustificationValues.Right),
                Cell(Money(item.Amount), widths[13], align: JustificationValues.Right),
                Cell(string.Empty, widths[14]),
                Cell(string.Empty, widths[15])));
        }

        table.Append(Row(
            Cell(string.Empty, widths[0]),
            Cell("Всего к оплате", widths[1] + widths[2] + widths[3] + widths[4] + widths[5] + widths[6] + widths[7] + widths[8], colspan: 8, bold: true),
            Cell(Money(Total(invoice) - TotalVat(invoice)), widths[9], bold: true, align: JustificationValues.Right),
            Cell(string.Empty, widths[10]),
            Cell(string.Empty, widths[11]),
            Cell(Money(TotalVat(invoice)), widths[12], bold: true, align: JustificationValues.Right),
            Cell(Money(Total(invoice)), widths[13], bold: true, align: JustificationValues.Right),
            Cell(string.Empty, widths[14]),
            Cell(string.Empty, widths[15])));
        return table;
    }

    private static Table BuildUpdTransferTable(
        InvoiceData invoice,
        ODataEntity organization,
        ODataEntity counterparty,
        string? documentNumber)
    {
        var table = FixedTable(3700, 4100, 3700, 4100, bordered: true);
        table.Append(
            Row(Cell("Документ составлен на", 3700, bold: true), Cell("1 листе", 4100), Cell("Всего передано", 3700, bold: true), Cell($"на сумму {Money(Total(invoice))} руб.", 4100)),
            Row(Cell("Основание передачи", 3700, bold: true), Cell($"УПД № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}", 4100), Cell("Дата отгрузки", 3700, bold: true), Cell(invoice.DocumentDate.ToString("dd.MM.yyyy", Ru), 4100)),
            Row(Cell("Ответственный за правильность оформления факта хозяйственной жизни", 7800, colspan: 2, bold: true), Cell("Ответственный за приемку", 7800, colspan: 2, bold: true)),
            Row(Cell("__________________ / " + organization.Description, 7800, colspan: 2), Cell("__________________ / " + counterparty.Description, 7800, colspan: 2)),
            Row(Cell("Руководитель организации-продавца", 3700, bold: true), Cell("__________________", 4100), Cell("Получил груз/услуги", 3700, bold: true), Cell("__________________", 4100)),
            Row(Cell("Главный бухгалтер", 3700, bold: true), Cell("__________________", 4100), Cell("Дата получения", 3700, bold: true), Cell(invoice.DocumentDate.ToString("dd.MM.yyyy", Ru), 4100)),
            Row(Cell("Отпуск груза произвел / услуги передал", 3700, bold: true), Cell("__________________", 4100), Cell("Груз получил / услуги принял", 3700, bold: true), Cell("__________________", 4100)),
            Row(Cell("Основание полномочий представителя продавца", 3700, bold: true), Cell("-", 4100), Cell("Основание полномочий представителя покупателя", 3700, bold: true), Cell("-", 4100)));
        return table;
    }

    private static void FillUpd(
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
            SetLabelValue(titleTable, "Счет-фактура №", documentNumber ?? invoice.Number);
            SetCellText(titleTable, 0, 5, $"от {invoice.DocumentDate:dd.MM.yyyy} г.");
        }

        var partyTable = tables.ElementAtOrDefault(1);
        if (partyTable is not null)
        {
            SetLabelValue(partyTable, "Продавец:", organization.Description);
            SetLabelValue(partyTable, "Покупатель:", counterparty.Description);
            SetLabelValue(partyTable, "Адрес:", FirstNonEmpty(GetAny(organization, "Адрес", "ЮридическийАдрес"), string.Empty));
            SetCellText(partyTable, 1, 8, FirstNonEmpty(GetAny(counterparty, "Адрес", "ЮридическийАдрес"), string.Empty));
            SetLabelValue(partyTable, "ИНН/КПП продавца:", InnKpp(organization));
            SetCellText(partyTable, 2, 8, InnKpp(counterparty));
            SetLabelValue(partyTable, "Идентификатор государственного контракта", agreement?.Description ?? invoice.AgreementName);
            SetRowTextContaining(partyTable, "Документ об отгрузке", $"Документ об отгрузке: Универсальный передаточный документ, № {documentNumber ?? invoice.Number} от {invoice.DocumentDate:dd.MM.yyyy}");
        }

        FillUpdItems(tables.ElementAtOrDefault(2), invoice);
    }

    private static void FillUpdItems(Table? table, InvoiceData invoice)
    {
        if (table is null)
            return;

        var rows = table.Elements<TableRow>().ToList();
        var columnsIndex = FindRowIndexContaining(table, "10а");
        var totalIndex = FindRowIndexContaining(table, "Всего к оплате");
        if (columnsIndex < 0 || totalIndex < 0 || totalIndex <= columnsIndex)
            return;

        var template = rows.ElementAtOrDefault(columnsIndex + 1)?.CloneNode(true) as TableRow ?? CreateRow(16);
        var totalRow = rows[totalIndex];
        foreach (var row in rows.Skip(columnsIndex + 1).Take(totalIndex - columnsIndex - 1).ToList())
            row.Remove();

        for (var i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var item = invoice.InvoiceItems[i];
            var row = (TableRow)template.CloneNode(true);
            var vat = CalculateIncludedVat(item.Amount, item.VatRate);
            SetRowTexts(row,
                string.Empty,
                (i + 1).ToString(Ru),
                ItemName(item),
                string.Empty,
                "796",
                "шт",
                item.Quantity.ToString("N2", Ru),
                Money(item.Price),
                Money(item.Amount - vat),
                "без акциза",
                VatRateText(item.VatRate),
                Money(vat),
                Money(item.Amount),
                string.Empty,
                string.Empty,
                string.Empty);
            table.InsertBefore(row, totalRow);
        }

        SetRowTexts(totalRow,
            string.Empty,
            "Всего к оплате",
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            Money(Total(invoice) - TotalVat(invoice)),
            string.Empty,
            string.Empty,
            Money(TotalVat(invoice)),
            Money(Total(invoice)),
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static Table FixedTable(params int[] widths) => FixedTable(widths, bordered: true);

    private static Table FixedTable(int[] widths, bool bordered)
    {
        var table = new Table();
        table.Append(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            new TableLayout { Type = TableLayoutValues.Fixed },
            bordered ? Borders() : NoBorders()));
        table.Append(new TableGrid(widths.Select(width => new GridColumn { Width = width.ToString(Ru) }).ToArray()));
        return table;
    }

    private static TableProperties FixedTableProperties(bool bordered)
        => new(new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct }, new TableLayout { Type = TableLayoutValues.Fixed }, bordered ? Borders() : NoBorders());

    private static Table FixedTable(int width1, int width2, bool bordered)
    {
        var table = new Table();
        table.Append(FixedTableProperties(bordered));
        table.Append(new TableGrid(new GridColumn { Width = width1.ToString(Ru) }, new GridColumn { Width = width2.ToString(Ru) }));
        return table;
    }

    private static Table FixedTable(int width1, int width2, int width3, bool bordered = true)
    {
        var table = new Table();
        table.Append(FixedTableProperties(bordered));
        table.Append(new TableGrid(
            new GridColumn { Width = width1.ToString(Ru) },
            new GridColumn { Width = width2.ToString(Ru) },
            new GridColumn { Width = width3.ToString(Ru) }));
        return table;
    }

    private static Table FixedTable(int width1, int width2, int width3, int width4, bool bordered = true)
    {
        var table = new Table();
        table.Append(FixedTableProperties(bordered));
        table.Append(new TableGrid(
            new GridColumn { Width = width1.ToString(Ru) },
            new GridColumn { Width = width2.ToString(Ru) },
            new GridColumn { Width = width3.ToString(Ru) },
            new GridColumn { Width = width4.ToString(Ru) }));
        return table;
    }

    private static Table FixedTable(int width1, int width2, int width3, int width4, int width5, int width6)
    {
        var table = new Table();
        table.Append(FixedTableProperties(bordered: true));
        table.Append(new TableGrid(
            new GridColumn { Width = width1.ToString(Ru) },
            new GridColumn { Width = width2.ToString(Ru) },
            new GridColumn { Width = width3.ToString(Ru) },
            new GridColumn { Width = width4.ToString(Ru) },
            new GridColumn { Width = width5.ToString(Ru) },
            new GridColumn { Width = width6.ToString(Ru) }));
        return table;
    }

    private static TableBorders Borders()
        => new(
            new TopBorder { Val = BorderValues.Single, Size = 4 },
            new BottomBorder { Val = BorderValues.Single, Size = 4 },
            new LeftBorder { Val = BorderValues.Single, Size = 4 },
            new RightBorder { Val = BorderValues.Single, Size = 4 },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 });

    private static TableBorders NoBorders()
        => new(
            new TopBorder { Val = BorderValues.None },
            new BottomBorder { Val = BorderValues.None },
            new LeftBorder { Val = BorderValues.None },
            new RightBorder { Val = BorderValues.None },
            new InsideHorizontalBorder { Val = BorderValues.None },
            new InsideVerticalBorder { Val = BorderValues.None });

    private static TableRow HeaderRow(params string[] values)
        => Row(values.Select((value, index) => Cell(value, HeaderWidths(values.Length)[index], bold: true, align: JustificationValues.Center, shaded: true)).ToArray());

    private static TableRow HeaderRowWithWidths(IReadOnlyList<int> widths, params string[] values)
        => Row(values.Select((value, index) => Cell(value, widths[index], bold: true, align: JustificationValues.Center, shaded: true, fontSize: 14)).ToArray());

    private static int[] HeaderWidths(int count)
        => count == 6 ? [520, 4520, 900, 760, 1680, 1680] : Enumerable.Repeat(PageWidth / count, count).ToArray();

    private static TableRow Row(params TableCell[] cells) => new(cells);

    private static TableCell Cell(
        string? text,
        int width,
        bool bold = false,
        bool shaded = false,
        bool bordered = true,
        int colspan = 1,
        JustificationValues? align = null,
        int fontSize = 18,
        bool rowspanBottomBorder = true)
    {
        var properties = new TableCellProperties(
            new TableCellWidth { Width = width.ToString(Ru), Type = TableWidthUnitValues.Dxa },
            new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center },
            new TableCellMargin(
                new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa },
                new TopMargin { Width = "40", Type = TableWidthUnitValues.Dxa },
                new BottomMargin { Width = "40", Type = TableWidthUnitValues.Dxa }));
        if (colspan > 1)
            properties.Append(new GridSpan { Val = colspan });
        if (shaded)
            properties.Append(new Shading { Val = ShadingPatternValues.Clear, Fill = "EDEDED" });
        if (!bordered || !rowspanBottomBorder)
            properties.Append(new TableCellBorders(new BottomBorder { Val = rowspanBottomBorder && bordered ? BorderValues.Single : BorderValues.None }));

        return new TableCell(properties, Paragraph(text ?? string.Empty, bold, fontSize, align));
    }

    private static Paragraph Paragraph(
        string text,
        bool bold = false,
        int fontSize = 20,
        JustificationValues? align = null,
        bool boldLabel = false)
    {
        var paragraph = new Paragraph();
        var properties = new ParagraphProperties(new SpacingBetweenLines { After = "80" });
        if (align is not null)
            properties.Append(new Justification { Val = align });
        paragraph.Append(properties);

        if (boldLabel && text.Contains(':'))
        {
            var index = text.IndexOf(':');
            paragraph.Append(Run(text[..(index + 1)], bold: true, fontSize));
            paragraph.Append(Run(text[(index + 1)..], bold: false, fontSize));
            return paragraph;
        }

        paragraph.Append(Run(text, bold, fontSize));
        return paragraph;
    }

    private static Run Run(string text, bool bold, int fontSize)
    {
        var properties = new RunProperties(new FontSize { Val = fontSize.ToString(Ru) }, new FontSizeComplexScript { Val = fontSize.ToString(Ru) });
        if (bold)
            properties.Append(new Bold());
        return new Run(properties, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }

    private static Paragraph Separator()
    {
        var paragraph = new Paragraph(new ParagraphProperties(new ParagraphBorders(new BottomBorder { Val = BorderValues.Single, Size = 8 })));
        paragraph.Append(new Run(new Text(string.Empty)));
        return paragraph;
    }

    private static Paragraph Space() => Paragraph(string.Empty, fontSize: 8);

    private static SectionProperties Section()
        => new(
            new PageSize { Width = 11906, Height = 16838 },
            new PageMargin { Top = 850, Right = 850, Bottom = 850, Left = 850, Header = 0, Footer = 0, Gutter = 0 });

    private static SectionProperties LandscapeSection()
        => new(
            new PageSize { Width = 16838, Height = 11906, Orient = PageOrientationValues.Landscape },
            new PageMargin { Top = 560, Right = 560, Bottom = 560, Left = 560, Header = 0, Footer = 0, Gutter = 0 });

    private static void CopyTemplate(string templateFileName, string destination)
    {
        var templatePath = ResolveTemplate(templateFileName);
        File.Copy(templatePath, destination, overwrite: true);
    }

    private static string ResolveTemplate(string templateFileName)
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFileName),
            Path.Combine(AppContext.BaseDirectory, "Templates", templateFileName),
            Path.Combine(PathResolver.ResolveFromProjectRoot("Templates"), templateFileName)
        };

        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null)
            throw new FileNotFoundException($"Не найден шаблон печатной формы '{templateFileName}'.", templateFileName);

        return path;
    }

    private static string BuildOutputPath(string outputDir, string filePrefix, string number)
    {
        var safeNumber = SafeFileName(string.IsNullOrWhiteSpace(number) ? "без_номера" : number);
        return Path.Combine(outputDir, $"{filePrefix}_{safeNumber}_{DateTime.Now:yyyyMMddHHmmssfff}.docx");
    }

    private static void SetLabelValue(Table table, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            for (var i = 0; i < cells.Count; i++)
            {
                if (!CellText(cells[i]).Contains(label, StringComparison.OrdinalIgnoreCase))
                    continue;

                var target = cells.Skip(i + 1).FirstOrDefault(cell => !CellText(cell).Trim().StartsWith("(", StringComparison.Ordinal));
                if (target is not null)
                {
                    SetCellText(target, value);
                    return;
                }

                SetCellText(cells[i], $"{label} {value}");
                return;
            }
        }
    }

    private static void SetRowTextContaining(Table table, string contains, string value)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var cells = row.Elements<TableCell>().ToList();
            var cell = cells.FirstOrDefault(candidate => CellText(candidate).Contains(contains, StringComparison.OrdinalIgnoreCase));
            if (cell is not null)
            {
                SetCellText(cell, value);
                return;
            }
        }
    }

    private static void SetCellText(Table table, int rowIndex, int cellIndex, string? value)
    {
        var cell = table.Elements<TableRow>().ElementAtOrDefault(rowIndex)?.Elements<TableCell>().ElementAtOrDefault(cellIndex);
        if (cell is not null)
            SetCellText(cell, value ?? string.Empty);
    }

    private static void SetCellText(TableCell cell, string value)
    {
        cell.RemoveAllChildren<Paragraph>();
        cell.Append(Paragraph(value, fontSize: 18));
    }

    private static void SetRowTexts(TableRow row, params string[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        while (cells.Count < values.Length)
        {
            var cell = new TableCell();
            row.Append(cell);
            cells.Add(cell);
        }

        for (var i = 0; i < values.Length; i++)
            SetCellText(cells[i], values[i]);
    }

    private static TableRow CreateRow(int cellCount)
    {
        var row = new TableRow();
        for (var i = 0; i < cellCount; i++)
            row.Append(new TableCell(new Paragraph()));
        return row;
    }

    private static int FindRowIndexContaining(Table table, string text)
    {
        var rows = table.Elements<TableRow>().ToList();
        for (var i = 0; i < rows.Count; i++)
        {
            if (RowText(rows[i]).Contains(text, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static string RowText(TableRow row) => string.Join(" ", row.Elements<TableCell>().Select(CellText));

    private static string CellText(TableCell cell) => cell.InnerText ?? string.Empty;

    private static string FormatParty(ODataEntity entity)
    {
        var inn = Get(entity, "ИНН");
        var kpp = Get(entity, "КПП");
        var parts = new List<string> { entity.Description };
        if (!string.IsNullOrWhiteSpace(inn))
            parts.Add($"ИНН {inn}");
        if (!string.IsNullOrWhiteSpace(kpp))
            parts.Add($"КПП {kpp}");
        return string.Join(", ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string InnKpp(ODataEntity entity)
    {
        var inn = Get(entity, "ИНН");
        var kpp = Get(entity, "КПП");
        return string.IsNullOrWhiteSpace(kpp) ? inn : $"{inn}/{kpp}";
    }

    private static string Get(ODataEntity entity, string field)
    {
        if (field.Contains('.', StringComparison.Ordinal))
            return GetNested(entity.Raw, field.Split('.', StringSplitOptions.RemoveEmptyEntries));

        return entity.Raw.TryGetValue(field, out var value) ? FormatRawValue(value) : string.Empty;
    }

    private static string GetNested(Dictionary<string, object?> raw, IReadOnlyList<string> path)
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

        return FormatRawValue(current);
    }

    private static string FormatRawValue(object? value)
    {
        if (value is null)
            return string.Empty;

        if (value is Dictionary<string, object?> dictionary)
        {
            foreach (var field in new[] { "Description", "Наименование", "НаименованиеБанка", "Представление" })
            {
                if (dictionary.TryGetValue(field, out var nestedValue))
                {
                    var text = Convert.ToString(nestedValue, Ru);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }

            return string.Empty;
        }

        return Convert.ToString(value, Ru) ?? string.Empty;
    }

    private static string GetAny(ODataEntity entity, params string[] fields)
    {
        foreach (var field in fields)
        {
            var value = Get(entity, field);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static string GetAnyOptional(ODataEntity? entity, params string[] fields)
    {
        if (entity is null)
            return string.Empty;

        return GetAny(entity, fields);
    }

    private static string GetAddress(ODataEntity entity)
        => GetAny(entity, "Адрес", "ЮридическийАдрес", "АдресСтрокой", "ПредставлениеАдреса", "ФактическийАдрес");

    private static decimal Total(InvoiceData invoice) => invoice.InvoiceItems.Sum(item => item.Amount);

    private static decimal TotalVat(InvoiceData invoice) => invoice.InvoiceItems.Sum(item => CalculateIncludedVat(item.Amount, item.VatRate));

    private static string VatText(InvoiceData invoice)
    {
        var vat = TotalVat(invoice);
        return vat <= 0 ? "Без налога (НДС)" : Money(vat);
    }

    private static string ItemName(InvoiceItemData item) => FirstNonEmpty(item.NomenclatureDescription, item.NomenclatureName);

    private static decimal CalculateIncludedVat(decimal amount, string vatRate)
    {
        var percent = ExtractVatPercent(vatRate);
        if (percent <= 0)
            return 0m;

        return Math.Round(amount * percent / (100m + percent), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ExtractVatPercent(string vatRate)
    {
        if (string.IsNullOrWhiteSpace(vatRate) || vatRate.Contains("без", StringComparison.OrdinalIgnoreCase))
            return 0m;

        var match = Regex.Match(vatRate, @"\d+");
        return match.Success ? decimal.Parse(match.Value, CultureInfo.InvariantCulture) : 0m;
    }

    private static string VatRateText(string vatRate)
    {
        if (string.IsNullOrWhiteSpace(vatRate) || vatRate.Contains("без", StringComparison.OrdinalIgnoreCase))
            return "без НДС";

        var percent = ExtractVatPercent(vatRate);
        return percent <= 0 ? vatRate : $"{percent:0}%";
    }

    private static string Money(decimal value) => value.ToString("N2", Ru);

    private static string FormatDate(DateTime date) => date.ToString("dd MMMM yyyy", Ru);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "без_номера" : cleaned;
    }

    private static string AmountInWords(decimal amount)
    {
        var rubles = (long)Math.Floor(amount);
        var kopecks = (int)Math.Round((amount - rubles) * 100m, 0, MidpointRounding.AwayFromZero);
        if (kopecks == 100)
        {
            rubles++;
            kopecks = 0;
        }

        var words = rubles == 0 ? "ноль" : NumberToWords(rubles);
        return $"{Capitalize(words)} {ChooseForm(rubles, "рубль", "рубля", "рублей")} {kopecks:00} {ChooseForm(kopecks, "копейка", "копейки", "копеек")}";
    }

    private static string NumberToWords(long value)
    {
        var groups = new[]
        {
            (Value: value / 1_000_000_000, Forms: new[] { "миллиард", "миллиарда", "миллиардов" }, Feminine: false),
            (Value: value / 1_000_000 % 1000, Forms: new[] { "миллион", "миллиона", "миллионов" }, Feminine: false),
            (Value: value / 1_000 % 1000, Forms: new[] { "тысяча", "тысячи", "тысяч" }, Feminine: true),
            (Value: value % 1000, Forms: Array.Empty<string>(), Feminine: false)
        };

        var parts = new List<string>();
        foreach (var group in groups)
        {
            if (group.Value == 0)
                continue;

            parts.Add(HundredsToWords((int)group.Value, group.Feminine));
            if (group.Forms.Length > 0)
                parts.Add(ChooseForm(group.Value, group.Forms[0], group.Forms[1], group.Forms[2]));
        }

        return string.Join(" ", parts);
    }

    private static string HundredsToWords(int value, bool feminine)
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
            parts.Add(teens[rest - 10]);
        else
        {
            if (rest / 10 > 0)
                parts.Add(tens[rest / 10]);
            if (rest % 10 > 0)
                parts.Add(ones[rest % 10]);
        }

        return string.Join(" ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string ChooseForm(long value, string one, string two, string five)
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

    private static string Capitalize(string value)
        => string.IsNullOrWhiteSpace(value) ? value : char.ToUpper(value[0], Ru) + value[1..];
}

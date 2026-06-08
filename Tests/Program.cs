using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.Logging.Abstractions;
using OneCFreshInvoiceODataBot.Models;
using OneCFreshInvoiceODataBot.Services;
using System.Net;
using System.Text;

TestExcelReaderUsesCounterpartyNameForNewReferences();
TestCounterpartyEnrichmentMapsOrgRegisterResponse();
await TestDocxPrintFormGeneratorCreatesInvoiceDocument();
await TestDocxPrintFormGeneratorCreatesRealizationDocuments();
await TestOneCPrintPdfProviderDownloadsInvoicePdf();
TestTaxableInvoiceUsesCalculatedVat();
TestWithoutVatInvoiceSetsDocumentFlag();
TestTaxableRealizationUsesCalculatedVat();
TestZeroRateRealizationUsesUpd();
TestWithoutVatRealizationDoesNotUseUpd();
TestStoredInvoiceRealizationUsesCalculatedVat();
TestTaxableRealizationBuildsIssuedInvoice();
TestWithoutVatRealizationSkipsIssuedInvoice();

Console.WriteLine("VAT regression tests passed.");

static void TestExcelReaderUsesCounterpartyNameForNewReferences()
{
    var path = Path.Combine(Path.GetTempPath(), $"invoice-reader-{Guid.NewGuid():N}.xlsx");
    try
    {
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("data");
            var headers = new[]
            {
                "Номер",
                "Дата документа",
                "ИНН Контрагента",
                "Контрагент",
                "Договор",
                "Номенклатура",
                "Номенклатура - описание",
                "Количество",
                "Цена",
                "СтавкаНДС"
            };

            for (var i = 0; i < headers.Length; i++)
                sheet.Cell(1, i + 1).Value = headers[i];

            sheet.Cell(2, 1).Value = "1";
            sheet.Cell(2, 2).Value = new DateTime(2026, 6, 2);
            sheet.Cell(2, 3).Value = "7700000000";
            sheet.Cell(2, 4).Value = "ООО Новый контрагент";
            sheet.Cell(2, 5).Value = "Основной договор";
            sheet.Cell(2, 6).Value = "Услуга";
            sheet.Cell(2, 7).Value = "Услуга";
            sheet.Cell(2, 8).Value = 1;
            sheet.Cell(2, 9).Value = 1200;
            sheet.Cell(2, 10).Value = "20%";

            workbook.SaveAs(path);
        }

        var invoice = new ExcelInvoiceReader().Read(path).Single();
        AssertEqual("ООО Новый контрагент", invoice.CounterpartyName, "название контрагента из Excel");
    }
    finally
    {
        if (File.Exists(path))
            File.Delete(path);
    }
}

static void TestCounterpartyEnrichmentMapsOrgRegisterResponse()
{
    const string json = """"
        {
          "inn": "7707083893",
          "ogrn": "1027700132195",
          "registrationDate": "1991-06-20",
          "registeredStateAgencyName": "Управление Федеральной налоговой службы по г.Москве",
          "registeredStateAgencyCode": "7700",
          "kpp": { "value": "773601001" },
          "name": {
            "shortName": "ПАО СБЕРБАНК",
            "fullName": "Публичное акционерное общество \"СБЕРБАНК РОССИИ\"",
            "commonName": "ПАО СБЕРБАНК"
          },
          "status": { "name": "Действующее" },
          "address": { "valueWithPostalCode": "117312, Город Москва, ул. Вавилова, дом 19" },
          "headPersonInfo": {
            "director": {
              "name": "Герман",
              "lastName": "Греф",
              "patronymic": "Оскарович",
              "position": "Президент, Председатель Правления"
            }
          }
        }
        """";

    var details = CounterpartyEnrichmentClient.ParseResponse(json);
    var payload = CounterpartyEnrichmentClient.ApplyToPayload(
        new ReferenceMap(),
        new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        details,
        fallbackInn: "7707083893",
        fallbackDescription: "7707083893");

    AssertEqual("ПАО СБЕРБАНК", payload["Description"], "краткое название контрагента");
    AssertEqual("Публичное акционерное общество \"СБЕРБАНК РОССИИ\"", payload["НаименованиеПолное"], "полное название контрагента");
    AssertEqual("7707083893", payload["ИНН"], "ИНН контрагента");
    AssertEqual("773601001", payload["КПП"], "КПП контрагента");
    AssertEqual("1027700132195", payload["РегистрационныйНомер"], "ОГРН контрагента");
    AssertEqual(new DateTime(1991, 6, 20), payload["ДатаРегистрации"], "дата регистрации контрагента");
    AssertMissing(payload, "ГосударственныйОрган", "булево поле ГосударственныйОрган");
    AssertEqual("7700", payload["КодГосударственногоОргана"], "код регистрирующего органа");
    AssertEqual(
        "Статус: Действующее; Регистрирующий орган: Управление Федеральной налоговой службы по г.Москве; Руководитель: Греф Герман Оскарович, Президент, Председатель Правления; Адрес: 117312, Город Москва, ул. Вавилова, дом 19",
        payload["ДополнительнаяИнформация"],
        "дополнительная информация контрагента");
}

static async Task TestDocxPrintFormGeneratorCreatesInvoiceDocument()
{
    var (_, invoice, counterparty, _) = CreateFixture("20%", vatAmountFromExcel: null);
    var outputDir = Path.Combine(Path.GetTempPath(), $"docx-print-{Guid.NewGuid():N}");
    Directory.CreateDirectory(outputDir);

    try
    {
        var generator = new DocxPrintFormGenerator();
        var path = await generator.CreateInvoiceAsync(
            invoice,
            new ODataEntity
            {
                Description = "ООО Продавец",
                Raw = new Dictionary<string, object?>
                {
                    ["ИНН"] = "7700000001",
                    ["КПП"] = "770001001",
                    ["Адрес"] = "125009, г. Москва, ул. Примерная, д. 1"
                }
            },
            new ODataEntity
            {
                RefKey = counterparty.RefKey,
                Description = "ООО Покупатель",
                Raw = new Dictionary<string, object?>
                {
                    ["ИНН"] = "7700000002",
                    ["КПП"] = "770002001",
                    ["Адрес"] = "140108, Московская область, г. Раменское, ул. Михаилевича, д. 51А"
                }
            },
            agreement: null,
            bankAccount: new ODataEntity
            {
                Description = "40702810900000000001",
                Raw = new Dictionary<string, object?>
                {
                    ["НомерСчета"] = "40702810900000000001",
                    ["Банк"] = new Dictionary<string, object?>
                    {
                        ["Description"] = "ООО \"Банк Точка\"",
                        ["БИК"] = "044525104",
                        ["КоррСчет"] = "30101810745374525104"
                    }
                }
            },
            documentNumber: "A-15",
            outputDir,
            CancellationToken.None);

        AssertEqual(true, File.Exists(path), "файл печатной формы счета");
        var text = ReadDocxText(path);
        AssertContains(text, "Счет на оплату");
        AssertContains(text, "A-15");
        AssertContains(text, "ООО Покупатель");
        AssertContains(text, "Услуга");
        AssertContains(text, "200,00");
        AssertContains(text, "7700000001");
        AssertContains(text, "770001001");
        AssertContains(text, "044525104");
        AssertContains(text, "30101810745374525104");
        AssertContains(text, "40702810900000000001");
        AssertContains(text, "Получатель");
    }
    finally
    {
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
    }
}

static async Task TestDocxPrintFormGeneratorCreatesRealizationDocuments()
{
    var (_, invoice, counterparty, _) = CreateFixture("20%", vatAmountFromExcel: null);
    var outputDir = Path.Combine(Path.GetTempPath(), $"docx-print-realization-{Guid.NewGuid():N}");
    Directory.CreateDirectory(outputDir);

    try
    {
        var generator = new DocxPrintFormGenerator();
        var organization = new ODataEntity
        {
            Description = "ООО Продавец",
            Raw = new Dictionary<string, object?>
            {
                ["ИНН"] = "7700000001",
                ["КПП"] = "770001001",
                ["Адрес"] = "125009, г. Москва, ул. Примерная, д. 1"
            }
        };
        var buyer = new ODataEntity
        {
            RefKey = counterparty.RefKey,
            Description = "ООО Покупатель",
            Raw = new Dictionary<string, object?>
            {
                ["ИНН"] = "7700000002",
                ["КПП"] = "770002001",
                ["Адрес"] = "140108, Московская область, г. Раменское, ул. Михаилевича, д. 51А"
            }
        };

        var actPath = await generator.CreateRealizationAsync(
            "Act",
            invoice,
            organization,
            buyer,
            agreement: null,
            documentNumber: "ACT-15",
            outputDir,
            CancellationToken.None);
        var updPath = await generator.CreateRealizationAsync(
            "\u0423\u041F\u0414",
            invoice,
            organization,
            buyer,
            agreement: null,
            documentNumber: "UPD-15",
            outputDir,
            CancellationToken.None);

        AssertEqual(true, File.Exists(actPath), "act print form file");
        AssertEqual(true, File.Exists(updPath), "upd print form file");
        var actText = ReadDocxText(actPath);
        AssertContains(actText, "ACT-15");
        AssertContains(actText, "Исполнитель");
        AssertContains(actText, "Заказчик");
        AssertContains(actText, "7700000001/770001001");
        AssertContains(actText, "7700000002/770002001");
        AssertContains(actText, "125009, г. Москва");
        AssertContains(actText, "140108, Московская область");
        AssertContains(actText, "Исполнитель оказал, а Заказчик принял");
        var updText = ReadDocxText(updPath);
        AssertContains(updText, "UPD-15");
        AssertContains(updText, "Статус: 1");
        AssertContains(updText, "(5б)");
        AssertContains(updText, "7700000001/770001001");
        AssertContains(updText, "7700000002/770002001");
        AssertContains(updText, "125009, г. Москва");
        AssertContains(updText, "140108, Московская область");
        AssertContains(updText, "Код вида товара");
        AssertContains(updText, "(1б)");
        AssertContains(updText, "Идентификатор государственного контракта");
        AssertContains(updText, "(8)");
        AssertContains(updText, "Главный бухгалтер");
        AssertContains(updText, "Дата получения");
    }
    finally
    {
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
    }
}

static async Task TestOneCPrintPdfProviderDownloadsInvoicePdf()
{
    var outputDir = Path.Combine(Path.GetTempPath(), $"onec-pdf-provider-{Guid.NewGuid():N}");
    var requests = new List<string>();
    using var http = new HttpClient(new StubHttpMessageHandler(async request =>
    {
        requests.Add(await request.Content!.ReadAsStringAsync());
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent("%PDF-1.7\n%test"u8.ToArray())
        };
    }));

    try
    {
        var provider = new OneCPrintPdfProvider(
            new PrintServiceSettings
            {
                Enabled = true,
                Url = "https://example.test/hs/odata-bot-print/print"
            },
            new ODataMap
            {
                Invoice = new DocumentMap { EntitySet = "Document_СчетНаОплатуПокупателю" }
            },
            new ProcessingSettings { OutputDir = outputDir },
            NullLogger<OneCPrintPdfProvider>.Instance,
            http);

        var files = await provider.GetPdfFilesAsync(
            [
                new ProcessingResult
                {
                    Status = "Success",
                    InvoiceRefKey = "11111111-1111-1111-1111-111111111111",
                    InvoiceNumber = "INV-1"
                }
            ],
            CancellationToken.None);

        AssertEqual(1, files.Count, "количество PDF");
        AssertEqual(true, File.Exists(files[0]), "PDF файл");
        AssertContains(requests.Single(), "Document_");
        AssertContains(requests.Single(), "11111111-1111-1111-1111-111111111111");
    }
    finally
    {
        if (Directory.Exists(outputDir))
            Directory.Delete(outputDir, recursive: true);
    }
}

static void TestTaxableInvoiceUsesCalculatedVat()
{
    var payload = BuildInvoicePayload("20%", vatAmountFromExcel: 999m);
    var line = GetOnlyLine(payload);

    AssertEqual("НДС20", line["СтавкаНДС"], "ставка НДС строки");
    AssertEqual(200m, line["СуммаНДС"], "рассчитанная сумма НДС строки");
    AssertEqual(false, payload["ДокументБезНДС"], "признак документа без НДС");
}

static void TestWithoutVatInvoiceSetsDocumentFlag()
{
    var payload = BuildInvoicePayload("Без НДС", vatAmountFromExcel: 999m);
    var line = GetOnlyLine(payload);

    AssertEqual("БезНДС", line["СтавкаНДС"], "ставка НДС строки");
    AssertEqual(0m, line["СуммаНДС"], "сумма НДС строки без НДС");
    AssertEqual(true, payload["ДокументБезНДС"], "признак документа без НДС");
}

static void TestTaxableRealizationUsesCalculatedVat()
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture("20%", vatAmountFromExcel: 999m);
    var payload = factory.BuildRealizationPayload(
        invoice,
        counterparty,
        new ODataEntity { RefKey = "33333333-3333-3333-3333-333333333333" },
        nomenclature,
        new Dictionary<string, ODataEntity>(),
        invoiceRefKey: "44444444-4444-4444-4444-444444444444");
    var rows = (object[])payload["Услуги"]!;
    var line = (Dictionary<string, object?>)rows.Single();

    AssertEqual("НДС20", line["СтавкаНДС"], "ставка НДС реализации");
    AssertEqual(200m, line["СуммаНДС"], "рассчитанная сумма НДС реализации");
    AssertEqual(false, payload["ДокументБезНДС"], "признак реализации без НДС");
    AssertEqual("УПД", payload["ВидЭлектронногоДокумента"], "вид электронного документа реализации");
    AssertEqual(true, payload["ЭтоУниверсальныйДокумент"], "признак универсального документа реализации");
}

static void TestWithoutVatRealizationDoesNotUseUpd()
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture("Без НДС", vatAmountFromExcel: 999m);
    var payload = factory.BuildRealizationPayload(
        invoice,
        counterparty,
        new ODataEntity { RefKey = "33333333-3333-3333-3333-333333333333" },
        nomenclature,
        new Dictionary<string, ODataEntity>(),
        invoiceRefKey: "44444444-4444-4444-4444-444444444444");

    AssertMissing(payload, "ВидЭлектронногоДокумента", "вид электронного документа реализации без НДС");
    AssertMissing(payload, "ЭтоУниверсальныйДокумент", "признак универсального документа реализации без НДС");
}

static void TestZeroRateRealizationUsesUpd()
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture("0%", vatAmountFromExcel: 999m);
    var payload = factory.BuildRealizationPayload(
        invoice,
        counterparty,
        new ODataEntity { RefKey = "33333333-3333-3333-3333-333333333333" },
        nomenclature,
        new Dictionary<string, ODataEntity>(),
        invoiceRefKey: "44444444-4444-4444-4444-444444444444");

    AssertEqual("УПД", payload["ВидЭлектронногоДокумента"], "вид электронного документа реализации со ставкой 0%");
    AssertEqual(true, payload["ЭтоУниверсальныйДокумент"], "признак универсального документа реализации со ставкой 0%");
    AssertNotNull(
        factory.BuildIssuedInvoicePayload(payload, "66666666-6666-6666-6666-666666666666", "101"),
        "счет-фактура для реализации со ставкой 0%");
}

static void TestStoredInvoiceRealizationUsesCalculatedVat()
{
    var map = CreateMap();
    map.Realization.OperationKindValue = "Услуги";
    var payload = new PayloadFactory(map).BuildRealizationPayload(
        new ODataEntity
        {
            RefKey = "44444444-4444-4444-4444-444444444444",
            Raw = new Dictionary<string, object?>
            {
                ["Number"] = "1",
                ["ДоговорКонтрагента_Key"] = "33333333-3333-3333-3333-333333333333",
                ["Контрагент_Key"] = "11111111-1111-1111-1111-111111111111",
                ["Организация_Key"] = "55555555-5555-5555-5555-555555555555",
                ["Товары"] = new List<object>
                {
                    new Dictionary<string, object?>
                    {
                        ["LineNumber"] = "1",
                        ["Номенклатура"] = "22222222-2222-2222-2222-222222222222",
                        ["Содержание"] = "Услуга",
                        ["Количество"] = 1m,
                        ["Цена"] = 1200m,
                        ["Сумма"] = 1200m,
                        ["СтавкаНДС"] = "НДС20",
                        ["СуммаНДС"] = 999m
                    }
                }
            }
        },
        new DateOnly(2026, 6, 2),
        new Dictionary<string, ODataEntity>());
    var rows = (object[])payload["Услуги"]!;
    var line = (Dictionary<string, object?>)rows.Single();

    AssertEqual("НДС20", line["СтавкаНДС"], "ставка НДС отложенной реализации");
    AssertEqual(200m, line["СуммаНДС"], "рассчитанная сумма НДС отложенной реализации");
    AssertEqual(false, payload["ДокументБезНДС"], "признак отложенной реализации без НДС");
    AssertEqual("УПД", payload["ВидЭлектронногоДокумента"], "вид электронного документа отложенной реализации");
    AssertEqual(true, payload["ЭтоУниверсальныйДокумент"], "признак универсального документа отложенной реализации");
}

static void TestTaxableRealizationBuildsIssuedInvoice()
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture("20%", vatAmountFromExcel: 999m);
    var realization = factory.BuildRealizationPayload(
        invoice,
        counterparty,
        new ODataEntity { RefKey = "33333333-3333-3333-3333-333333333333" },
        nomenclature,
        new Dictionary<string, ODataEntity>(),
        invoiceRefKey: "44444444-4444-4444-4444-444444444444");
    realization["Организация_Key"] = "55555555-5555-5555-5555-555555555555";

    var payload = factory.BuildIssuedInvoicePayload(
        realization,
        realizationRefKey: "66666666-6666-6666-6666-666666666666",
        realizationNumber: "101")!;
    var bases = (object[])payload["ДокументыОснования"]!;
    var basis = (Dictionary<string, object?>)bases.Single();

    AssertEqual("НаРеализацию", payload["ВидСчетаФактуры"], "вид счета-фактуры");
    AssertEqual("66666666-6666-6666-6666-666666666666", payload["ДокументОснование"], "основание счета-фактуры");
    AssertEqual("StandardODATA.Document_РеализацияТоваровУслуг", payload["ДокументОснование_Type"], "тип основания счета-фактуры");
    AssertEqual(1200m, payload["СуммаДокумента"], "сумма документа счета-фактуры");
    AssertEqual(200m, payload["СуммаНДСДокумента"], "сумма НДС счета-фактуры");
    AssertEqual("66666666-6666-6666-6666-666666666666", basis["ДокументОснование"], "строка основания счета-фактуры");
}

static void TestWithoutVatRealizationSkipsIssuedInvoice()
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture("Без НДС", vatAmountFromExcel: 999m);
    var realization = factory.BuildRealizationPayload(
        invoice,
        counterparty,
        new ODataEntity { RefKey = "33333333-3333-3333-3333-333333333333" },
        nomenclature,
        new Dictionary<string, ODataEntity>(),
        invoiceRefKey: "44444444-4444-4444-4444-444444444444");

    var payload = factory.BuildIssuedInvoicePayload(
        realization,
        realizationRefKey: "66666666-6666-6666-6666-666666666666",
        realizationNumber: "101");

    AssertEqual<Dictionary<string, object?>?>(null, payload, "счет-фактура для реализации без НДС");
}

static Dictionary<string, object?> BuildInvoicePayload(string vatRate, decimal? vatAmountFromExcel)
{
    var (factory, invoice, counterparty, nomenclature) = CreateFixture(vatRate, vatAmountFromExcel);
    return factory.BuildInvoicePayload(invoice, counterparty, agreement: null, nomenclature, bankAccountKey: string.Empty);
}

static (PayloadFactory Factory, InvoiceData Invoice, ODataEntity Counterparty, Dictionary<string, ODataEntity> Nomenclature)
    CreateFixture(string vatRate, decimal? vatAmountFromExcel)
{
    var map = CreateMap();
    var invoice = new InvoiceData
    {
        DocumentDate = new DateTime(2026, 6, 2),
        CounterpartyInn = "7700000000",
        Number = "1"
    };
    invoice.InvoiceItems.Add(new InvoiceItemData
    {
        NomenclatureName = "Услуга",
        NomenclatureDescription = "Услуга",
        Quantity = 1m,
        Price = 1200m,
        VatRate = vatRate,
        VatAmount = vatAmountFromExcel
    });

    return (
        new PayloadFactory(map),
        invoice,
        new ODataEntity { RefKey = "11111111-1111-1111-1111-111111111111" },
        new Dictionary<string, ODataEntity>
        {
            ["Услуга"] = new() { RefKey = "22222222-2222-2222-2222-222222222222" }
        });
}

static ODataMap CreateMap()
{
    var map = new ODataMap
    {
        DefaultOrganizationKey = "55555555-5555-5555-5555-555555555555",
        VatRates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["20%"] = "НДС20",
            ["0%"] = "НДС0",
            ["Без НДС"] = "БезНДС"
        }
    };
    map.Realization.OperationKindValue = "Услуги";
    map.Realization.UseUpdField = "ВидЭлектронногоДокумента";
    map.Realization.UseUpdFlagField = "ЭтоУниверсальныйДокумент";
    map.IssuedInvoice.Enabled = true;
    return map;
}

static Dictionary<string, object?> GetOnlyLine(Dictionary<string, object?> payload)
{
    var rows = (object[])payload["Товары"]!;
    return (Dictionary<string, object?>)rows.Single();
}

static void AssertEqual<T>(T expected, object? actual, string label)
{
    if (!Equals(expected, actual))
        throw new InvalidOperationException($"{label}: ожидалось '{expected}', получено '{actual}'.");
}

static void AssertMissing(Dictionary<string, object?> payload, string field, string label)
{
    if (payload.ContainsKey(field))
        throw new InvalidOperationException($"{label}: поле '{field}' не должно передаваться.");
}

static void AssertNotNull(object? value, string label)
{
    if (value is null)
        throw new InvalidOperationException($"{label}: ожидалось заполненное значение.");
}

static string ReadDocxText(string path)
{
    using var document = WordprocessingDocument.Open(path, false);
    return document.MainDocumentPart?.Document.Body?.InnerText ?? string.Empty;
}

static void AssertContains(string text, string expected)
{
    if (!text.Contains(expected, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Ожидался текст '{expected}', но его нет в документе.");
}

sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

    public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => _handler(request);
}

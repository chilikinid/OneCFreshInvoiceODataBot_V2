using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OneCFreshInvoiceODataBot.Models;
using OneCFreshInvoiceODataBot.Services;

using System.Text;
using System.Text.Json;

Console.OutputEncoding = Encoding.UTF8;

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    });
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger("Program");

try
{
    string localAppSettingsPath = "appsettings.local.json";
    if (args.Length > 0)
    {
        localAppSettingsPath = args[0];
    }

    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.example.json", optional: false, reloadOnChange: false)
        .AddJsonFile(localAppSettingsPath, optional: true, reloadOnChange: false)
        .AddEnvironmentVariables(prefix: "ONEC_BOT_")
        .Build();

    var settings = new AppSettings();
    configuration.Bind(settings);
    ValidateSettings(settings);

    var mapPath = PathResolver.ResolveFromProjectRoot(settings.Processing.ODataMapPath);
    if (!File.Exists(mapPath))
        throw new FileNotFoundException($"Не найден файл карты OData: {mapPath}. Скопируйте config/odata-map.example.json в config/odata-map.local.json и настройте поля.", mapPath);

    var mapJson = await File.ReadAllTextAsync(mapPath, Encoding.UTF8);
    var map = JsonSerializer.Deserialize<ODataMap>(mapJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Не удалось прочитать OData map.");

    Directory.CreateDirectory(PathResolver.ResolveFromProjectRoot(settings.Processing.OutputDir));

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        cts.Cancel();
    };

    using var odataClient = new ODataClient(settings.OData);

    if (settings.Processing.DownloadMetadataOnly)
    {
        var metadata = await odataClient.DownloadMetadataAsync(cts.Token);
        var metadataPath = Path.Combine(PathResolver.ResolveFromProjectRoot(settings.Processing.OutputDir), "metadata.xml");
        await File.WriteAllTextAsync(metadataPath, metadata, Encoding.UTF8, cts.Token);
        logger.LogInformation("$metadata сохранен: {Path}", metadataPath);
        return 0;
    }

    using var counterpartyEnrichmentClient = map.CounterpartyEnrichment.Enabled
        ? new CounterpartyEnrichmentClient(map.CounterpartyEnrichment)
        : null;
    var resolver = new ReferenceResolver(odataClient, map, counterpartyEnrichmentClient);
    var payloadFactory = new PayloadFactory(map);
    var processor = new InvoiceProcessor(
        odataClient,
        resolver,
        payloadFactory,
        map,
        settings.Processing,
        loggerFactory.CreateLogger<InvoiceProcessor>());

    if (settings.Processing.RealizationOnly == true)
    {
        DateOnly fromDate = settings.Processing.InvoiceFromDate!.Value;
        DateOnly toDate = settings.Processing.RealizationDate!.Value;
        var invoicesEntities = await resolver.FindInvoicesByDateAsync(fromDate, toDate, cts.Token);
        logger.LogInformation("Счетов сохраненных {Count}", invoicesEntities.Count);
        var results1 = await processor.ProcessAsync(invoicesEntities, cts.Token);
        return 0;
    }


    var inputPath = PathResolver.ResolveFromProjectRoot(settings.Processing.InputXlsx);
    var invoices = new ExcelInvoiceReader().Read(inputPath);
    logger.LogInformation("Прочитано строк из Excel: {Count}", invoices.Count);
    var results = await processor.ProcessAsync(invoices,  cts.Token);

    var outputDir = PathResolver.ResolveFromProjectRoot(settings.Processing.OutputDir);
    var reportPath = ReportWriter.WriteReport(outputDir, results);
    logger.LogInformation("Отчет сохранен: {ReportPath}", reportPath);

    var attachments = new List<string> { reportPath };
    attachments.AddRange(results.SelectMany(result => result.Attachments));
    if (settings.Processing.GeneratePdf)
    {
        IPdfProvider pdfProvider = settings.PrintService.Enabled
            ? new OneCPrintPdfProvider(settings.PrintService, map, settings.Processing, loggerFactory.CreateLogger<OneCPrintPdfProvider>())
            : new NoODataPdfProvider(loggerFactory.CreateLogger<NoODataPdfProvider>());
        var pdfFiles = await pdfProvider.GetPdfFilesAsync(results, cts.Token);
        attachments.AddRange(pdfFiles);
        if (pdfProvider is IDisposable disposablePdfProvider)
            disposablePdfProvider.Dispose();
    }

    if (settings.Processing.SendEmail)
    {
        var hasErrors = results.Any(r => r.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));
        var subject = hasErrors ? "Ошибка бота 1С OData" : "Результат работы бота 1С OData";
        var body = BuildEmailBody(results, settings.Processing.GeneratePdf);
        await new EmailSender(settings.Mail).SendAsync(subject, body, attachments, cts.Token);
        logger.LogInformation("Email отправлен на {Email}", settings.Mail.ResultEmail);
    }

    return results.Any(r => r.Status.Equals("Error", StringComparison.OrdinalIgnoreCase)) ? 2 : 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "Критическая ошибка");
    return 1;
}

static void ValidateSettings(AppSettings settings)
{
    var missing = new List<string>();
    if (settings.Processing.RealizationOnly == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.RealizationOnly)}");
    if (settings.Processing.RealizationOnly == true)
    {
        if (settings.Processing.RealizationDate == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.RealizationDate)}");
        if (settings.Processing.InvoiceFromDate == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.InvoiceFromDate)}");
    }
    if (missing.Count > 0)
        throw new InvalidOperationException("Не заполнены настройки обработки: " + string.Join(", ", missing));
}

static string BuildEmailBody(IReadOnlyList<ProcessingResult> results, bool pdfRequested)
{
    var success = results.Count(r => r.Status.Equals("Success", StringComparison.OrdinalIgnoreCase));
    var errors = results.Count(r => r.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));
    var dryRun = results.Count(r => r.Status.Equals("DryRunOk", StringComparison.OrdinalIgnoreCase));

    var sb = new StringBuilder();
    sb.AppendLine("Результат обработки Excel через OData:");
    sb.AppendLine($"Всего обработано строк: {results.Count}");
    sb.AppendLine($"Успешно создано и проведено: {success}");
    if (dryRun > 0) sb.AppendLine($"Проверено в DryRun без создания документов: {dryRun}");
    sb.AppendLine($"Ошибок: {errors}");
    sb.AppendLine();

    foreach (var r in results)
    {
        sb.AppendLine($"Строка {r.ExcelRowNumbers}: {r.Status}");
        if (!string.IsNullOrWhiteSpace(r.InvoiceNumber)) sb.AppendLine($"  Счет: {r.InvoiceNumber} / {r.InvoiceRefKey}");
        if (!string.IsNullOrWhiteSpace(r.RealizationNumber)) sb.AppendLine($"  Реализация: {r.RealizationNumber} / {r.RealizationRefKey}");
        if (!string.IsNullOrWhiteSpace(r.IssuedInvoiceNumber)) sb.AppendLine($"  Счет-фактура: {r.IssuedInvoiceNumber} / {r.IssuedInvoiceRefKey}");
        if (!string.IsNullOrWhiteSpace(r.Error)) sb.AppendLine($"  Ошибка: {r.Error}");
    }

    if (pdfRequested)
    {
        sb.AppendLine();
        sb.AppendLine("Примечание: стандартный OData не формирует печатные формы PDF. Для PDF нужен отдельный HTTP-сервис/расширение 1С или отдельный RPA-шаг печати.");
    }

    return sb.ToString();
}

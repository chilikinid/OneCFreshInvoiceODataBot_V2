using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using OneCFreshInvoiceODataBot.Models;
using OneCFreshInvoiceODataBot.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            ApplicationConfiguration.Initialize();

            var localAppSettingsPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.local.json");
            using var settingsForm = new SettingsForm(localAppSettingsPath);
            Application.Run(settingsForm);
            return 0;
        }

        _TrySetConsoleOutputEncoding();
        using var loggerFactory = _CreateLoggerFactory(useConsole: true);
        return RunProcessingAsync(args, loggerFactory).GetAwaiter().GetResult();
    }

    private static ILoggerFactory _CreateLoggerFactory(bool useConsole)
    {
        return LoggerFactory.Create(builder =>
        {
            if (useConsole)
            {
                builder.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
                });
            }

            builder.SetMinimumLevel(LogLevel.Information);
        });
    }

    private static void _TrySetConsoleOutputEncoding()
    {
        try
        {
            Console.OutputEncoding = Encoding.UTF8;
        }
        catch (IOException)
        {
            Debug.WriteLine("Не удалось установить кодировку консоли.");
        }
    }

    internal static async Task<int> RunProcessingAsync(
        string[] args,
        ILoggerFactory loggerFactory,
        bool registerConsoleCancelKeyPress = true)
    {
        var logger = loggerFactory.CreateLogger("Program");

        try
        {
            var settings = _LoadSettings(args);
            _ValidateSettings(settings);
            var map = await _LoadODataMapAsync(settings.Processing.ODataMapPath);

            Directory.CreateDirectory(PathResolver.ResolveFromProjectRoot(settings.Processing.OutputDir));

            using var cts = new CancellationTokenSource();
            _RegisterConsoleCancellation(cts, registerConsoleCancelKeyPress);

            using var odataClient = new ODataClient(settings.OData);
            return await _RunProcessingCoreAsync(settings, map, odataClient, loggerFactory, logger, cts.Token);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Критическая ошибка");
            return 1;
        }
    }

    private static AppSettings _LoadSettings(string[] args)
    {
        var localAppSettingsPath = args.Length > 0 ? args[0] : "appsettings.local.json";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.example.json", optional: false, reloadOnChange: false)
            .AddJsonFile(localAppSettingsPath, optional: true, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: "ONEC_BOT_")
            .Build();

        var settings = new AppSettings();
        configuration.Bind(settings);
        return settings;
    }
    private static readonly JsonSerializerOptions jsonSerializerOptions = new() { PropertyNameCaseInsensitive = true };

    private static async Task<ODataMap> _LoadODataMapAsync(string mapPathSetting)
    {
        var mapPath = PathResolver.ResolveFromProjectRoot(mapPathSetting);
        if (!File.Exists(mapPath))
        {
            throw new FileNotFoundException(
                $"Не найден файл карты OData: {mapPath}. Скопируйте config/odata-map.example.json в config/odata-map.local.json и настройте поля.",
                mapPath);
        }

        var mapJson = await File.ReadAllTextAsync(mapPath, Encoding.UTF8);
        return JsonSerializer.Deserialize<ODataMap>(
            mapJson,
            jsonSerializerOptions)
            ?? throw new InvalidOperationException("Не удалось прочитать OData map.");
    }

    private static void _RegisterConsoleCancellation(CancellationTokenSource cts, bool enabled)
    {
        if (!enabled)
            return;

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cts.Cancel();
        };
    }

    private static async Task<int> _RunProcessingCoreAsync(
        AppSettings settings,
        ODataMap map,
        ODataClient odataClient,
        ILoggerFactory loggerFactory,
        ILogger logger,
        CancellationToken ct)
    {
        if (settings.Processing.DownloadMetadataOnly)
            return await _DownloadMetadataAsync(settings, odataClient, logger, ct);

        using var counterpartyEnrichmentClient = _CreateCounterpartyEnrichmentClient(map);
        var context = _CreateProcessingContext(settings, map, odataClient, counterpartyEnrichmentClient, loggerFactory);

        return settings.Processing.ProcessExistingInvoices == true
            ? await _ProcessExistingInvoicesAsync(context, logger, ct)
            : await _ProcessExcelAsync(context,  logger, ct);
    }

    private static CounterpartyEnrichmentClient? _CreateCounterpartyEnrichmentClient(ODataMap map)
    {
        return map.CounterpartyEnrichment.Enabled
            ? new CounterpartyEnrichmentClient(map.CounterpartyEnrichment)
            : null;
    }

    private static ProcessingContext _CreateProcessingContext(
        AppSettings settings,
        ODataMap map,
        ODataClient odataClient,
        CounterpartyEnrichmentClient? counterpartyEnrichmentClient,
        ILoggerFactory loggerFactory)
    {
        var resolver = new ReferenceResolver(odataClient, map, counterpartyEnrichmentClient);
        var payloadFactory = new PayloadFactory(map);
        var processor = new InvoiceProcessor(
            odataClient,
            resolver,
            payloadFactory,
            map,
            settings.Processing,
            loggerFactory.CreateLogger<InvoiceProcessor>());

        return new ProcessingContext(settings, map, resolver, processor);
    }

    private static async Task<int> _DownloadMetadataAsync(
        AppSettings settings,
        ODataClient odataClient,
        ILogger logger,
        CancellationToken ct)
    {
        var metadata = await odataClient.DownloadMetadataAsync(ct);
        var metadataPath = Path.Combine(PathResolver.ResolveFromProjectRoot(settings.Processing.OutputDir), "metadata.xml");
        await File.WriteAllTextAsync(metadataPath, metadata, Encoding.UTF8, ct);
        logger.LogInformation("$metadata сохранен: {Path}", metadataPath);
        return 0;
    }

    private static async Task<int> _ProcessExistingInvoicesAsync(
        ProcessingContext context,
        ILogger logger,
        CancellationToken ct)
    {
        DateOnly fromDate = context.Settings.Processing.InvoiceFromDate!.Value;
        DateOnly toDate = context.Settings.Processing.RealizationDate!.Value;
        var invoiceEntities = await context.Resolver.FindInvoicesByDateAsync(fromDate, toDate, ct);
        logger.LogInformation("Счетов сохраненных {Count}", invoiceEntities.Count);
        await context.Processor.ProcessAsync(invoiceEntities, ct);
        return 0;
    }

    private static async Task<int> _ProcessExcelAsync(
        ProcessingContext context,
                ILogger logger,
        CancellationToken ct)
    {
        var inputPath = PathResolver.ResolveFromProjectRoot(context.Settings.Processing.InputXlsx);
        var invoices = new ExcelInvoiceReader().Read(inputPath);
        logger.LogInformation("Прочитано строк из Excel: {Count}", invoices.Count);

        var results = await context.Processor.ProcessByExcelAsync(invoices, ct);
        var attachments = _CreateAttachments(context, logger, results);
        await _SendEmailIfNeededAsync(context.Settings, attachments, results, logger, ct);

        return _HasErrors(results) ? 2 : 0;
    }

    private static List<string> _CreateAttachments(
        ProcessingContext context,
        ILogger logger,
        IReadOnlyList<ProcessingResult> results)
    {
        var outputDir = PathResolver.ResolveFromProjectRoot(context.Settings.Processing.OutputDir);
        var reportPath = ReportWriter.WriteReport(outputDir, results);
        logger.LogInformation("Отчет сохранен: {ReportPath}", reportPath);

        var attachments = new List<string> { reportPath };
        attachments.AddRange(results.SelectMany(result => result.Attachments));
        return attachments;
    }

    private static async Task _SendEmailIfNeededAsync(
        AppSettings settings,
        IReadOnlyList<string> attachments,
        IReadOnlyList<ProcessingResult> results,
        ILogger logger,
        CancellationToken ct)
    {
        if (!settings.Processing.SendEmail)
            return;

        var subject = _HasErrors(results)
            ? "Ошибка бота 1С OData"
            : "Результат работы бота 1С OData";
        var body = _BuildEmailBody(results);
        await new EmailSender(settings.Mail).SendAsync(subject, body, attachments, ct);
        logger.LogInformation("Email отправлен на {Email}", settings.Mail.ResultEmail);
    }

    private static bool _HasErrors(IEnumerable<ProcessingResult> results)
    {
        return results.Any(r => r.Status.Equals("Error", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record ProcessingContext(
        AppSettings Settings,
        ODataMap Map,
        ReferenceResolver Resolver,
        InvoiceProcessor Processor);

    private static void _ValidateSettings(AppSettings settings)
    {
        var missing = new List<string>();
        if (settings.Processing.ProcessExistingInvoices == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.ProcessExistingInvoices)}");
        if (settings.Processing.ProcessExistingInvoices == true)
        {
            if (settings.Processing.RealizationDate == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.RealizationDate)}");
            if (settings.Processing.InvoiceFromDate == null) missing.Add($"{nameof(settings.Processing)}.{nameof(settings.Processing.InvoiceFromDate)}");
        }
        if (missing.Count > 0)
            throw new InvalidOperationException("Не заполнены настройки обработки: " + string.Join(", ", missing));
    }

    private static string _BuildEmailBody(IReadOnlyList<ProcessingResult> results)
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

        return sb.ToString();
    }
}

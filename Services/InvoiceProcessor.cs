using Microsoft.Extensions.Logging;

using OneCFreshInvoiceODataBot.Models;

using System.Diagnostics;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class InvoiceProcessor
{
    private readonly ODataClient _client;
    private readonly ReferenceResolver _resolver;
    private readonly PayloadFactory _payloadFactory;
    private readonly ODataMap _map;
    private readonly ProcessingSettings _settings;
    private readonly ILogger<InvoiceProcessor> _logger;
    private readonly DocxPrintFormGenerator _printFormGenerator;
    private readonly string _outputDir;

    public InvoiceProcessor(
        ODataClient client,
        ReferenceResolver resolver,
        PayloadFactory payloadFactory,
        ODataMap map,
        ProcessingSettings settings,
        ILogger<InvoiceProcessor> logger,
        DocxPrintFormGenerator? printFormGenerator = null,
        string? outputDir = null)
    {
        _client = client;
        _resolver = resolver;
        _payloadFactory = payloadFactory;
        _map = map;
        _settings = settings;
        _logger = logger;
        _printFormGenerator = printFormGenerator ?? new DocxPrintFormGenerator();
        _outputDir = outputDir ?? PathResolver.ResolveFromProjectRoot(settings.OutputDir);
    }

    public async Task<IReadOnlyList<ProcessingResult>> ProcessAsync(IReadOnlyList<InvoiceData> invoices, CancellationToken ct)
    {
        var results = new List<ProcessingResult>();
        Dictionary<string, ODataEntity> nomenclatureCache = []; // по имени
        Dictionary<string, ODataEntity> accountCache = []; // по GUID номенклатуры

        var organization = await _resolver.FindOrganizationAsync(_settings.INN, ct);

        foreach (var invoice in invoices)
        {
            var result = new ProcessingResult
            {
                ExcelRowNumbers = invoice.ExcelRowNumbers,
                CounterpartyInn = invoice.CounterpartyInn,
                Number = invoice.Number,
                NomenclatureNames = invoice.NomenclatureNames
            };
            results.Add(result);

            try
            {
                _logger.LogInformation("Обработка {HumanKey}", invoice.HumanKey);


                var counterparty = await _resolver.ResolveCounterpartyByInnAsync(invoice.CounterpartyInn, invoice.CounterpartyName, ct);

                ODataEntity? agreement = null;
                if (!string.IsNullOrWhiteSpace(invoice.AgreementName))
                {
                    agreement = await _resolver.FindAgreementAsync(invoice.AgreementName, counterparty.RefKey, ct);
                }
                foreach (var nomenclatureName in invoice.InvoiceItems.Select(m => m.NomenclatureName).Distinct())
                {
                    if (!nomenclatureCache.ContainsKey(nomenclatureName))
                    {
                        var itemEntity = await _resolver.FindNomenclatureAsync(nomenclatureName, ct);
                        nomenclatureCache[nomenclatureName] = itemEntity;
                    }
                }

                if (_settings.DryRun)
                {
                    result.Status = "DryRunOk";
                    continue;
                }

                var bankAccount = await _resolver.ResolveBankAccountAsync(invoice.BankAccount, organization.RefKey, ct);
                var bankAccountKey = bankAccount.RefKey;
                var invoicePayload = _payloadFactory.BuildInvoicePayload(invoice, counterparty, agreement, nomenclatureCache, bankAccountKey);
                var createdInvoice = await _client.CreateAsync(_map.Invoice.EntitySet, invoicePayload, ct);
                result.InvoiceRefKey = ReferenceResolver.GetRequiredString(createdInvoice, _map.Invoice.KeyField, "созданный счет");
                result.InvoiceNumber = TryGetString(createdInvoice, _map.Invoice.NumberField);

                await _client.PostDocumentAsync(_map.Invoice.EntitySet, result.InvoiceRefKey, ct);
                if (_settings.GeneratePrintForms)
                {
                    result.Attachments.Add(await _printFormGenerator.CreateInvoiceAsync(
                        invoice,
                        organization,
                        counterparty,
                        agreement,
                        bankAccount,
                        result.InvoiceNumber ?? invoice.Number,
                        _outputDir,
                        ct));
                }

                if (!_settings.CreateInvoicesOnly)
                {
                    await LoadAccountsForNomenclatureAsync(nomenclatureCache.Values, accountCache, ct);

                    var realizationPayload = _payloadFactory.BuildRealizationPayload(invoice, counterparty, agreement, nomenclatureCache, accountCache, result.InvoiceRefKey);
                    var createdRealization = await _client.CreateAsync(_map.Realization.EntitySet, realizationPayload, ct);
                    result.RealizationRefKey = ReferenceResolver.GetRequiredString(createdRealization, _map.Realization.KeyField, "созданная реализация");
                    result.RealizationNumber = TryGetString(createdRealization, _map.Realization.NumberField);

                    await _client.PostDocumentAsync(_map.Realization.EntitySet, result.RealizationRefKey, ct);
                    if (_settings.GeneratePrintForms)
                    {
                        result.Attachments.Add(await _printFormGenerator.CreateRealizationAsync(
                            GetRealizationPrintTitle(realizationPayload),
                            invoice,
                            organization,
                            counterparty,
                            agreement,
                            result.RealizationNumber,
                            _outputDir,
                            ct));
                    }

                    await CreateIssuedInvoiceIfNeededAsync(realizationPayload, createdRealization, result, ct);
                }

                result.Status = "Success";
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Error = ex.Message;
                _logger.LogError(ex, "Ошибка при обработке строк Excel {Row}", invoice.ExcelRowNumbers);

                if (_settings.StopOnFirstError)
                    break;
            }
        }

        return results;
    }

    internal async Task<IReadOnlyList<ProcessingResult>> ProcessAsync(IReadOnlyList<ODataEntity> invoices, CancellationToken ct)
    {
        var results = new List<ProcessingResult>();

        Dictionary<string, ODataEntity> nomenclatureCache = []; // по GUID
        Dictionary<string, ODataEntity> accountCache = []; // по GUID номенклатуры
        DateOnly realizationDate = _settings.RealizationDate!.Value;

        HashSet<string> nomenclatureKeys = [];
        foreach (var invoice in invoices)
        {
            var goodsTable = invoice.Raw[_map.Invoice.GoodsTablePart] as List<Object>;
            if (goodsTable != null)
            {
                for (int i = 0; i < goodsTable.Count; i++)
                {
                    var dataItem = goodsTable[i] as Dictionary<string, object?>;
                    if (dataItem == null) continue;
                    var nomenclatureKey = TryGetString(dataItem, _map.Invoice.ItemKeyField)
                        ?? TryGetString(dataItem, _map.Invoice.NomenclatureField)
                        ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(nomenclatureKey))
                        nomenclatureKeys.Add(nomenclatureKey);
                }
            }
        }

        foreach (var nomenclatureKey in nomenclatureKeys)
        {
            if (!nomenclatureCache.ContainsKey(nomenclatureKey))
            {
                var itemEntity = await _resolver.FindNomenclatureByKeyAsync(nomenclatureKey, ct);
                nomenclatureCache[nomenclatureKey] = itemEntity;
            }
        }
        await LoadAccountsForNomenclatureAsync(nomenclatureCache.Values, accountCache, ct);

        foreach (var invoice in invoices)
        {
            var result = new ProcessingResult
            {
                InvoceRefKey = invoice.RefKey
            };
            results.Add(result);

            try
            {
                _logger.LogInformation("Обработка {HumanKey}", invoice.RefKey);

                if (_settings.DryRun)
                {
                    result.Status = "DryRunOk";
                    continue;
                }

                result.InvoiceNumber = invoice.Raw[_map.Invoice.NumberField]?.ToString();


                var realizationPayload = _payloadFactory.BuildRealizationPayload(invoice, realizationDate, accountCache);
                var createdRealization = await _client.CreateAsync(_map.Realization.EntitySet, realizationPayload, ct);
                result.RealizationRefKey = ReferenceResolver.GetRequiredString(createdRealization, _map.Realization.KeyField, "созданная реализация");
                result.RealizationNumber = TryGetString(createdRealization, _map.Realization.NumberField);

                await _client.PostDocumentAsync(_map.Realization.EntitySet, result.RealizationRefKey, ct);
                await CreateIssuedInvoiceIfNeededAsync(realizationPayload, createdRealization, result, ct);

                result.Status = "Success";
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Error = ex.Message;
                _logger.LogError(ex, "Ошибка при обработке {Row}", invoice.RefKey);

                if (_settings.StopOnFirstError)
                    break;
            }
        }

        return results;
    }

    private async Task CreateIssuedInvoiceIfNeededAsync(
        Dictionary<string, object?> realizationPayload,
        Dictionary<string, object?> createdRealization,
        ProcessingResult result,
        CancellationToken ct)
    {
        var payload = _payloadFactory.BuildIssuedInvoicePayload(
            realizationPayload,
            result.RealizationRefKey!,
            result.RealizationNumber,
            createdRealization);
        if (payload is null)
            return;

        var createdIssuedInvoice = await _client.CreateAsync(_map.IssuedInvoice.EntitySet, payload, ct);
        result.IssuedInvoiceRefKey = ReferenceResolver.GetRequiredString(
            createdIssuedInvoice,
            _map.IssuedInvoice.KeyField,
            "созданный счет-фактура");
        result.IssuedInvoiceNumber = TryGetString(createdIssuedInvoice, _map.IssuedInvoice.NumberField);

        await _client.PostDocumentAsync(_map.IssuedInvoice.EntitySet, result.IssuedInvoiceRefKey, ct);
    }

    private string GetRealizationPrintTitle(Dictionary<string, object?> realizationPayload)
    {
        if (!string.IsNullOrWhiteSpace(_map.Realization.UseUpdField)
            && realizationPayload.ContainsKey(_map.Realization.UseUpdField))
        {
            return "УПД";
        }

        return "Акт выполненных работ";
    }

    private static string? TryGetString(Dictionary<string, object?> row, string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        return row.TryGetValue(field, out var value) ? Convert.ToString(value) : null;
    }

    private async Task LoadAccountsForNomenclatureAsync(IEnumerable<ODataEntity> nomenclatureItems, Dictionary<string, ODataEntity> accountCache, CancellationToken ct)
    {
        if (!_map.AccountLookup.Enabled)
            return;

        var items = nomenclatureItems
            .Where(item => !string.IsNullOrWhiteSpace(item.RefKey))
            .DistinctBy(item => item.RefKey, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var nomenclature in items)
        {
            var nomenclatureKey = nomenclature.RefKey;
            if (accountCache.ContainsKey(nomenclatureKey))
                continue;

            var accounts = await _resolver.FindAccountsByNomenclatureKeyAsync(nomenclature, ct, required: false);
            if (accounts is not null)
                accountCache[nomenclatureKey] = accounts;
        }

        foreach (var nomenclature in items.Where(item => !accountCache.ContainsKey(item.RefKey)))
        {
            var accounts = await _resolver.FindAccountsByNomenclatureKeyAsync(nomenclature, ct);
            if (accounts is not null)
                accountCache[nomenclature.RefKey] = accounts;
        }
    }

}

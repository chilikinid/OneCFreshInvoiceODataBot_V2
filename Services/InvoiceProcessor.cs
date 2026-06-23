using Microsoft.Extensions.Logging;

using OneCFreshInvoiceODataBot.Models;

using System.Linq;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class InvoiceProcessorOptions
{
    public DocxPrintFormGenerator? PrintFormGenerator { get; init; }
    public string? OutputDir { get; init; }
}

public sealed class InvoiceProcessor(
    ODataClient client,
    ReferenceResolver resolver,
    PayloadFactory payloadFactory,
    ODataMap map,
    ProcessingSettings settings,
    ILogger<InvoiceProcessor> logger,
    InvoiceProcessorOptions? options = null)
{
    private readonly string _outputDir = options?.OutputDir ?? PathResolver.ResolveFromProjectRoot(settings.OutputDir);


    public async Task<IReadOnlyList<ProcessingResult>> ProcessByExcelAsync(IReadOnlyList<InvoiceData> invoices, CancellationToken ct)
    {
        var results = new List<ProcessingResult>();
        Dictionary<string, ODataEntity> nomenclatureCache = []; // по имени
        Dictionary<string, ODataEntity> accountCache = []; // по GUID номенклатуры

        var organization = await resolver.FindOrganizationAsync(settings.INN, ct);

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
                logger.LogInformation("Обработка {HumanKey}", invoice.HumanKey);
                await _ProcessSingleInvoiceByExcelAsync(invoice, organization, result, nomenclatureCache, accountCache, ct);
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Error = ex.Message;
                logger.LogError(ex, "Ошибка при обработке строк Excel {_Row}", invoice.ExcelRowNumbers);

                if (settings.StopOnFirstError)
                    break;
            }
        }

        return results;
    }

    private async Task _ProcessSingleInvoiceByExcelAsync(
        InvoiceData invoice,
        ODataEntity organization,
        ProcessingResult result,
        Dictionary<string, ODataEntity> nomenclatureCache,
        Dictionary<string, ODataEntity> accountCache,
        CancellationToken ct)
    {
        var counterparty = await resolver.ResolveCounterpartyByInnAsync(invoice.CounterpartyInn, invoice.CounterpartyName, ct);

        ODataEntity? agreement = null;
        if (!string.IsNullOrWhiteSpace(invoice.AgreementName))
        {
            agreement = await resolver.FindAgreementAsync(invoice.AgreementName, counterparty.RefKey, ct);
        }

        await _CacheNomenclatureItemsAsync(invoice.InvoiceItems.Select(i => i.NomenclatureName).Distinct(), nomenclatureCache, ct);

        if (settings.DryRun)
        {
            result.Status = "DryRunOk";
            return;
        }

        var bankAccount = await resolver.ResolveBankAccountAsync(invoice.BankAccount, organization.RefKey, ct);
        var bankAccountKey = bankAccount.RefKey;
        var invoicePayload = payloadFactory.BuildInvoicePayload(invoice, counterparty, agreement, nomenclatureCache, bankAccountKey);
        var createdInvoice = await client.CreateAsync(map.Invoice.EntitySet, invoicePayload, ct);
        result.InvoiceRefKey = ReferenceResolver.GetRequiredString(createdInvoice, map.Invoice.KeyField, "созданный счет");
        result.InvoiceNumber = _TryGetString(createdInvoice, map.Invoice.NumberField);

        await client.PostDocumentAsync(map.Invoice.EntitySet, result.InvoiceRefKey, ct);

        if (settings.GeneratePrintForms)
        {
            result.Attachments.Add(await DocxPrintFormGenerator.CreateInvoiceAsync(
                new InvoicePrintFormRequest(
                    invoice,
                    organization,
                    counterparty,
                    agreement,
                    bankAccount,
                    result.InvoiceNumber ?? invoice.Number,
                    _outputDir),
                ct));
        }

        if (!settings.CreateInvoicesOnly)
        {
            await _LoadAccountsForNomenclatureAsync(nomenclatureCache.Values, accountCache, ct);

            var realizationPayload = payloadFactory.BuildRealizationPayload(invoice, counterparty, agreement, nomenclatureCache, accountCache, result.InvoiceRefKey);
            var createdRealization = await client.CreateAsync(map.Realization.EntitySet, realizationPayload, ct);
            result.RealizationRefKey = ReferenceResolver.GetRequiredString(createdRealization, map.Realization.KeyField, "созданная реализация");
            result.RealizationNumber = _TryGetString(createdRealization, map.Realization.NumberField);

            await client.PostDocumentAsync(map.Realization.EntitySet, result.RealizationRefKey, ct);

            if (settings.GeneratePrintForms)
            {
                result.Attachments.Add(await DocxPrintFormGenerator.CreateRealizationAsync(
                    new RealizationPrintFormRequest(
                        _GetRealizationPrintTitle(realizationPayload),
                        invoice,
                        organization,
                        counterparty,
                        agreement,
                        result.RealizationNumber,
                        _outputDir),
                    ct));
            }

            await _CreateIssuedInvoiceIfNeededAsync(realizationPayload, createdRealization, result, ct);
        }

        result.Status = "Success";
    }

    private async Task _CacheNomenclatureItemsAsync(IEnumerable<string> nomenclatureNames, Dictionary<string, ODataEntity> nomenclatureCache, CancellationToken ct)
    {
        var notCachedNomenclatures = nomenclatureNames.Where(n => !nomenclatureCache.ContainsKey(n)).ToList();
        foreach (var nomenclatureName in notCachedNomenclatures)
        {
            // Загружаем из OData (ReferenceResolver уже кэширует)
            var itemEntity = await resolver.FindNomenclatureAsync(nomenclatureName, ct);
            nomenclatureCache[nomenclatureName] = itemEntity;
        }
    }

    internal async Task<IReadOnlyList<ProcessingResult>> ProcessExistingInvoicesAsync(IReadOnlyList<ODataEntity> invoices, CancellationToken ct)
    {
        var results = new List<ProcessingResult>();

        Dictionary<string, ODataEntity> nomenclatureCache = []; // по GUID
        Dictionary<string, ODataEntity> accountCache = []; // по GUID номенклатуры
        DateOnly realizationDate = settings.RealizationDate!.Value;

        await _PrepareNomenclatureAndAccountCachesAsync(invoices, nomenclatureCache, accountCache, ct);

        foreach (var invoice in invoices)
        {
            var result = new ProcessingResult
            {
                InvoiceRefKey = invoice.RefKey
            };
            results.Add(result);

            try
            {
                logger.LogInformation("Обработка {HumanKey}", invoice.RefKey);
                await _ProcessSingleExistingInvoiceAsync(invoice, realizationDate, result, accountCache, ct);
            }
            catch (Exception ex)
            {
                result.Status = "Error";
                result.Error = ex.Message;
                logger.LogError(ex, "Ошибка при обработке {_Row}", invoice.RefKey);

                if (settings.StopOnFirstError)
                    break;
            }
        }

        return results;
    }

    private async Task _PrepareNomenclatureAndAccountCachesAsync(
        IReadOnlyList<ODataEntity> invoices,
        Dictionary<string, ODataEntity> nomenclatureCache,
        Dictionary<string, ODataEntity> accountCache,
        CancellationToken ct)
    {
        var nomenclatureKeys = _CollectNomenclatureKeys(invoices);

        foreach (var nomenclatureKey in nomenclatureKeys)
        {
            if (nomenclatureCache.ContainsKey(nomenclatureKey)) 
                continue;

            var itemEntity = await resolver.FindNomenclatureByKeyAsync(nomenclatureKey, ct);
            nomenclatureCache[nomenclatureKey] = itemEntity;
        }

        await _LoadAccountsForNomenclatureAsync(nomenclatureCache.Values, accountCache, ct);
    }

    private HashSet<string> _CollectNomenclatureKeys(IReadOnlyList<ODataEntity> invoices)
    {
        var nomenclatureKeys = new HashSet<string>();

        foreach (var invoice in invoices)
        {
            if (invoice.Raw[map.Invoice.GoodsTablePart] is List<Object> goodsTable)
            {
                for (int i = 0; i < goodsTable.Count; i++)
                {
                    if (goodsTable[i] is not Dictionary<string, object?> dataItem) continue;
                    var nomenclatureKey = _TryGetString(dataItem, map.Invoice.ItemKeyField)
                        ?? _TryGetString(dataItem, map.Invoice.NomenclatureField)
                        ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(nomenclatureKey))
                        nomenclatureKeys.Add(nomenclatureKey);
                }
            }
        }

        return nomenclatureKeys;
    }

    private async Task _ProcessSingleExistingInvoiceAsync(
        ODataEntity invoice,
        DateOnly realizationDate,
        ProcessingResult result,
        Dictionary<string, ODataEntity> accountCache,
        CancellationToken ct)
    {
        if (settings.DryRun)
        {
            result.Status = "DryRunOk";
            return;
        }

        result.InvoiceNumber = invoice.Raw[map.Invoice.NumberField]?.ToString();

        var realizationPayload = payloadFactory.BuildRealizationPayload(invoice, realizationDate, accountCache);
        var createdRealization = await client.CreateAsync(map.Realization.EntitySet, realizationPayload, ct);
        result.RealizationRefKey = ReferenceResolver.GetRequiredString(createdRealization, map.Realization.KeyField, "созданная реализация");
        result.RealizationNumber = _TryGetString(createdRealization, map.Realization.NumberField);

        await client.PostDocumentAsync(map.Realization.EntitySet, result.RealizationRefKey, ct);
        await _CreateIssuedInvoiceIfNeededAsync(realizationPayload, createdRealization, result, ct);

        result.Status = "Success";
    }

    private async Task _CreateIssuedInvoiceIfNeededAsync(
        Dictionary<string, object?> realizationPayload,
        Dictionary<string, object?> createdRealization,
        ProcessingResult result,
        CancellationToken ct)
    {
        var payload = payloadFactory.BuildIssuedInvoicePayload(
            realizationPayload,
            result.RealizationRefKey!,
            result.RealizationNumber,
            createdRealization);
        if (payload is null)
            return;

        var createdIssuedInvoice = await client.CreateAsync(map.IssuedInvoice.EntitySet, payload, ct);
        result.IssuedInvoiceRefKey = ReferenceResolver.GetRequiredString(
            createdIssuedInvoice,
            map.IssuedInvoice.KeyField,
            "созданный счет-фактура");
        result.IssuedInvoiceNumber = _TryGetString(createdIssuedInvoice, map.IssuedInvoice.NumberField);

        await client.PostDocumentAsync(map.IssuedInvoice.EntitySet, result.IssuedInvoiceRefKey, ct);
    }

    private string _GetRealizationPrintTitle(Dictionary<string, object?> realizationPayload)
    {
        if (!string.IsNullOrWhiteSpace(map.Realization.UseUpdField)
            && realizationPayload.ContainsKey(map.Realization.UseUpdField))
        {
            return "УПД";
        }

        return "Акт выполненных работ";
    }

    private static string? _TryGetString(Dictionary<string, object?> row, string field)
    {
        if (string.IsNullOrWhiteSpace(field)) return null;
        return row.TryGetValue(field, out var value) ? Convert.ToString(value) : null;
    }

    private async Task _LoadAccountsForNomenclatureAsync(IEnumerable<ODataEntity> nomenclatureItems, Dictionary<string, ODataEntity> accountCache, CancellationToken ct)
    {
        if (!map.AccountLookup.Enabled)
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

            var accounts = await resolver.FindAccountsByNomenclatureKeyAsync(nomenclature, ct, required: false);
            if (accounts is not null)
            {
                accountCache[nomenclatureKey] = accounts;
            }
        }

        foreach (var nomenclature in items.Where(item => !accountCache.ContainsKey(item.RefKey)))
        {
            var accounts = await resolver.FindAccountsByNomenclatureKeyAsync(nomenclature, ct);
            if (accounts is not null)
            {
                accountCache[nomenclature.RefKey] = accounts;
            }
        }
    }

}

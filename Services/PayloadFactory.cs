using OneCFreshInvoiceODataBot.Models;

using System.Globalization;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class PayloadFactory(ODataMap map)
{
    public Dictionary<string, object?> BuildInvoicePayload(
        InvoiceData invoice,
        ODataEntity counterparty,
        ODataEntity? agreement,
        Dictionary<string, ODataEntity> nomenclatureCache,
        string bankAccountKey)
    {
        var doc = map.Invoice;
        var payload = _BuildCommonDocumentPayload(doc, invoice, counterparty, agreement, bankAccountKey: bankAccountKey);
        List<Object> targetRows = [];
        for (int i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            InvoiceItemData? row = invoice.InvoiceItems[i];
            var item = nomenclatureCache[row.NomenclatureName];
            targetRows.Add(_BuildGoodsLine(doc, row, item, lineNumber: i + 1, accounts: null));
        }
        payload[doc.GoodsTablePart] = targetRows.ToArray();
        _ApplyVatDocumentFields(payload, doc, invoice.InvoiceItems.Select(row => row.VatRate));
        return payload;
    }
    public Dictionary<string, object?>? BuildIssuedInvoicePayload(
        Dictionary<string, object?> realizationPayload,
        string realizationRefKey,
        string? realizationNumber,
        Dictionary<string, object?>? createdRealization = null)
    {
        var doc = map.IssuedInvoice;
        if (!doc.Enabled || !_HasVat(realizationPayload))
            return null;

        var realization = map.Realization;
        var date = _GetRequiredString(realizationPayload, realization.DateField, "дата реализации");
        var amount = _SumRealizationLines(realizationPayload, realization.AmountField);
        var vatAmount = _SumRealizationLines(realizationPayload, realization.VatAmountField);
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Date"] = date,
            [realization.OrganizationKeyField] = _GetRequiredString(realizationPayload, realization.OrganizationKeyField, "организация реализации"),
            [realization.CounterpartyKeyField] = _GetRequiredString(realizationPayload, realization.CounterpartyKeyField, "контрагент реализации"),
            [doc.DocumentKindField] = doc.DocumentKindValue,
            [doc.BasisDocumentField] = realizationRefKey,
            [doc.BasisDocumentTypeField] = doc.BasisDocumentTypeValue,
            [doc.OperationCodeField] = doc.OperationCodeValue,
            [doc.IssuedField] = true,
            [doc.IssueDateField] = date,
            [doc.IssueMethodCodeField] = doc.IssueMethodCodeValue,
            [doc.WithoutVatField] = false,
            [doc.AmountField] = amount,
            [doc.VatAmountField] = vatAmount,
            [doc.BasisTablePart] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LineNumber"] = "1",
                    [doc.BasisDocumentField] = realizationRefKey,
                    [doc.BasisDocumentTypeField] = doc.BasisDocumentTypeValue,
                }
            },
            [doc.ShipmentTablePart] = new object[]
            {
                new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["LineNumber"] = "1",
                    ["НомераСтрок"] = string.Empty,
                    ["НаименованиеДокумента"] = "Акт",
                    ["НомерДокумента"] = realizationNumber ?? string.Empty,
                    ["ДатаДокумента"] = date,
                }
            },
        };

        if (realizationPayload.TryGetValue(realization.AgreementKeyField, out var agreementKey)
            && !string.IsNullOrWhiteSpace(Convert.ToString(agreementKey)))
        {
            payload[realization.AgreementKeyField] = agreementKey;
        }

        _CopyIfPresent(createdRealization, payload, "ВалютаДокумента_Key");
        _CopyIfPresent(createdRealization, payload, "Ответственный_Key");

        return payload;
    }

    public Dictionary<string, object?> BuildRealizationPayload(
        InvoiceData invoice,
        ODataEntity counterparty,
        ODataEntity? agreement,
        Dictionary<string, ODataEntity> nomenclatureCache,
        Dictionary<string, ODataEntity> accountCache,
        string invoiceRefKey)
    {
        var doc = map.Realization;
        var payload = _BuildCommonDocumentPayload(doc, invoice, counterparty, agreement, false);

        if (!string.IsNullOrWhiteSpace(doc.InvoiceKeyField))
            payload[doc.InvoiceKeyField] = invoiceRefKey; // 99b68878-58f4-11f1-8de4-fa163e7b1300

        if (!string.IsNullOrWhiteSpace(doc.OperationKindField) && !string.IsNullOrWhiteSpace(doc.OperationKindValue))
            payload[doc.OperationKindField] = doc.OperationKindValue;

        List<Object> targetRows = [];
        for (int i = 0; i < invoice.InvoiceItems.Count; i++)
        {
            var dataItem = invoice.InvoiceItems[i];
            var item = nomenclatureCache[dataItem.NomenclatureName];
            accountCache.TryGetValue(item.RefKey, out var accounts);
            targetRows.Add(_BuildGoodsLine(doc, dataItem, item, lineNumber: i + 1, accounts));
        }
        payload["Услуги"] = targetRows.ToArray();
        _ApplyVatDocumentFields(payload, doc, invoice.InvoiceItems.Select(row => row.VatRate));
        _ApplyUpdDocumentFields(payload, doc, invoice.InvoiceItems.Select(row => row.VatRate));
        return payload;
    }

    public Dictionary<string, object?> BuildRealizationPayload(
        ODataEntity invoiceEntity,
        DateOnly realizationDate,
        Dictionary<string, ODataEntity> accountCache)
    {
        var doc = map.Realization;
        string number = invoiceEntity.Raw[doc.NumberField]?.ToString() ?? string.Empty;
        var payload = _BuildCommonDocumentPayload(doc,
                                                number,
                                                realizationDate.ToDateTime(TimeOnly.MinValue),
                                                invoiceEntity.Raw[doc.AgreementKeyField]?.ToString(),
                                                invoiceEntity.Raw[doc.CounterpartyKeyField]?.ToString(),
                                                invoiceEntity.Raw[doc.OrganizationKeyField]?.ToString()
                                                , false);

        if (!string.IsNullOrWhiteSpace(doc.InvoiceKeyField))
            payload[doc.InvoiceKeyField] = invoiceEntity.RefKey; // 99b68878-58f4-11f1-8de4-fa163e7b1300

        if (!string.IsNullOrWhiteSpace(doc.OperationKindField) && !string.IsNullOrWhiteSpace(doc.OperationKindValue))
            payload[doc.OperationKindField] = doc.OperationKindValue;

        List<Object> targetRows = [];
        if (invoiceEntity.Raw[doc.GoodsTablePart] is List<Object> goodsTable)
        {
            var vatRates = new List<string>();
            for (int i = 0; i < goodsTable.Count; i++)
            {
                if (goodsTable[i] is not Dictionary<string, object?> dataItem) continue;
                vatRates.Add(_GetFirstString(dataItem, doc.VatRateField, map.Invoice.VatRateField));
                var nomenclatureKey = _GetNomenclatureKey(doc, dataItem);
                accountCache.TryGetValue(nomenclatureKey, out var accounts);
                targetRows.Add(_BuildGoodsLine(doc, dataItem, accounts));
            }
            payload[doc.OperationKindValue] = targetRows.ToArray();
            _ApplyVatDocumentFields(payload, doc, vatRates);
            _ApplyUpdDocumentFields(payload, doc, vatRates);
        }


        return payload;
    }

    private Dictionary<string, object?> _BuildCommonDocumentPayload(DocumentMap doc,
                                                                   string number,
                                                                   DateTime documentDate,
                                                                   string? agreementRefKey,
                                                                   string? counterpartyRefKey,
                                                                   string? organizationRefKey,
                                                                   bool setNumber = true)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [doc.DateField] = _ToODataDateTime(documentDate),
            [doc.CounterpartyKeyField] = counterpartyRefKey,
            [doc.AgreementKeyField] = agreementRefKey,
            [doc.OrganizationKeyField] = organizationRefKey,
            [doc.AmountIncludeVatField] = true,
        };

        if (!string.IsNullOrWhiteSpace(doc.RecipientOrganizationKeyField))
            payload[doc.RecipientOrganizationKeyField] = organizationRefKey;
        if (setNumber)
        {
            payload[doc.NumberField] = number;
        }

        if (!string.IsNullOrWhiteSpace(doc.WarehouseKeyField) && !string.IsNullOrWhiteSpace(map.DefaultWarehouseKey))
            payload[doc.WarehouseKeyField] = map.DefaultWarehouseKey;

        if (!string.IsNullOrWhiteSpace(doc.BankAccountKeyField) && !string.IsNullOrWhiteSpace(map.DefaultBankAccountKey))
            payload[doc.BankAccountKeyField] = map.DefaultBankAccountKey;

        return payload;
    }

    private Dictionary<string, object?> _BuildCommonDocumentPayload(
        DocumentMap doc,
        InvoiceData row,
        ODataEntity counterparty,
        ODataEntity? agreement,
        bool setNumber = true,
        string? bankAccountKey = null)
    {
        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [doc.DateField] = _ToODataDateTime(row.DocumentDate),
            [doc.CounterpartyKeyField] = counterparty.RefKey,
            [doc.AmountIncludeVatField] = true,
        };
        if (agreement is not null) payload[doc.AgreementKeyField] = agreement.RefKey;
        if (setNumber) payload[doc.NumberField] = row.Number;

        if (!string.IsNullOrWhiteSpace(doc.OrganizationKeyField) && !string.IsNullOrWhiteSpace(map.DefaultOrganizationKey))
            payload[doc.OrganizationKeyField] = map.DefaultOrganizationKey;

        if (!string.IsNullOrWhiteSpace(doc.RecipientOrganizationKeyField) && !string.IsNullOrWhiteSpace(map.DefaultOrganizationKey))
            payload[doc.RecipientOrganizationKeyField] = map.DefaultOrganizationKey;

        if (!string.IsNullOrWhiteSpace(doc.WarehouseKeyField) && !string.IsNullOrWhiteSpace(map.DefaultWarehouseKey))
            payload[doc.WarehouseKeyField] = map.DefaultWarehouseKey;

        var resolvedBankAccountKey = string.IsNullOrWhiteSpace(bankAccountKey) ? map.DefaultBankAccountKey : bankAccountKey;
        if (!string.IsNullOrWhiteSpace(doc.BankAccountKeyField) && !string.IsNullOrWhiteSpace(resolvedBankAccountKey))
            payload[doc.BankAccountKeyField] = resolvedBankAccountKey;

        _ApplyDefaultFields(payload, doc);
        _ApplyOrganizationFieldMappings(payload, doc);

        return payload;
    }

    private static void _ApplyDefaultFields(Dictionary<string, object?> payload, DocumentMap doc)
    {
        foreach (var (field, value) in doc.DefaultFields)
        {
            if (!string.IsNullOrWhiteSpace(field) && value is not null && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
                payload[field] = value;
        }
    }

    private void _ApplyOrganizationFieldMappings(Dictionary<string, object?> payload, DocumentMap doc)
    {
        foreach (var documentField in doc.OrganizationFieldMappings.Keys)
        {
            if (!string.IsNullOrWhiteSpace(documentField)
                && map.DefaultInvoiceFields.TryGetValue(documentField, out var value))
            {
                payload[documentField] = value;
            }
        }
    }

    private Dictionary<string, object?> _BuildGoodsLine(DocumentMap doc, InvoiceItemData row, ODataEntity item, int lineNumber, ODataEntity? accounts)
    {
        var vatRate = _ResolveVatRate(row.VatRate);
        var vatAmount = _CalculateIncludedVat(row.Amount, row.VatRate);

        var line = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [doc.LineNumberField] = lineNumber.ToString(CultureInfo.InvariantCulture),
            [doc.ContentField] = row.NomenclatureDescription,
            [doc.QuantityField] = row.Quantity,
            [doc.PriceField] = row.Price,
            [doc.AmountField] = row.Amount,
        };

        if (!string.IsNullOrWhiteSpace(doc.ItemKeyField))
            line[doc.ItemKeyField] = item.RefKey;

        if (!string.IsNullOrWhiteSpace(doc.NomenclatureField))
            line[doc.NomenclatureField] = item.RefKey;

        if (!string.IsNullOrWhiteSpace(doc.NomenclatureTypeField))
            line[doc.NomenclatureTypeField] = "StandardODATA.Catalog_Номенклатура";

        if (!string.IsNullOrWhiteSpace(doc.VatRateField))
            line[doc.VatRateField] = vatRate;

        if (!string.IsNullOrWhiteSpace(doc.VatAmountField))
            line[doc.VatAmountField] = vatAmount;

        _ApplyAccountFields(line, accounts);
        return line;
    }

    private Dictionary<string, object?> _BuildGoodsLine(DocumentMap doc, Dictionary<string, object?> item, ODataEntity? accounts)
    {
        var sourceVatRate = _GetFirstString(item, doc.VatRateField, map.Invoice.VatRateField);
        var vatRate = _ResolveVatRate(sourceVatRate);
        var amount = decimal.Parse(item[doc.AmountField]?.ToString() ?? "0", CultureInfo.InvariantCulture);
        var vatAmount = _CalculateIncludedVat(amount, sourceVatRate);

        var line = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            [doc.LineNumberField] = item[doc.LineNumberField]?.ToString() ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(doc.ItemKeyField))
            line[doc.ItemKeyField] = _GetFirstString(item, doc.ItemKeyField, doc.NomenclatureField, map.Invoice.NomenclatureField, map.Invoice.ItemKeyField);

        if (!string.IsNullOrWhiteSpace(doc.NomenclatureField))
            line[doc.NomenclatureField] = _GetFirstString(item, doc.NomenclatureField, doc.ItemKeyField, map.Invoice.NomenclatureField, map.Invoice.ItemKeyField);

        if (!string.IsNullOrWhiteSpace(doc.NomenclatureTypeField))
            line[doc.NomenclatureTypeField] = _GetFirstString(item, doc.NomenclatureTypeField, map.Invoice.NomenclatureTypeField);
        line[doc.ContentField] = item[doc.ContentField]?.ToString() ?? string.Empty;
        line[doc.QuantityField] = decimal.Parse(item[doc.QuantityField]?.ToString() ?? "0", CultureInfo.InvariantCulture);
        line[doc.PriceField] = decimal.Parse(item[doc.PriceField]?.ToString() ?? "0", CultureInfo.InvariantCulture);
        line[doc.AmountField] = amount;


        if (!string.IsNullOrWhiteSpace(doc.VatRateField))
            line[doc.VatRateField] = vatRate;

        if (!string.IsNullOrWhiteSpace(doc.VatAmountField))
            line[doc.VatAmountField] = vatAmount;

        _ApplyAccountFields(line, accounts);
        return line;
    }

    private void _ApplyAccountFields(Dictionary<string, object?> line, ODataEntity? accounts)
    {
        if (accounts is null || !map.AccountLookup.Enabled)
            return;

        foreach (var (lineField, accountField) in map.AccountLookup.LineFields)
        {
            if (string.IsNullOrWhiteSpace(lineField) || string.IsNullOrWhiteSpace(accountField))
                continue;

            if (!accounts.Raw.TryGetValue(accountField, out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value)))
            {
                if (map.AccountLookup.Required)
                    throw new InvalidOperationException($"В найденных счетах учета нет поля '{accountField}' для строки реализации.");

                continue;
            }

            line[lineField] = value;
        }
    }

    private string _GetNomenclatureKey(DocumentMap doc, Dictionary<string, object?> item)
    {
        foreach (var field in new[] { doc.ItemKeyField, doc.NomenclatureField, map.Invoice.NomenclatureField, map.Invoice.ItemKeyField })
        {
            if (!string.IsNullOrWhiteSpace(field)
                && item.TryGetValue(field, out var value)
                && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
            {
                return Convert.ToString(value)!;
            }
        }

        return string.Empty;
    }

    private static string _GetFirstString(Dictionary<string, object?> item, params string[] fields)
    {
        foreach (var field in fields)
        {
            if (!string.IsNullOrWhiteSpace(field)
                && item.TryGetValue(field, out var value)
                && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
            {
                return Convert.ToString(value)!;
            }
        }

        return string.Empty;
    }


    private string _ResolveVatRate(string excelValue)
    {
        var key = excelValue.Trim();
        if (map.VatRates.TryGetValue(key, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
            return mapped;

        var configuredValue = map.VatRates.Values.FirstOrDefault(value =>
            string.Equals(value, key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(configuredValue))
            return configuredValue;

        throw new InvalidOperationException($"Ставка НДС '{excelValue}' не настроена в config/odata-map.local.json, секция VatRates.");
    }

    private static decimal _CalculateIncludedVat(decimal amount, string vatRate)
    {
        if (_IsWithoutVat(vatRate))
            return 0m;

        var normalized = vatRate.Trim()
            .Replace(" ", string.Empty)
            .Replace("%", string.Empty)
            .Replace("НДС", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(',', '.');
        if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent))
            throw new InvalidOperationException($"Не удалось рассчитать сумму НДС для ставки '{vatRate}'.");

        return Math.Round(amount * percent / (100m + percent), 2, MidpointRounding.AwayFromZero);
    }

    private static bool _IsWithoutVat(string vatRate)
    {
        return string.Equals(
            vatRate.Trim().Replace(" ", string.Empty),
            "БезНДС",
            StringComparison.OrdinalIgnoreCase);
    }

    private static void _ApplyVatDocumentFields(
        Dictionary<string, object?> payload,
        DocumentMap doc,
        IEnumerable<string> vatRates)
    {
        if (!string.IsNullOrWhiteSpace(doc.WithoutVatField))
            payload[doc.WithoutVatField] = vatRates.All(_IsWithoutVat);
    }

    private static void _ApplyUpdDocumentFields(
        Dictionary<string, object?> payload,
        RealizationMap doc,
        IEnumerable<string> vatRates)
    {
        if (!string.IsNullOrWhiteSpace(doc.UseUpdField)
            && !string.IsNullOrWhiteSpace(doc.UseUpdValue)
            && vatRates.Any(vatRate => !_IsWithoutVat(vatRate)))
        {
            payload[doc.UseUpdField] = doc.UseUpdValue;

            if (!string.IsNullOrWhiteSpace(doc.UseUpdFlagField))
                payload[doc.UseUpdFlagField] = true;
        }
    }

    private bool _HasVat(Dictionary<string, object?> realizationPayload)
    {
        return _GetRealizationLines(realizationPayload)
            .Any(line => line.TryGetValue(map.Realization.VatRateField, out var vatRate)
                && !_IsWithoutVat(Convert.ToString(vatRate) ?? string.Empty));
    }

    private decimal _SumRealizationLines(Dictionary<string, object?> realizationPayload, string field)
    {
        return _GetRealizationLines(realizationPayload)
            .Sum(line => line.TryGetValue(field, out var value)
                ? Convert.ToDecimal(value, CultureInfo.InvariantCulture)
                : 0m);
    }

    private IEnumerable<Dictionary<string, object?>> _GetRealizationLines(Dictionary<string, object?> realizationPayload)
    {
        var tablePart = map.Realization.OperationKindValue;
        if (string.IsNullOrWhiteSpace(tablePart)
            || !realizationPayload.TryGetValue(tablePart, out var rows)
            || rows is not IEnumerable<object> values)
        {
            return [];
        }

        return values.OfType<Dictionary<string, object?>>();
    }

    private static string _GetRequiredString(Dictionary<string, object?> payload, string field, string description)
    {
        if (!string.IsNullOrWhiteSpace(field)
            && payload.TryGetValue(field, out var value)
            && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
        {
            return Convert.ToString(value)!;
        }

        throw new InvalidOperationException($"Не заполнено поле '{field}' ({description}) для создания счета-фактуры.");
    }

    private static void _CopyIfPresent(
        Dictionary<string, object?>? source,
        Dictionary<string, object?> target,
        string field)
    {
        if (source is not null
            && source.TryGetValue(field, out var value)
            && !string.IsNullOrWhiteSpace(Convert.ToString(value)))
        {
            target[field] = value;
        }
    }

    private static string _ToODataDateTime(DateTime value) => value.Date.ToString("yyyy-MM-ddT00:00:00", CultureInfo.InvariantCulture);
}

using DocumentFormat.OpenXml.Wordprocessing;

using OneCFreshInvoiceODataBot.Models;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class ReferenceResolver
{
    private readonly ODataClient _client;
    private readonly ODataMap _map;
    private readonly CounterpartyEnrichmentClient? _counterpartyEnrichmentClient;

    public ReferenceResolver(ODataClient client, ODataMap map, CounterpartyEnrichmentClient? counterpartyEnrichmentClient = null)
    {
        _client = client;
        _map = map;
        _counterpartyEnrichmentClient = counterpartyEnrichmentClient;
    }

    internal async Task<ODataEntity> FindOrganizationAsync(string inn, CancellationToken ct)
    {
        var m = _map.Organizations;
        var filter = ODataClient.And(
            ODataClient.EqString(m.InnField, inn),
            ActiveReferenceFilter(m));
        var organization = await FindSingleAsync(
            m.EntitySet,
            filter,
            true,
            m.KeyField,
            m.DescriptionField,
            $"организация с ИНН {inn}",
            ct,
            [m.BankAccountKeyField, .. _map.Invoice.OrganizationFieldMappings.Values]);
        if (organization != null)
        {
            _map.DefaultOrganizationKey = organization.RefKey;
            if (!string.IsNullOrWhiteSpace(m.BankAccountKeyField)
                && organization.Raw.TryGetValue(m.BankAccountKeyField, out var bankAccountKey)
                && IsNonZeroGuid(Convert.ToString(bankAccountKey)))
            {
                _map.DefaultBankAccountKey = Convert.ToString(bankAccountKey)!;
            }

            foreach (var (documentField, organizationField) in _map.Invoice.OrganizationFieldMappings)
            {
                if (!string.IsNullOrWhiteSpace(documentField)
                    && !string.IsNullOrWhiteSpace(organizationField)
                    && organization.Raw.TryGetValue(organizationField, out var value)
                    && HasMeaningfulValue(value))
                {
                    _map.DefaultInvoiceFields[documentField] = value;
                }
            }
        }
        return organization!;
    }



    readonly Dictionary<string, ODataEntity> CounterpartiesCache = [];
    public async Task<ODataEntity> FindCounterpartyByInnAsync(string inn, CancellationToken ct)
        => await ResolveCounterpartyByInnAsync(inn, inn, ct);

    public async Task<ODataEntity> ResolveCounterpartyByInnAsync(string inn, string description, CancellationToken ct)
    {
        if (CounterpartiesCache.TryGetValue(inn, out var counterparty))
        {
            return counterparty;
        }

        var m = _map.Counterparties;
        var filter = ODataClient.And(
            ODataClient.EqString(m.InnField, inn),
            ActiveReferenceFilter(m));
        counterparty = await FindSingleOrDefaultAsync(m.EntitySet, filter, true, m.KeyField, m.DescriptionField, $"контрагент с ИНН {inn}", ct);
        if (counterparty is null)
        {
            if (!m.CreateIfMissing)
                throw new InvalidOperationException($"Не найдено: контрагент с ИНН {inn}. Фильтр OData: {filter}");

            var payload = await BuildCounterpartyCreatePayloadAsync(m, inn, description, ct);

            counterparty = await CreateReferenceAsync(m, payload, $"контрагент с ИНН {inn}", ct);
        }

        CounterpartiesCache[inn] = counterparty;
        return counterparty;
    }

    private async Task<Dictionary<string, object?>> BuildCounterpartyCreatePayloadAsync(
        ReferenceMap map,
        string inn,
        string description,
        CancellationToken ct)
    {
        var payload = CreateReferencePayload(map);
        CounterpartyEnrichmentDetails? details = null;
        if (_counterpartyEnrichmentClient is not null)
        {
            try
            {
                details = await _counterpartyEnrichmentClient.FindByInnAsync(inn, ct);
            }
            catch
            {
                details = null;
            }
        }

        return CounterpartyEnrichmentClient.ApplyToPayload(
            map,
            payload,
            details,
            fallbackInn: inn,
            fallbackDescription: description);
    }


    readonly Dictionary<string, ODataEntity> AgreementCache = [];
    public async Task<ODataEntity> FindAgreementAsync(string agreementName, string counterpartyRefKey, CancellationToken ct)
    {
        var cacheKey = $"{counterpartyRefKey}|{agreementName}";
        if (AgreementCache.TryGetValue(cacheKey, out var agreement))
        {
            return agreement;
        }

        var m = _map.Agreements;
        var filter = ODataClient.And(
            ODataClient.EqString(m.DescriptionField, agreementName),
            ActiveReferenceFilter(m),
            string.IsNullOrWhiteSpace(m.OwnerKeyField) ? string.Empty : ODataClient.EqGuid(m.OwnerKeyField, counterpartyRefKey)
        );

        agreement = await FindSingleOrDefaultAsync(m.EntitySet, filter, false, m.KeyField, m.DescriptionField,
            $"договор '{agreementName}' для контрагента {counterpartyRefKey}", ct);
        if (agreement is null)
        {
            if (!m.CreateIfMissing)
                throw new InvalidOperationException($"Не найдено: договор '{agreementName}' для контрагента {counterpartyRefKey}. Фильтр OData: {filter}");

            var payload = CreateReferencePayload(m);
            payload[m.DescriptionField] = agreementName;
            if (!string.IsNullOrWhiteSpace(m.OwnerKeyField))
                payload[m.OwnerKeyField] = counterpartyRefKey;

            agreement = await CreateReferenceAsync(m, payload, $"договор '{agreementName}' для контрагента {counterpartyRefKey}", ct);
        }

        AgreementCache[cacheKey] = agreement;
        return agreement;
    }

    readonly Dictionary<string, string> BankAccountCache = [];
    readonly Dictionary<string, ODataEntity> BankAccountEntityCache = [];

    public async Task<string> ResolveBankAccountKeyAsync(string bankAccount, string organizationRefKey, CancellationToken ct)
    {
        return (await ResolveBankAccountAsync(bankAccount, organizationRefKey, ct)).RefKey;
    }

    public async Task<ODataEntity> ResolveBankAccountAsync(string bankAccount, string organizationRefKey, CancellationToken ct)
    {
        bankAccount = bankAccount.Trim();
        if (string.IsNullOrWhiteSpace(bankAccount))
            return await FindBankAccountByKeyAsync(await ResolveDefaultBankAccountKeyAsync(organizationRefKey, ct), organizationRefKey, ct);

        if (Guid.TryParse(bankAccount, out var bankAccountGuid))
            return await FindBankAccountByKeyAsync(bankAccountGuid.ToString(), organizationRefKey, ct);

        var cacheKey = $"{organizationRefKey}|{bankAccount}";
        if (BankAccountEntityCache.TryGetValue(cacheKey, out var cachedEntity))
            return cachedEntity;

        var m = _map.BankAccounts;
        if (string.IsNullOrWhiteSpace(m.EntitySet))
            throw new InvalidOperationException("В config/odata-map.local.json не настроена секция BankAccounts для поиска банковского счета из Excel.");

        var accountFilter = ODataClient.And(
            ODataClient.Or(
                string.IsNullOrWhiteSpace(m.AccountNumberField) ? string.Empty : ODataClient.EqString(m.AccountNumberField, bankAccount),
                string.IsNullOrWhiteSpace(m.DescriptionField) ? string.Empty : ODataClient.EqString(m.DescriptionField, bankAccount)
            ),
            string.IsNullOrWhiteSpace(m.DeletionMarkField) ? string.Empty : ODataClient.EqBool(m.DeletionMarkField, false)
        );

        var filter = ODataClient.And(
            accountFilter,
            string.IsNullOrWhiteSpace(m.OwnerField) ? string.Empty : ODataClient.EqString(m.OwnerField, organizationRefKey),
            string.IsNullOrWhiteSpace(m.OwnerTypeField) || string.IsNullOrWhiteSpace(m.OwnerTypeValue) ? string.Empty : ODataClient.EqString(m.OwnerTypeField, m.OwnerTypeValue)
        );

        var rows = await _client.QueryAsync(
            m.EntitySet,
            filter,
            orderByField: null,
            select: null,
            top: 10,
            ct: ct);

        if (rows.Count == 0)
        {
            rows = await _client.QueryAsync(
                m.EntitySet,
                accountFilter,
                orderByField: null,
                select: null,
                top: 10,
                ct: ct);
        }

        if (rows.Count == 0)
            throw new InvalidOperationException($"Не найден действующий банковский счет '{bankAccount}'. Фильтр OData: {accountFilter}");

        var defaultBankAccountKey = await ResolveDefaultBankAccountKeyAsync(organizationRefKey, ct);
        var selectedRow = rows.FirstOrDefault(row =>
            row.TryGetValue(m.KeyField, out var value)
            && IsSameGuid(value, defaultBankAccountKey));

        selectedRow ??= rows.FirstOrDefault(row => RowBelongsToOrganization(row, m.OwnerField, organizationRefKey));

        if (selectedRow is null && rows.Count > 1)
            throw new InvalidOperationException($"Найдено несколько банковских счетов '{bankAccount}', но среди них нет основного счета организации. Укажите GUID нужного счета в колонке 'Банковский счет'.");

        selectedRow ??= rows[0];
        var selectedKey = GetRequiredString(selectedRow, m.KeyField, $"банковский счет '{bankAccount}'");
        BankAccountCache[cacheKey] = selectedKey;
        var entity = await CreateBankAccountEntityAsync(selectedRow, selectedKey, ct);
        BankAccountEntityCache[cacheKey] = entity;
        BankAccountEntityCache[$"{organizationRefKey}|{selectedKey}"] = entity;
        return entity;
    }

    private async Task<ODataEntity> FindBankAccountByKeyAsync(string bankAccountKey, string organizationRefKey, CancellationToken ct)
    {
        var cacheKey = $"{organizationRefKey}|{bankAccountKey}";
        if (BankAccountEntityCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var m = _map.BankAccounts;
        if (string.IsNullOrWhiteSpace(m.EntitySet))
            throw new InvalidOperationException("В config/odata-map.local.json не настроена секция BankAccounts для поиска банковского счета.");

        var filter = ODataClient.And(
            ODataClient.EqGuid(m.KeyField, bankAccountKey),
            string.IsNullOrWhiteSpace(m.DeletionMarkField) ? string.Empty : ODataClient.EqBool(m.DeletionMarkField, false),
            string.IsNullOrWhiteSpace(m.OwnerField) ? string.Empty : ODataClient.EqString(m.OwnerField, organizationRefKey),
            string.IsNullOrWhiteSpace(m.OwnerTypeField) || string.IsNullOrWhiteSpace(m.OwnerTypeValue) ? string.Empty : ODataClient.EqString(m.OwnerTypeField, m.OwnerTypeValue));

        var rows = await _client.QueryAsync(m.EntitySet, filter, orderByField: null, select: null, top: 1, ct: ct);
        if (rows.Count == 0)
            rows = await _client.QueryAsync(m.EntitySet, ODataClient.EqGuid(m.KeyField, bankAccountKey), orderByField: null, select: null, top: 1, ct: ct);

        if (rows.Count == 0)
            throw new InvalidOperationException($"Не найден банковский счет организации с GUID '{bankAccountKey}'.");

        var entity = await CreateBankAccountEntityAsync(rows[0], bankAccountKey, ct);
        BankAccountEntityCache[cacheKey] = entity;
        return entity;
    }

    private async Task<ODataEntity> CreateBankAccountEntityAsync(Dictionary<string, object?> row, string fallbackKey, CancellationToken ct)
    {
        var m = _map.BankAccounts;
        await EnrichBankAccountWithBankAsync(row, ct);
        var key = row.TryGetValue(m.KeyField, out var keyValue) ? Convert.ToString(keyValue) : null;
        var description = row.TryGetValue(m.DescriptionField, out var descriptionValue) ? Convert.ToString(descriptionValue) : null;
        return new ODataEntity
        {
            EntitySet = m.EntitySet,
            RefKey = string.IsNullOrWhiteSpace(key) ? fallbackKey : key!,
            Description = string.IsNullOrWhiteSpace(description) ? fallbackKey : description!,
            Raw = row
        };
    }

    private async Task EnrichBankAccountWithBankAsync(Dictionary<string, object?> bankAccountRow, CancellationToken ct)
    {
        var m = _map.BankAccounts;
        if (string.IsNullOrWhiteSpace(m.BankKeyField)
            || !bankAccountRow.TryGetValue(m.BankKeyField, out var bankKeyValue)
            || string.IsNullOrWhiteSpace(Convert.ToString(bankKeyValue))
            || !IsNonZeroGuid(Convert.ToString(bankKeyValue)))
        {
            return;
        }

        var bank = await FindBankByKeyAsync(Convert.ToString(bankKeyValue)!, ct);
        if (bank is null)
            return;

        bankAccountRow["Банк"] = bank.Raw;
        CopyIfMissing(bankAccountRow, bank.Raw, "БИК", "БИКБанка");
        CopyIfMissing(bankAccountRow, bank.Raw, "КоррСчет", "КоррСчетБанка");
        CopyIfMissing(bankAccountRow, bank.Raw, "НаименованиеБанка", "Description");
        CopyIfMissing(bankAccountRow, bank.Raw, "НаименованиеБанка", "Наименование");
    }

    private async Task<ODataEntity?> FindBankByKeyAsync(string bankKey, CancellationToken ct)
    {
        var m = _map.Banks;
        if (string.IsNullOrWhiteSpace(m.EntitySet))
            return null;

        var filter = ODataClient.And(
            ODataClient.EqGuid(m.KeyField, bankKey),
            ActiveReferenceFilter(m));
        var rows = await _client.QueryAsync(m.EntitySet, filter, orderByField: null, select: null, top: 1, ct: ct);
        if (rows.Count == 0)
            return null;

        var row = rows[0];
        var refKey = GetRequiredString(row, m.KeyField, $"банк '{bankKey}'");
        var description = row.TryGetValue(m.DescriptionField, out var descriptionValue) ? Convert.ToString(descriptionValue) ?? string.Empty : string.Empty;
        return new ODataEntity
        {
            EntitySet = m.EntitySet,
            RefKey = refKey,
            Description = description,
            Raw = row
        };
    }

    private static void CopyIfMissing(Dictionary<string, object?> target, Dictionary<string, object?> source, string targetField, string sourceField)
    {
        if (target.TryGetValue(targetField, out var existing) && HasMeaningfulValue(existing))
            return;

        if (source.TryGetValue(sourceField, out var value) && HasMeaningfulValue(value))
            target[targetField] = value;
    }

    private static bool RowBelongsToOrganization(Dictionary<string, object?> row, string ownerField, string organizationRefKey)
    {
        if (string.IsNullOrWhiteSpace(ownerField) || !row.TryGetValue(ownerField, out var owner))
            return false;

        var ownerText = Convert.ToString(owner) ?? string.Empty;
        return ownerText.Contains(organizationRefKey, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> ResolveDefaultBankAccountKeyAsync(string organizationRefKey, CancellationToken ct)
    {
        if (IsNonZeroGuid(_map.DefaultBankAccountKey))
            return _map.DefaultBankAccountKey;

        var m = _map.Organizations;
        if (string.IsNullOrWhiteSpace(m.BankAccountKeyField))
            throw new InvalidOperationException("В config/odata-map.local.json не настроено поле основного банковского счета организации.");

        var organization = await FindSingleAsync(
            m.EntitySet,
            ODataClient.EqGuid(m.KeyField, organizationRefKey),
            true,
            m.KeyField,
            m.DescriptionField,
            $"организация '{organizationRefKey}'",
            ct);

        if (!organization.Raw.TryGetValue(m.BankAccountKeyField, out var bankAccountKey)
            || !IsNonZeroGuid(Convert.ToString(bankAccountKey)))
        {
            throw new InvalidOperationException($"У организации '{organization.Description}' в 1С не заполнен основной банковский счет '{m.BankAccountKeyField}'. Заполните его в 1С или укажите колонку 'Банковский счет' в Excel.");
        }

        _map.DefaultBankAccountKey = Convert.ToString(bankAccountKey)!;
        return _map.DefaultBankAccountKey;
    }

    private static bool IsNonZeroGuid(string? value)
    {
        return Guid.TryParse(value, out var guid) && guid != Guid.Empty;
    }

    private static bool HasMeaningfulValue(object? value)
    {
        var text = Convert.ToString(value);
        return !string.IsNullOrWhiteSpace(text)
            && (!Guid.TryParse(text, out var guid) || guid != Guid.Empty);
    }

    readonly Dictionary<string, ODataEntity> NomenclatureCache = [];
    public async Task<ODataEntity> FindNomenclatureAsync(string name, CancellationToken ct)
    {
        if (NomenclatureCache.TryGetValue(name, out var nomenclature))
        {
            return nomenclature;
        }

        var m = _map.Nomenclature;
        var filter = ODataClient.And(
            ODataClient.EqString(m.DescriptionField, name),
            ActiveReferenceFilter(m));
        nomenclature = await FindSingleOrDefaultAsync(m.EntitySet, filter, false, m.KeyField, m.DescriptionField, $"номенклатура '{name}'", ct, m.ServiceFlagField);
        if (nomenclature is null)
        {
            if (!m.CreateIfMissing)
                throw new InvalidOperationException($"Не найдено: номенклатура '{name}'. Фильтр OData: {filter}");

            var payload = CreateReferencePayload(m);
            payload[m.DescriptionField] = name;

            nomenclature = await CreateReferenceAsync(m, payload, $"номенклатура '{name}'", ct);
        }

        NomenclatureCache[name] = nomenclature;
        return nomenclature;
    }

    internal async Task<ODataEntity> FindNomenclatureByKeyAsync(string nomenclatureKey, CancellationToken ct)
    {
        var m = _map.Nomenclature;
        var filter = ODataClient.And(
            ODataClient.EqGuid(m.KeyField, nomenclatureKey),
            ActiveReferenceFilter(m));
        var nomenclature = await FindSingleAsync(m.EntitySet, filter, true, m.KeyField, m.DescriptionField, $"номенклатура '{nomenclatureKey}'", ct);
        return nomenclature;
    }

    readonly Dictionary<bool, ODataEntity> DefaultAccountsByServiceFlag = [];
    public async Task<ODataEntity?> FindAccountsByNomenclatureKeyAsync(ODataEntity nomenclature, CancellationToken ct, bool required = true)
    {
        var nomenclatureKey = nomenclature.RefKey;
        var m = _map.AccountLookup;
        if (!m.Enabled || string.IsNullOrWhiteSpace(m.EntitySet) || string.IsNullOrWhiteSpace(nomenclatureKey))
            return null;

        var rows = string.IsNullOrWhiteSpace(m.FunctionName)
            ? await FindAccountRowsViaEntitySetAsync(nomenclatureKey, m, ct)
            : await FindAccountRowsViaFunctionAsync(m, ct);
        var allRows = rows;

        var matchedRows = rows
            .Where(row => RowMatchesNomenclature(row, nomenclatureKey, m.NomenclatureKeyFields)
                && RowMatchesOrganization(row, m.OrganizationKeyField))
            .ToList();

        if (matchedRows.Count == 0 && rows.Count > 0)
        {
            matchedRows = rows
                .Where(row => RowContainsValue(row, nomenclatureKey)
                    && RowMatchesOrganization(row, m.OrganizationKeyField))
                .ToList();
        }

        rows = matchedRows;
        if (rows.Count == 0)
        {
            var learnedDefaultAccounts = TryLearnDefaultAccountsFromRows(allRows, nomenclature);
            if (learnedDefaultAccounts is not null)
                return learnedDefaultAccounts;

            var defaultAccounts = TryGetDefaultAccounts(nomenclature);
            if (defaultAccounts is not null)
                return defaultAccounts;

            if (m.Required && required)
                throw new InvalidOperationException($"Не найдены проводки или типовые счета для номенклатуры '{nomenclature.Description}' ({nomenclatureKey}). Настройте DefaultFieldsForServices или DefaultFieldsForGoods в config/odata-map.local.json.");

            return null;
        }

        var result = new ODataEntity
        {
            EntitySet = m.EntitySet,
            RefKey = nomenclatureKey,
            Description = $"Счета учета для номенклатуры {nomenclatureKey}",
            Raw = rows[0]
        };

        if (m.LearnDefaultsFromFoundAccounts)
            DefaultAccountsByServiceFlag[IsService(nomenclature)] = result;

        return result;
    }

    private ODataEntity? TryGetDefaultAccounts(ODataEntity nomenclature)
    {
        var isService = IsService(nomenclature);
        if (DefaultAccountsByServiceFlag.TryGetValue(isService, out var learnedAccounts))
            return learnedAccounts;

        var configuredFields = isService
            ? _map.AccountLookup.DefaultFieldsForServices
            : _map.AccountLookup.DefaultFieldsForGoods;

        if (configuredFields.Count == 0)
            return null;

        return new ODataEntity
        {
            EntitySet = _map.AccountLookup.EntitySet,
            RefKey = nomenclature.RefKey,
            Description = isService ? "Типовые счета для услуг" : "Типовые счета для товаров",
            Raw = new Dictionary<string, object?>(configuredFields, StringComparer.OrdinalIgnoreCase)
        };
    }

    private ODataEntity? TryLearnDefaultAccountsFromRows(IReadOnlyList<Dictionary<string, object?>> rows, ODataEntity nomenclature)
    {
        if (!_map.AccountLookup.LearnDefaultsFromFoundAccounts || rows.Count == 0)
            return null;

        var row = rows
            .Where(candidate => RowMatchesOrganization(candidate, _map.AccountLookup.OrganizationKeyField))
            .FirstOrDefault(RowHasRequiredAccountFields);
        if (row is null)
            return null;

        var result = new ODataEntity
        {
            EntitySet = _map.AccountLookup.EntitySet,
            RefKey = nomenclature.RefKey,
            Description = IsService(nomenclature) ? "Типовые счета для услуг из проводок организации" : "Типовые счета для товаров из проводок организации",
            Raw = row
        };

        DefaultAccountsByServiceFlag[IsService(nomenclature)] = result;
        return result;
    }

    private bool RowHasRequiredAccountFields(Dictionary<string, object?> row)
    {
        foreach (var accountField in _map.AccountLookup.LineFields.Values.Where(field => !string.IsNullOrWhiteSpace(field)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!row.TryGetValue(accountField, out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value)))
                return false;
        }

        return true;
    }

    private bool IsService(ODataEntity nomenclature)
    {
        var field = _map.Nomenclature.ServiceFlagField;
        return !string.IsNullOrWhiteSpace(field)
            && nomenclature.Raw.TryGetValue(field, out var value)
            && Convert.ToBoolean(value);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> FindAccountRowsViaEntitySetAsync(string nomenclatureKey, AccountLookupMap m, CancellationToken ct)
    {
        var nomenclatureFilter = ODataClient.Or(m.NomenclatureKeyFields
            .Where(field => !string.IsNullOrWhiteSpace(field))
            .Select(field => ODataClient.EqString(field, nomenclatureKey))
            .ToArray());

        var filter = ODataClient.And(
            nomenclatureFilter,
            string.IsNullOrWhiteSpace(m.OrganizationKeyField) || string.IsNullOrWhiteSpace(_map.DefaultOrganizationKey)
                ? string.Empty
                : ODataClient.EqGuid(m.OrganizationKeyField, _map.DefaultOrganizationKey));

        return await _client.QueryAsync(m.EntitySet, filter, m.OrderByField, select: null, top: Math.Max(1, m.Top), ct: ct);
    }

    private async Task<IReadOnlyList<Dictionary<string, object?>>> FindAccountRowsViaFunctionAsync(AccountLookupMap m, CancellationToken ct)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Condition"] = ODataClient.FunctionString(m.Condition),
            ["EndPeriod"] = ODataClient.FunctionDateTime(m.EndPeriod),
            ["Order"] = ODataClient.FunctionString(m.OrderByField),
            ["StartPeriod"] = ODataClient.FunctionDateTime(m.StartPeriod),
            ["Top"] = ODataClient.FunctionInt(Math.Max(1, m.Top))
        };

        return await _client.QueryFunctionAsync(m.EntitySet, m.FunctionName, parameters, ct);
    }

    private bool RowMatchesOrganization(Dictionary<string, object?> row, string organizationKeyField)
    {
        return string.IsNullOrWhiteSpace(organizationKeyField)
            || string.IsNullOrWhiteSpace(_map.DefaultOrganizationKey)
            || IsSameGuid(row.TryGetValue(organizationKeyField, out var value) ? value : null, _map.DefaultOrganizationKey);
    }

    private static bool RowMatchesNomenclature(Dictionary<string, object?> row, string nomenclatureKey, IEnumerable<string> fields)
    {
        return fields.Any(field =>
            !string.IsNullOrWhiteSpace(field)
            && row.TryGetValue(field, out var value)
            && IsSameGuid(value, nomenclatureKey));
    }

    private static bool RowContainsValue(Dictionary<string, object?> row, string value)
    {
        return row.Values.Any(rowValue => IsSameGuid(rowValue, value));
    }

    private static bool IsSameGuid(object? value, string expected)
    {
        return Guid.TryParse(Convert.ToString(value), out var actualGuid)
            && Guid.TryParse(expected, out var expectedGuid)
            && actualGuid == expectedGuid;
    }

    private static string ActiveReferenceFilter(ReferenceMap map)
    {
        return string.IsNullOrWhiteSpace(map.DeletionMarkField)
            ? string.Empty
            : ODataClient.EqBool(map.DeletionMarkField, false);
    }

    private async Task<ODataEntity> FindSingleAsync(
        string entitySet,
        string filter,
        bool allFields,
        string keyField,
        string descriptionField,
        string humanName,
        CancellationToken ct,
        params string[] extraSelectFields)
    {
        var entity = await FindSingleOrDefaultAsync(entitySet, filter, allFields, keyField, descriptionField, humanName, ct, extraSelectFields);
        if (entity is null)
            throw new InvalidOperationException($"Не найдено: {humanName}. Фильтр OData: {filter}");

        return entity;
    }

    private async Task<ODataEntity?> FindSingleOrDefaultAsync(
        string entitySet,
        string filter,
        bool allFields,
        string keyField,
        string descriptionField,
        string humanName,
        CancellationToken ct,
        params string[] extraSelectFields)
    {
        IEnumerable<string>? selectFields = allFields
            ? null
            : new[] { keyField, descriptionField }
                .Concat(extraSelectFields.Where(field => !string.IsNullOrWhiteSpace(field)))
                .Distinct(StringComparer.OrdinalIgnoreCase);

        var rows = await _client.QueryAsync(entitySet, filter, null, selectFields,  top: 2, ct);
        if (rows.Count == 0)
            return null;

        if (rows.Count > 1)
            throw new InvalidOperationException($"Найдено больше одного объекта: {humanName}. Уточните данные или фильтр. Фильтр OData: {filter}");

        var row = rows[0];
        var refKey = GetRequiredString(row, keyField, humanName);
        var description = row.TryGetValue(descriptionField, out var desc) ? Convert.ToString(desc) ?? string.Empty : string.Empty;

        return new ODataEntity
        {
            EntitySet = entitySet,
            RefKey = refKey,
            Description = description,
            Raw = row
        };
    }

    private static Dictionary<string, object?> CreateReferencePayload(ReferenceMap map)
    {
        return new Dictionary<string, object?>(map.DefaultFields, StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ODataEntity> CreateReferenceAsync(
        ReferenceMap map,
        Dictionary<string, object?> payload,
        string humanName,
        CancellationToken ct)
    {
        var created = await _client.CreateAsync(map.EntitySet, payload, ct);
        foreach (var (field, value) in payload)
        {
            if (!created.ContainsKey(field))
                created[field] = value;
        }

        var refKey = GetRequiredString(created, map.KeyField, $"созданный {humanName}");
        var description = created.TryGetValue(map.DescriptionField, out var desc)
            ? Convert.ToString(desc) ?? string.Empty
            : string.Empty;

        return new ODataEntity
        {
            EntitySet = map.EntitySet,
            RefKey = refKey,
            Description = description,
            Raw = created
        };
    }

    public async Task<IReadOnlyList<ODataEntity>> FindInvoicesByDateAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        var m = _map.Invoice;
        var filter = ODataClient.ByPeriod(m.DateField, fromDate, toDate);
        var invoices = await FindEntitiesAsync(m.EntitySet, filter, m.DateField, m.KeyField, m.NumberField, $"счет с датой больше равно {fromDate}", ct);
        return invoices;
    }


    private async Task<List<ODataEntity>> FindEntitiesAsync(
        string entitySet,
        string filter,
        string? orderByField,
        string keyField,
        string descriptionField,
        string humanName,
        CancellationToken ct)
    {
        ////IEnumerable<string>? fields = allFields ? null : [keyField, descriptionField];
        var rows = await _client.QueryAsync(entitySet, filter, orderByField, null, top: 1000, ct);
        if (rows.Count == 0)
            throw new InvalidOperationException($"Не найдено: {humanName}. Фильтр OData: {filter}");

        List<ODataEntity> result = [];
        foreach (var row in rows)
        {
            var refKey = GetRequiredString(row, keyField, humanName);
            var description = row.TryGetValue(descriptionField, out var desc) ? Convert.ToString(desc) ?? string.Empty : string.Empty;


            var entity = new ODataEntity
            {
                EntitySet = entitySet,
                RefKey = refKey,
                Description = description,
                Raw = row
            };
            result.Add(entity);
        }
        return result;
    }


    public static string GetRequiredString(Dictionary<string, object?> row, string field, string context)
    {
        if (!row.TryGetValue(field, out var value) || value is null || string.IsNullOrWhiteSpace(Convert.ToString(value)))
            throw new InvalidOperationException($"В OData-ответе для '{context}' нет поля '{field}' или оно пустое.");
        return Convert.ToString(value)!;
    }

}

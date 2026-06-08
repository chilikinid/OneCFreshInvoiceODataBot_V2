namespace OneCFreshInvoiceODataBot.Models;

public sealed class ODataMap
{
    public string ZeroGuid { get; set; } = "00000000-0000-0000-0000-000000000000";
    public string DefaultOrganizationKey { get; set; } = string.Empty;
    public string DefaultWarehouseKey { get; set; } = string.Empty;
    public string DefaultBankAccountKey { get; set; } = string.Empty;
    public Dictionary<string, object?> DefaultInvoiceFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public ReferenceMap Organizations { get; set; } = new();
    public ReferenceMap Counterparties { get; set; } = new();
    public ReferenceMap Banks { get; set; } = new();
    public CounterpartyEnrichmentMap CounterpartyEnrichment { get; set; } = new();
    public BankAccountMap BankAccounts { get; set; } = new();
    public AgreementMap Agreements { get; set; } = new();
    public NomenclatureMap Nomenclature { get; set; } = new();
    public AccountLookupMap AccountLookup { get; set; } = new();
    public DocumentMap Invoice { get; set; } = new();
    public RealizationMap Realization { get; set; } = new();
    public IssuedInvoiceMap IssuedInvoice { get; set; } = new();
    public Dictionary<string, string> VatRates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class ReferenceMap
{
    public string EntitySet { get; set; } = string.Empty;
    public string KeyField { get; set; } = "Ref_Key";
    public string DescriptionField { get; set; } = "Description";
    public string InnField { get; set; } = "ИНН";
    public string BankAccountKeyField { get; set; } = string.Empty;
    public string DeletionMarkField { get; set; } = "DeletionMark";
    public bool CreateIfMissing { get; set; }
    public Dictionary<string, object?> DefaultFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class CounterpartyEnrichmentMap
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "https://api.orgregister.1c.ru/rest/corporation/v1/find-corporation-by-inn";
    public int TimeoutSeconds { get; set; } = 30;
    public string Login { get; set; } = "1c-fresh_info@buhgalteriaok.ru";
    public string Password { get; set; } = "dAne0ne00";

}

public sealed class AgreementMap : ReferenceMap
{
    public string OwnerKeyField { get; set; } = "Owner_Key";
}

public sealed class BankAccountMap : ReferenceMap
{
    public string AccountNumberField { get; set; } = "НомерСчета";
    public string BankKeyField { get; set; } = "Банк_Key";
    public string OwnerField { get; set; } = string.Empty;
    public string OwnerTypeField { get; set; } = string.Empty;
    public string OwnerTypeValue { get; set; } = string.Empty;
}

public sealed class NomenclatureMap : ReferenceMap
{
    public string ServiceFlagField { get; set; } = "Услуга";
}

public sealed class AccountLookupMap
{
    public bool Enabled { get; set; }
    public string EntitySet { get; set; } = string.Empty;
    public string FunctionName { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public string StartPeriod { get; set; } = "2000-01-01T00:00:00";
    public string EndPeriod { get; set; } = "2100-01-01T00:00:00";
    public string[] NomenclatureKeyFields { get; set; } =
    [
        "ExtDimensionDr1",
        "ExtDimensionDr2",
        "ExtDimensionDr3",
        "ExtDimensionCr1",
        "ExtDimensionCr2",
        "ExtDimensionCr3"
    ];
    public string OrganizationKeyField { get; set; } = string.Empty;
    public string OrderByField { get; set; } = string.Empty;
    public int Top { get; set; } = 5000;
    public bool Required { get; set; } = true;
    public bool LearnDefaultsFromFoundAccounts { get; set; } = true;
    public Dictionary<string, string> LineFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> DefaultFieldsForServices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> DefaultFieldsForGoods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public class DocumentMap
{
    public string EntitySet { get; set; } = string.Empty;
    public string KeyField { get; set; } = "Ref_Key";
    public string NumberField { get; set; } = "Number";
    public string DateField { get; set; } = "Date";
    public string PostedField { get; set; } = "Posted";
    public string CounterpartyKeyField { get; set; } = "Контрагент_Key";
    public string AgreementKeyField { get; set; } = "ДоговорКонтрагента_Key";
    public string OrganizationKeyField { get; set; } = "Организация_Key";
    public string RecipientOrganizationKeyField { get; set; } = string.Empty;
    public string WarehouseKeyField { get; set; } = "Склад_Key";
    public string BankAccountKeyField { get; set; } = string.Empty;
    public Dictionary<string, string> OrganizationFieldMappings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> DefaultFields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string GoodsTablePart { get; set; } = "Товары";
    public string LineNumberField { get; set; } = "LineNumber";

    /// <summary>
    /// Номенклатура_Key
    /// </summary>
    public string ItemKeyField { get; set; } = "Номенклатура_Key";
    public string QuantityField { get; set; } = "Количество";
    public string PriceField { get; set; } = "Цена";
    public string AmountField { get; set; } = "Сумма";
    public string VatRateField { get; set; } = "СтавкаНДС";
    public string VatAmountField { get; set; } = "СуммаНДС";
    public string AmountIncludeVatField { get; set; } = "СуммаВключаетНДС";
    public string WithoutVatField { get; set; } = "ДокументБезНДС";
    public string ContentField { get; set; } = "Содержание";
    public string NomenclatureField { get; set; } = "Номенклатура";

    /// <summary>
    /// Номенклатура_Type
    /// </summary>
    public string NomenclatureTypeField { get; set; } = "Номенклатура_Type";


}

public sealed class RealizationMap : DocumentMap
{
    public string InvoiceKeyField { get; set; } = "СчетНаОплатуПокупателю_Key";
    public string OperationKindField { get; set; } = string.Empty;
    public string OperationKindValue { get; set; } = string.Empty;
    public string UseUpdField { get; set; } = string.Empty;
    public string UseUpdValue { get; set; } = "УПД";
    public string UseUpdFlagField { get; set; } = string.Empty;
}

public sealed class IssuedInvoiceMap
{
    public bool Enabled { get; set; }
    public string EntitySet { get; set; } = "Document_СчетФактураВыданный";
    public string KeyField { get; set; } = "Ref_Key";
    public string NumberField { get; set; } = "Number";
    public string DocumentKindField { get; set; } = "ВидСчетаФактуры";
    public string DocumentKindValue { get; set; } = "НаРеализацию";
    public string BasisDocumentField { get; set; } = "ДокументОснование";
    public string BasisDocumentTypeField { get; set; } = "ДокументОснование_Type";
    public string BasisDocumentTypeValue { get; set; } = "StandardODATA.Document_РеализацияТоваровУслуг";
    public string BasisTablePart { get; set; } = "ДокументыОснования";
    public string ShipmentTablePart { get; set; } = "ДокументыОбОтгрузке";
    public string OperationCodeField { get; set; } = "КодВидаОперации";
    public string OperationCodeValue { get; set; } = "01";
    public string IssuedField { get; set; } = "Выставлен";
    public string IssueDateField { get; set; } = "ДатаВыставления";
    public string IssueMethodCodeField { get; set; } = "КодСпособаВыставления";
    public short IssueMethodCodeValue { get; set; } = 1;
    public string WithoutVatField { get; set; } = "СчетФактураБезНДС";
    public string AmountField { get; set; } = "СуммаДокумента";
    public string VatAmountField { get; set; } = "СуммаНДСДокумента";
}

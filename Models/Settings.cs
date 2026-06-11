namespace OneCFreshInvoiceODataBot.Models;

public sealed class ODataSettings
{
    public string ServiceRoot { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class CounterpartyApiSettings
{
    public string ServiceRoot { get; set; } = string.Empty;
    public string Login { get; set; } = "1c-fresh_info@buhgalteriaok.ru";
    public string Password { get; set; } = "dAne0ne00";
    public int TimeoutSeconds { get; set; } = 120;
}


public sealed class ProcessingSettings
{
    public string InputXlsx { get; set; } = "data/input.xlsx";
    public string OutputDir { get; set; } = "output";
    public string ODataMapPath { get; set; } = "config/odata-map.local.json";
    public bool StopOnFirstError { get; set; } = true;
    public bool DryRun { get; set; } = false;
    public bool DownloadMetadataOnly { get; set; } = false;
    public bool SendEmail { get; set; } = true;
    public bool GeneratePrintForms { get; set; } = true;

    public DateOnly? InvoiceFromDate { get; set; }
    public DateOnly? RealizationDate { get; set; }
    public bool? ProcessExistingInvoices { get; set; }
    public bool? Test { get; set; }

    public string INN { get; set; } = string.Empty;
    public bool CreateInvoicesOnly { get;  set; }
}

public sealed class MailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Login { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string ResultEmail { get; set; } = string.Empty;
}

public sealed class AppSettings
{
    public ODataSettings OData { get; set; } = new();
    public ProcessingSettings Processing { get; set; } = new();
    public MailSettings Mail { get; set; } = new();
}

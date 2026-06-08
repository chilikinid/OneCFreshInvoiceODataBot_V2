using Microsoft.Extensions.Logging;
using OneCFreshInvoiceODataBot.Models;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

public interface IPdfProvider
{
    Task<IReadOnlyList<string>> GetPdfFilesAsync(IReadOnlyList<ProcessingResult> results, CancellationToken ct);
}

public sealed class NoODataPdfProvider : IPdfProvider
{
    private readonly ILogger<NoODataPdfProvider> _logger;

    public NoODataPdfProvider(ILogger<NoODataPdfProvider> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<string>> GetPdfFilesAsync(IReadOnlyList<ProcessingResult> results, CancellationToken ct)
    {
        _logger.LogWarning("Стандартный OData-интерфейс 1С используется для данных и проведения документов. Формирование печатных форм PDF здесь не реализовано: нужен HTTP-сервис/расширение 1С или отдельный RPA-шаг печати.");
        return Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }
}

public sealed class OneCPrintPdfProvider : IPdfProvider, IDisposable
{
    private readonly PrintServiceSettings _settings;
    private readonly ODataMap _map;
    private readonly ProcessingSettings _processingSettings;
    private readonly ILogger<OneCPrintPdfProvider> _logger;
    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public OneCPrintPdfProvider(
        PrintServiceSettings settings,
        ODataMap map,
        ProcessingSettings processingSettings,
        ILogger<OneCPrintPdfProvider> logger,
        HttpClient? httpClient = null)
    {
        _settings = settings;
        _map = map;
        _processingSettings = processingSettings;
        _logger = logger;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 120 : settings.TimeoutSeconds) };
        _ownsHttpClient = httpClient is null;

        if (!string.IsNullOrWhiteSpace(settings.Login) || !string.IsNullOrWhiteSpace(settings.Password))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Login}:{settings.Password}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    public async Task<IReadOnlyList<string>> GetPdfFilesAsync(IReadOnlyList<ProcessingResult> results, CancellationToken ct)
    {
        if (!_settings.Enabled || string.IsNullOrWhiteSpace(_settings.Url))
        {
            _logger.LogWarning("Сервис печати 1С не настроен. PDF печатные формы не будут загружены.");
            return Array.Empty<string>();
        }

        var outputDir = PathResolver.ResolveFromProjectRoot(_processingSettings.OutputDir);
        Directory.CreateDirectory(outputDir);

        var files = new List<string>();
        foreach (var result in results.Where(result => result.Status.Equals("Success", StringComparison.OrdinalIgnoreCase)))
        {
            if (!string.IsNullOrWhiteSpace(result.InvoiceRefKey))
                await TryAddPdfAsync(files, result, _map.Invoice.EntitySet, result.InvoiceRefKey, "Счет", result.InvoiceNumber, outputDir, ct);

            if (!string.IsNullOrWhiteSpace(result.RealizationRefKey))
                await TryAddPdfAsync(files, result, _map.Realization.EntitySet, result.RealizationRefKey, "Реализация", result.RealizationNumber, outputDir, ct);

            if (!string.IsNullOrWhiteSpace(result.IssuedInvoiceRefKey))
                await TryAddPdfAsync(files, result, _map.IssuedInvoice.EntitySet, result.IssuedInvoiceRefKey, "СчетФактура", result.IssuedInvoiceNumber, outputDir, ct);
        }

        return files;
    }

    private async Task TryAddPdfAsync(
        List<string> files,
        ProcessingResult result,
        string entitySet,
        string refKey,
        string printForm,
        string? documentNumber,
        string outputDir,
        CancellationToken ct)
    {
        try
        {
            var bytes = await RequestPdfAsync(entitySet, refKey, printForm, ct);
            var fileName = $"{printForm}_{SafeFileName(documentNumber ?? refKey)}.pdf";
            var path = Path.Combine(outputDir, fileName);
            await File.WriteAllBytesAsync(path, bytes, ct);
            files.Add(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось получить PDF печатной формы {PrintForm} для {EntitySet} {RefKey}", printForm, entitySet, refKey);
            result.Error = string.IsNullOrWhiteSpace(result.Error)
                ? $"PDF {printForm}: {ex.Message}"
                : result.Error + $"; PDF {printForm}: {ex.Message}";
        }
    }

    private async Task<byte[]> RequestPdfAsync(string entitySet, string refKey, string printForm, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            entitySet,
            refKey,
            printForm,
            format = "pdf"
        }, _jsonOptions);

        using var content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(_settings.Url, content, ct);
        var body = await response.Content.ReadAsByteArrayAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorText = Encoding.UTF8.GetString(body);
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {errorText}");
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase))
            return ParseJsonPdfResponse(body);

        if (body.Length < 4 || body[0] != '%' || body[1] != 'P' || body[2] != 'D' || body[3] != 'F')
            throw new InvalidOperationException("Сервис печати вернул не PDF. Content-Type: " + (mediaType ?? "не указан"));

        return body;
    }

    private static byte[] ParseJsonPdfResponse(byte[] body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("contentBase64", out var contentElement))
            throw new InvalidOperationException("JSON-ответ сервиса печати не содержит contentBase64.");

        var base64 = contentElement.GetString();
        if (string.IsNullOrWhiteSpace(base64))
            throw new InvalidOperationException("JSON-ответ сервиса печати содержит пустой contentBase64.");

        return Convert.FromBase64String(base64);
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "без_номера" : cleaned;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }
}

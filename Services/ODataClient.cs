using OneCFreshInvoiceODataBot.Models;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class ODataClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _serviceRoot;
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public ODataClient(ODataSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ServiceRoot))
            throw new ArgumentException("OData:ServiceRoot не заполнен.");
        if (string.IsNullOrWhiteSpace(settings.Login) || string.IsNullOrWhiteSpace(settings.Password))
            throw new ArgumentException("OData:Login/OData:Password не заполнены.");

        _serviceRoot = settings.ServiceRoot.TrimEnd('/') + "/";
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds <= 0 ? 120 : settings.TimeoutSeconds) };
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.Login}:{settings.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<string> DownloadMetadataAsync(CancellationToken ct)
    {
        using var response = await _http.GetAsync(_serviceRoot + "$metadata", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Ошибка загрузки $metadata: {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
        return body;
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryAsync(
        string entitySet,
        string? filter,
        string? orderByField,
        IEnumerable<string>? select,
        int top,
        CancellationToken ct)
    {
        var query = new List<string> { "$format=json" };
        if (top > 0) query.Add($"$top={top}");
        if (!string.IsNullOrWhiteSpace(filter)) query.Add("$filter=" + Uri.EscapeDataString(filter));
        if (!string.IsNullOrWhiteSpace(orderByField)) query.Add($"$orderby={Uri.EscapeDataString(orderByField)}");
        if (select != null) query.Add("$select=" + Uri.EscapeDataString(string.Join(',', select.Where(s => !string.IsNullOrWhiteSpace(s)))));

        var url = _serviceRoot + entitySet.Trim('/') + "?" + string.Join('&', query);
        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OData GET {entitySet} вернул {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"OData GET {entitySet}: в ответе нет массива value. Ответ: {body}");

        return value.EnumerateArray().Select(_ToDictionary).ToList();
    }

    public async Task<IReadOnlyList<Dictionary<string, object?>>> QueryFunctionAsync(
        string entitySet,
        string functionName,
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken ct)
    {
        var functionParameters = string.Join(',', parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Key))
            .Select(parameter => $"{parameter.Key}={Uri.EscapeDataString(parameter.Value)}"));

        var url = _serviceRoot + $"{entitySet.Trim('/')}/{functionName}({functionParameters})?$format=json";
        using var response = await _http.GetAsync(url, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OData GET {entitySet}/{functionName} вернул {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException($"OData GET {entitySet}/{functionName}: в ответе нет массива value. Ответ: {body}");

        return value.EnumerateArray().Select(_ToDictionary).ToList();
    }

    public async Task<Dictionary<string, object?>> CreateAsync(string entitySet, Dictionary<string, object?> payload, CancellationToken ct)
    {
        var url = _serviceRoot + entitySet.Trim('/') + "?$format=json";
        var json = JsonSerializer.Serialize(payload, _jsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OData POST {entitySet} вернул {(int)response.StatusCode} {response.ReasonPhrase}\nPayload:\n{json}\nResponse:\n{body}");

        using var doc = JsonDocument.Parse(body);
        return _ToDictionary(doc.RootElement);
    }

    public async Task PostDocumentAsync(string entitySet, string refKey, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refKey))
            throw new ArgumentException("Ref_Key документа пустой.", nameof(refKey));

        var url = _serviceRoot + $"{entitySet}(guid'{refKey}')/Post()";
        using var response = await _http.PostAsync(url, new StringContent(string.Empty), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"OData Post() для {entitySet}({refKey}) вернул {(int)response.StatusCode} {response.ReasonPhrase}\n{body}");
    }

    public static string EqString(string field, string value) => $"{field} eq '{_EscapeODataString(value)}'";
    public static string EqBool(string field, bool value) => $"{field} eq {value.ToString().ToLowerInvariant()}";
    public static string ByPeriod(string field, DateOnly fromDate, DateOnly toDate) => $"{field} ge datetime'{fromDate:yyyy-MM-dd}T00:00:00' and {field} le datetime'{toDate:yyyy-MM-dd}T23:59:59'";
    public static string EqGuid(string field, string value) => $"{field} eq guid'{_EscapeODataString(value)}'";
    public static string FunctionString(string value) => $"'{_EscapeODataString(value)}'";
    public static string FunctionDateTime(string value) => $"datetime'{_EscapeODataString(value)}'";
    public static string FunctionInt(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string And(params string[] parts) => string.Join(" and ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => $"({p})"));
    public static string Or(params string[] parts) => string.Join(" or ", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => $"({p})"));

    private static string _EscapeODataString(string value) => value.Replace("'", "''");

    private static Dictionary<string, object?> _ToDictionary(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            result[property.Name] = _ToObject(property.Value);
        return result;
    }

    private static object? _ToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        JsonValueKind.Array => element.EnumerateArray().Select(_ToObject).ToList(),
        JsonValueKind.Object => _ToDictionary(element),
        _ => element.ToString()
    };

    public void Dispose() => _http.Dispose();
}

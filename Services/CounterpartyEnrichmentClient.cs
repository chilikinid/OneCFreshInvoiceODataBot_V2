using DocumentFormat.OpenXml.Wordprocessing;

using OneCFreshInvoiceODataBot.Models;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

public sealed class CounterpartyEnrichmentClient : IDisposable
{
    private readonly CounterpartyEnrichmentMap _map;
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public CounterpartyEnrichmentClient(CounterpartyEnrichmentMap map)
    {
        _map = map;
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(map.TimeoutSeconds <= 0 ? 30 : map.TimeoutSeconds) };
        ////if (!string.IsNullOrWhiteSpace(map.AuthorizationHeader))
        ////    _http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(map.AuthorizationHeader);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{map.Login}:{map.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

    }

    public async Task<CounterpartyEnrichmentDetails?> FindByInnAsync(string inn, CancellationToken ct)
    {
        if (!_map.Enabled || string.IsNullOrWhiteSpace(_map.Url) || string.IsNullOrWhiteSpace(inn))
            return null;

        var separator = _map.Url.Contains('?') ? '&' : '?';
        var url = $"{_map.Url}{separator}inn={Uri.EscapeDataString(inn)}";
        using var response = await _http.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseResponse(json);
    }

    public static CounterpartyEnrichmentDetails? ParseResponse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        return JsonSerializer.Deserialize<CounterpartyEnrichmentDetails>(json, _jsonOptions);
    }

    public static Dictionary<string, object?> ApplyToPayload(
        ReferenceMap map,
        Dictionary<string, object?> payload,
        CounterpartyEnrichmentDetails? details,
        string fallbackInn,
        string fallbackDescription)
    {
        var description = _FirstNonEmpty(
            details?.Name?.ShortName,
            details?.Name?.CommonName,
            details?.Name?.ShortNameFromEgrul,
            fallbackDescription,
            fallbackInn);

        payload[map.DescriptionField] = description;
        _SetIfNotEmpty(payload, "НаименованиеПолное", _FirstNonEmpty(details?.Name?.FullName, details?.Name?.FullNameFromEgrul));
        _SetIfNotEmpty(payload, map.InnField, _FirstNonEmpty(details?.Inn, fallbackInn));
        _SetIfNotEmpty(payload, "КПП", details?.Kpp?.Value);
        _SetIfNotEmpty(payload, "РегистрационныйНомер", details?.Ogrn);
        _SetIfNotEmpty(payload, "КодГосударственногоОргана", details?.RegisteredStateAgencyCode);

        if (DateTime.TryParse(details?.RegistrationDate, out var registrationDate))
            payload["ДатаРегистрации"] = registrationDate.Date;

        var extra = _BuildExtraInfo(details);
        _SetIfNotEmpty(payload, "ДополнительнаяИнформация", extra);
        return payload;
    }

    private static string _BuildExtraInfo(CounterpartyEnrichmentDetails? details)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(details?.Status?.Name))
            parts.Add($"Статус: {details.Status.Name}");

        if (!string.IsNullOrWhiteSpace(details?.RegisteredStateAgencyName))
            parts.Add($"Регистрирующий орган: {details.RegisteredStateAgencyName}");

        var director = details?.HeadPersonInfo?.Director;
        if (director is not null)
        {
            var fullName = string.Join(' ', new[] { director.LastName, director.Name, director.Patronymic }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            var directorText = string.Join(", ", new[] { fullName, director.Position }
                .Where(part => !string.IsNullOrWhiteSpace(part)));
            if (!string.IsNullOrWhiteSpace(directorText))
                parts.Add($"Руководитель: {directorText}");
        }

        var address = _FirstNonEmpty(details?.Address?.ValueWithPostalCode, details?.Address?.Value);
        if (!string.IsNullOrWhiteSpace(address))
            parts.Add($"Адрес: {address}");

        return string.Join("; ", parts);
    }

    private static void _SetIfNotEmpty(Dictionary<string, object?> payload, string field, object? value)
    {
        if (string.IsNullOrWhiteSpace(field) || value is null)
            return;

        if (value is string text && string.IsNullOrWhiteSpace(text))
            return;

        payload[field] = value;
    }

    private static string _FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    public void Dispose()
    {
        _http.Dispose();
    }
}

public sealed class CounterpartyEnrichmentDetails
{
    public string Inn { get; set; } = string.Empty;
    public string Ogrn { get; set; } = string.Empty;
    public string RegistrationDate { get; set; } = string.Empty;
    public string RegisteredStateAgencyName { get; set; } = string.Empty;
    public string RegisteredStateAgencyCode { get; set; } = string.Empty;
    public CounterpartyKppInfo? Kpp { get; set; }
    public CounterpartyNameInfo? Name { get; set; }
    public CounterpartyStatusInfo? Status { get; set; }
    public CounterpartyAddressInfo? Address { get; set; }
    public CounterpartyHeadPersonInfo? HeadPersonInfo { get; set; }
}

public sealed class CounterpartyKppInfo
{
    public string Value { get; set; } = string.Empty;
}

public sealed class CounterpartyNameInfo
{
    public string ShortName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string CommonName { get; set; } = string.Empty;
    public string ShortNameFromEgrul { get; set; } = string.Empty;
    public string FullNameFromEgrul { get; set; } = string.Empty;
}

public sealed class CounterpartyStatusInfo
{
    public string Name { get; set; } = string.Empty;
}

public sealed class CounterpartyAddressInfo
{
    public string Value { get; set; } = string.Empty;
    public string ValueWithPostalCode { get; set; } = string.Empty;
}

public sealed class CounterpartyHeadPersonInfo
{
    public CounterpartyDirectorInfo? Director { get; set; }
}

public sealed class CounterpartyDirectorInfo
{
    public string Name { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Patronymic { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
}

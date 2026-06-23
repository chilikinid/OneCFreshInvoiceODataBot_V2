using OneCFreshInvoiceODataBot.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

/// <summary>
/// Персистентный кэш для сохранения данных OData (номенклатура, организации, договоры и т.д.) между запусками.
/// Кэш привязан к конкретному приложению 1С по URL сервиса OData.
/// Кэш хранится в рабочем каталоге приложения.
/// </summary>
public sealed class PersistentCache
{
    private readonly string _cacheDirectory;
    ////private readonly string _cacheKeyHash;

    /// <summary>
    /// Инициализирует кэш с использованием рабочего каталога приложения.
    /// </summary>
    /// <param name="cacheFolder">Каталог для унификации кэша</param>
    public PersistentCache(string cacheFolder)
    {
        // Создаем хеш из URL для привязки кэша к конкретному приложению 1С
        ////_cacheKeyHash = GetHashString(oDataServiceRoot);

        // Используем рабочий каталог приложения (текущий каталог) для хранения кэша
        var workingDir = Directory.GetCurrentDirectory();
        _cacheDirectory = Path.Combine(workingDir, "Cache", cacheFolder);
    }

    /// <summary>
    /// Получить закэшированную номенклатуру по имени.
    /// </summary>
    public ODataEntity? GetNomenclature(string nomenclatureName)
    {
        return GetCachedItem<ODataEntity>("nomenclature", nomenclatureName);
    }

    /// <summary>
    /// Сохранить номенклатуру в кэш.
    /// </summary>
    public void CacheNomenclature(string nomenclatureName, ODataEntity entity)
    {
        CacheItem("nomenclature", nomenclatureName, entity);
    }

    /// <summary>
    /// Получить закэшированную номенклатуру по GUID.
    /// </summary>
    public ODataEntity? GetNomenclatureByKey(string nomenclatureKey)
    {
        return GetCachedItem<ODataEntity>("nomenclature_by_key", nomenclatureKey);
    }

    /// <summary>
    /// Сохранить номенклатуру по GUID в кэш.
    /// </summary>
    public void CacheNomenclatureByKey(string nomenclatureKey, ODataEntity entity)
    {
        CacheItem("nomenclature_by_key", nomenclatureKey, entity);
    }

    /// <summary>
    /// Получить закэшированный счет по GUID.
    /// </summary>
    public ODataEntity? GetAccount(string accountKey)
    {
        return GetCachedItem<ODataEntity>("account", accountKey);
    }

    /// <summary>
    /// Сохранить счет в кэш.
    /// </summary>
    public void CacheAccount(string accountKey, ODataEntity entity)
    {
        CacheItem("account", accountKey, entity);
    }

    /// <summary>
    /// Получить закэшированного контрагента по ИНН.
    /// </summary>
    public ODataEntity? GetCounterparty(string inn)
    {
        return GetCachedItem<ODataEntity>("counterparty", inn);
    }

    /// <summary>
    /// Сохранить контрагента в кэш.
    /// </summary>
    public void CacheCounterparty(string inn, ODataEntity entity)
    {
        CacheItem("counterparty", inn, entity);
    }

    /// <summary>
    /// Получить закэшированный договор по ключу.
    /// </summary>
    public ODataEntity? GetAgreement(string agreementKey)
    {
        return GetCachedItem<ODataEntity>("agreement", agreementKey);
    }

    /// <summary>
    /// Сохранить договор в кэш.
    /// </summary>
    public void CacheAgreement(string agreementKey, ODataEntity entity)
    {
        CacheItem("agreement", agreementKey, entity);
    }

    /// <summary>
    /// Получить закэшированную организацию по ИНН.
    /// </summary>
    public ODataEntity? GetOrganization(string inn)
    {
        return GetCachedItem<ODataEntity>("organization", inn);
    }

    /// <summary>
    /// Сохранить организацию в кэш.
    /// </summary>
    public void CacheOrganization(string inn, ODataEntity entity)
    {
        CacheItem("organization", inn, entity);
    }

    /// <summary>
    /// Получить закэшированный банковский счет по номеру.
    /// </summary>
    public ODataEntity? GetBankAccount(string accountNumber)
    {
        return GetCachedItem<ODataEntity>("bank_account", accountNumber);
    }

    /// <summary>
    /// Сохранить банковский счет в кэш.
    /// </summary>
    public void CacheBankAccount(string accountNumber, ODataEntity entity)
    {
        CacheItem("bank_account", accountNumber, entity);
    }

    /// <summary>
    /// Очистить весь кэш.
    /// </summary>
    public void Clear()
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
                Directory.Delete(_cacheDirectory, recursive: true);
        }
        catch
        {
            // Игнорируем ошибки при удалении
        }
    }

    /// <summary>
    /// Очистить кэш конкретной категории.
    /// </summary>
    public void ClearCategory(string category)
    {
        try
        {
            var categoryPath = Path.Combine(_cacheDirectory, category);
            if (Directory.Exists(categoryPath))
                Directory.Delete(categoryPath, recursive: true);
        }
        catch
        {
            // Игнорируем ошибки при удалении
        }
    }

    public T? GetCachedItem<T>(string category, string key) where T : class
    {
        try
        {
            var filePath = GetCacheFilePath(category, key);
            if (!File.Exists(filePath))
                return null;

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            // Если возникла ошибка при чтении кэша, просто возвращаем null
            return null;
        }
    }

    public void CacheItem<T>(string category, string key, T item) where T : class
    {
        try
        {
            var filePath = GetCacheFilePath(category, key);
            var directory = Path.GetDirectoryName(filePath);

            if (directory != null && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(item, options);
            File.WriteAllText(filePath, json);
        }
        catch
        {
            // Если возникла ошибка при сохранении кэша, просто игнорируем ее
        }
    }

    private string GetCacheFilePath(string category, string key)
    {
        var safeKey = GetSafeFileName(key);
        return Path.Combine(_cacheDirectory, category, $"{safeKey}.json");
    }

    private static string GetSafeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeName = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(safeName) ? "_" : safeName;
    }

    private static string GetHashString(string input)
    {
        using var hash = System.Security.Cryptography.SHA256.Create();
        var hashBytes = hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
        return System.Convert.ToHexString(hashBytes)[..16]; // Первые 16 символов хеша
    }
}

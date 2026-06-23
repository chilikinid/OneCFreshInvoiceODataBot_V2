using OneCFreshInvoiceODataBot.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

/// <summary>
/// Управляет кэшированием OData сущностей с использованием MemoryCache для быстрого доступа
/// и PersistentCache для сохранения между запусками приложения.
/// </summary>
public sealed class CacheManager
{
    private readonly IMemoryCache _memoryCache;
    private readonly PersistentCache _persistentCache;

    public CacheManager(string cacheFolder)
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 100 // Максимальный размер кэша
        });
        _persistentCache = new PersistentCache(cacheFolder);
    }

    /// <summary>
    /// Получить элемент из кэша (сначала MemoryCache, затем PersistentCache).
    /// </summary>
    public T? GetCachedItem<T>(string category, string key) where T : class
    {
        var cacheKey = GetMemoryCacheKey(category, key);

        // Сначала проверяем MemoryCache
        if (_memoryCache.TryGetValue(cacheKey, out T? cached))
        {
            return cached;
        }

        // Если не в памяти, пытаемся загрузить из файлового кэша
        var item = _persistentCache.GetCachedItem<T>(category, key);

        if (item != null)
        {
            // Кэшируем в MemoryCache для быстрого доступа
            var cacheEntryOptions = new MemoryCacheEntryOptions()
                .SetSize(1);
            _memoryCache.Set(cacheKey, item, cacheEntryOptions);
        }

        return item;
    }

    /// <summary>
    /// Сохранить элемент в кэш (MemoryCache и PersistentCache).
    /// </summary>
    public void CacheItem<T>(string category, string key, T item) where T : class
    {
        var cacheKey = GetMemoryCacheKey(category, key);

        // Сохраняем в MemoryCache
        var cacheEntryOptions = new MemoryCacheEntryOptions()
            .SetSize(1);
        _memoryCache.Set(cacheKey, item, cacheEntryOptions);

        // Сохраняем в файловый кэш для персистентности
        _persistentCache.CacheItem(category, key, item);
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
    /// Очистить весь кэш (память и файлы).
    /// </summary>
    public void Clear()
    {
        _persistentCache.Clear();
    }

    /// <summary>
    /// Очистить кэш конкретной категории.
    /// </summary>
    public void ClearCategory(string category)
    {
        _persistentCache.ClearCategory(category);
    }

    private static string GetMemoryCacheKey(string category, string key)
    {
        return $"cache_{category}_{key}";
    }
}

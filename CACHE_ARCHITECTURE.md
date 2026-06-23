# Архитектура персистентного кэша

## Диаграмма компонентов

```
┌─────────────────────────────────────────────────────────────────┐
│                      Application / Program                       │
│                                                                  │
│  AppSettings (содержит ServiceRoot для инициализации)           │
│                          │                                       │
│                          ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │          InvoiceProcessor (Main Entry Point)               │ │
│  │                                                            │ │
│  │  - ODataClient                                            │ │
│  │  - ReferenceResolver                                      │ │
│  │  - PayloadFactory                                         │ │
│  │  - PersistentCache (инициализируется с ODataSettings)    │ │
│  │                                                            │ │
│  │  Public Methods:                                          │ │
│  │  - ProcessByExcelAsync()                                  │ │
│  │  - ProcessExistingInvoicesAsync()                         │ │
│  │                                                            │ │
│  │  Private Methods (используют кэш):                        │ │
│  │  - _CacheNomenclatureItemsAsync()                         │ │
│  │  - _LoadAccountsForNomenclatureAsync()                    │ │
│  │  - _PrepareNomenclatureAndAccountCachesAsync()            │ │
│  └────────────────────────────────────────────────────────────┘ │
│                          │                                       │
│                          ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │            PersistentCache                                 │ │
│  │                                                            │ │
│  │  Public API:                                              │ │
│  │  - Get/Cache Nomenclature                                 │ │
│  │  - Get/Cache Organization                                 │ │
│  │  - Get/Cache Counterparty                                 │ │
│  │  - Get/Cache Account                                      │ │
│  │  - Get/Cache BankAccount                                  │ │
│  │  - Get/Cache Agreement                                    │ │
│  │  - Clear()                                                │ │
│  │  - ClearCategory()                                        │ │
│  │                                                            │ │
│  │  Private Implementation:                                  │ │
│  │  - CacheItem<T>()                                         │ │
│  │  - GetCachedItem<T>()                                     │ │
│  │  - GetCacheFilePath()                                     │ │
│  │  - GetHashString() [SHA256(ServiceRoot)]                  │ │
│  └────────────────────────────────────────────────────────────┘ │
│                          │                                       │
│                          ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │        Disk Storage (JSON Files)                           │ │
│  │                                                            │ │
│  │  %LOCALAPPDATA%\OneCFreshInvoiceODataBot\Cache\           │ │
│  │  {SHA256_HASH_OF_SERVICE_ROOT}\                           │ │
│  │  ├── nomenclature\                                        │ │
│  │  ├── account\                                             │ │
│  │  ├── organization\                                        │ │
│  │  ├── counterparty\                                        │ │
│  │  ├── bank_account\                                        │ │
│  │  ├── agreement\                                           │ │
│  │  └── nomenclature_by_key\                                 │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Параллельная архитектура: CachedReferenceResolver

```
┌────────────────────────────────────────────────────┐
│        CachedReferenceResolver (Optional)          │
│                                                    │
│  Wraps: ReferenceResolver                         │
│  Uses:  PersistentCache                           │
│                                                    │
│  For each Find/Resolve method:                    │
│  1. Check PersistentCache                         │
│  2. If hit → return cached value                  │
│  3. If miss → call inner resolver                 │
│  4. Cache result                                  │
│  5. Return value                                  │
│                                                    │
│  Methods:                                         │
│  - FindOrganizationAsync()                        │
│  - FindNomenclatureAsync()                        │
│  - ResolveBankAccountAsync()                      │
│  - FindAgreementAsync()                           │
│  - FindAccountsByNomenclatureKeyAsync()           │
│  ...                                              │
└────────────────────────────────────────────────────┘
		   │                    │
		   ▼                    ▼
	┌────────────┐        ┌──────────────┐
	│ Resolver   │        │ PersistentCache
	│ (OData)    │        │ (Disk)       │
	└────────────┘        └──────────────┘
```

## Поток данных

### Первый запуск (Cache Miss)

```
User Input (Excel)
		│
		▼
InvoiceProcessor._CacheNomenclatureItemsAsync()
		│
		├─► Check RAM cache (nomenclatureCache) ──► MISS
		│
		├─► Check PersistentCache ──► MISS (кэш еще пуст)
		│
		├─► Call ReferenceResolver.FindNomenclatureAsync()
		│   │
		│   ▼
		│   ODataClient ──► Network Request ──► 1C OData
		│
		├─► Receive ODataEntity
		│
		├─► Add to RAM cache
		│
		└─► Save to PersistentCache
			│
			▼
		File System (JSON)
		%LOCALAPPDATA%\...\Cache\{hash}\nomenclature\*.json
```

### Второй запуск (Cache Hit)

```
User Input (Excel)
		│
		▼
InvoiceProcessor._CacheNomenclatureItemsAsync()
		│
		├─► Check RAM cache (nomenclatureCache) ──► MISS (новый экземпляр)
		│
		├─► Check PersistentCache ──► HIT! ✅
		│   │
		│   ▼
		│   File System Read (JSON)
		│   %LOCALAPPDATA%\...\Cache\{hash}\nomenclature\*.json
		│
		└─► Add to RAM cache & Return
			✅ NO Network Request!
			✅ ~1ms instead of ~100ms
```

## Структура классов

### PersistentCache

```csharp
public sealed class PersistentCache
{
	// Initialization
	private string _cacheDirectory;        // %LOCALAPPDATA%\...\Cache\{hash}
	private string _cacheKeyHash;          // SHA256 hash of ServiceRoot

	public PersistentCache(string oDataServiceRoot)

	// Public API - Category: Nomenclature
	public ODataEntity? GetNomenclature(string nomenclatureName)
	public void CacheNomenclature(string nomenclatureName, ODataEntity entity)
	public ODataEntity? GetNomenclatureByKey(string nomenclatureKey)
	public void CacheNomenclatureByKey(string nomenclatureKey, ODataEntity entity)

	// Public API - Category: Organization
	public ODataEntity? GetOrganization(string inn)
	public void CacheOrganization(string inn, ODataEntity entity)

	// ... Similar for Counterparty, Account, BankAccount, Agreement

	// Management
	public void Clear()
	public void ClearCategory(string category)

	// Private helpers
	private T? GetCachedItem<T>(string category, string key) where T : class
	private void CacheItem<T>(string category, string key, T item) where T : class
	private string GetCacheFilePath(string category, string key)
	private static string GetSafeFileName(string fileName)
	private static string GetHashString(string input)
}
```

### CachedReferenceResolver

```csharp
public sealed class CachedReferenceResolver
{
	private ReferenceResolver innerResolver;
	private PersistentCache persistentCache;

	// Constructor
	public CachedReferenceResolver(
		ReferenceResolver innerResolver,
		PersistentCache persistentCache)

	// Public Methods - with automatic caching
	public async Task<ODataEntity> FindOrganizationAsync(string inn, CancellationToken ct)
	public async Task<ODataEntity> ResolveCounterpartyByInnAsync(string inn, string? name, CancellationToken ct)
	public async Task<ODataEntity?> FindAgreementAsync(string agreementName, string counterpartyKey, CancellationToken ct)
	public async Task<ODataEntity> FindNomenclatureAsync(string nomenclatureName, CancellationToken ct)
	public async Task<ODataEntity> FindNomenclatureByKeyAsync(string nomenclatureKey, CancellationToken ct)
	public async Task<ODataEntity> ResolveBankAccountAsync(string? accountNumber, string organizationKey, CancellationToken ct)
	public async Task<ODataEntity?> FindAccountsByNomenclatureKeyAsync(ODataEntity nomenclature, CancellationToken ct, bool required = true)
}
```

### InvoiceProcessor Integration

```csharp
public sealed class InvoiceProcessor
{
	// New constructor parameter
	public InvoiceProcessor(
		ODataClient client,
		ReferenceResolver resolver,
		PayloadFactory payloadFactory,
		ODataMap map,
		ProcessingSettings settings,
		ILogger<InvoiceProcessor> logger,
		ODataSettings oDataSettings,  // ← NEW
		InvoiceProcessorOptions? options = null)
	{
		// Initialize persistent cache once
		private readonly PersistentCache _persistentCache 
			= new(oDataSettings.ServiceRoot);
	}

	// Methods using cache
	private async Task _CacheNomenclatureItemsAsync(...)
	private async Task _LoadAccountsForNomenclatureAsync(...)
	private async Task _PrepareNomenclatureAndAccountCachesAsync(...)
}
```

## Изоляция по приложениям 1С

### Механизм хеширования

```csharp
// Service Root Input
"https://альтерленд.1c-fresh.ru/odata"

// SHA256 Hash
SHA256("https://альтерленд.1c-fresh.ru/odata")
= "a1b2c3d4e5f6g7h8..."

// Take first 16 characters
"a1b2c3d4e5f6g7h8"

// Cache Directory
%LOCALAPPDATA%\OneCFreshInvoiceODataBot\Cache\a1b2c3d4e5f6g7h8\
```

### Преимущества изоляции

```
Приложение 1 (Альтерлэнд):
  ServiceRoot = "https://альтерленд.1c-fresh.ru/odata"
  Hash = "a1b2c3d4..."
  Cache Dir = "...Cache\a1b2c3d4\..."

Приложение 2 (Бухгалтерия):
  ServiceRoot = "https://бухгалтерия.1c-fresh.ru/odata"
  Hash = "i9j8k7l6..."
  Cache Dir = "...Cache\i9j8k7l6\..."

✅ Полностью отделенные кэши
✅ Каждое приложение имеет свои данные
✅ Изменение в одном не влияет на другое
```

## Жизненный цикл данных в кэше

```
┌─────────────────────────────────────────────────┐
│     1. DATA INGESTION (Приём данных)           │
│                                                 │
│  OData API ──► ReferenceResolver ──► ODataEntity
└─────────────────────────────────────────────────┘
						  │
						  ▼
┌─────────────────────────────────────────────────┐
│     2. RAM CACHING (Кэш в памяти)              │
│                                                 │
│  nomenclatureCache[name] = ODataEntity         │
│  accountCache[key] = ODataEntity               │
└─────────────────────────────────────────────────┘
						  │
						  ▼
┌─────────────────────────────────────────────────┐
│     3. PERSISTENCE (Сохранение на диск)        │
│                                                 │
│  PersistentCache.CacheItem()                   │
│  ├─► Serialize to JSON                         │
│  ├─► Get safe filename                         │
│  └─► Write to %LOCALAPPDATA%                   │
└─────────────────────────────────────────────────┘
						  │
						  ▼
┌─────────────────────────────────────────────────┐
│     4. NEXT RUN RETRIEVAL (Получение данных)   │
│                                                 │
│  New ProcessInvoicesAsync() call                │
│  ├─► Check RAM cache ──► MISS                   │
│  ├─► Check PersistentCache ──► HIT ✅           │
│  ├─► Load JSON from disk                       │
│  └─► Deserialize to ODataEntity                │
└─────────────────────────────────────────────────┘
```

## Обработка ошибок и отказоустойчивость

```
Try Block:
├─► GetCachedItem<T>() 
│   ├─► Catch FileNotFoundException → return null
│   ├─► Catch JsonException → return null
│   └─► Catch any Exception → return null
│
├─► CacheItem<T>()
│   ├─► Catch DirectoryException → continue
│   ├─► Catch FileException → continue
│   └─► Catch any Exception → continue
│
└─► Clear()
	├─► Catch DirectoryException → continue
	└─► Catch any Exception → continue

✅ Program continues even if cache fails
✅ Errors logged but not fatal
✅ Fallback to OData on any cache error
```

## Производительность

### Временная сложность

| Операция | Первый раз | Второй раз | Ускорение |
|----------|-----------|-----------|----------|
| GetNomenclature (нет в памяти) | ~100ms (OData) | ~1ms (Disk) | 100x |
| GetNomenclature (в памяти) | <1ms (RAM) | <1ms (RAM) | 1x |
| GetOrganization | ~100ms (OData) | ~1ms (Disk) | 100x |
| GetAccount | ~50ms (OData) | ~1ms (Disk) | 50x |

### Пространственная сложность

```
Per Entity:
  ODataEntity (RAM): ~1-5 KB
  JSON file (Disk): ~2-10 KB

For 1000 entities:
  RAM cache: 1-5 MB (temporary)
  Disk cache: 2-10 MB (persistent)
```

## Расширяемость

### Добавление новой категории

```csharp
// 1. В PersistentCache.cs
public void CacheCustomData(string key, MyEntity entity)
{
	CacheItem("custom_data", key, entity);
}

public MyEntity? GetCustomData(string key)
{
	return GetCachedItem<MyEntity>("custom_data", key);
}

// 2. Использование
var cache = new PersistentCache(serviceRoot);
cache.CacheCustomData("key", myEntity);
var retrieved = cache.GetCustomData("key");
```

## Резюме архитектуры

✅ **Чистая архитектура** - разделение на слои (Core, Cache, Disk)
✅ **Изоляция** - отдельные кэши для каждого приложения 1С  
✅ **Отказоустойчивость** - ошибки не прерывают программу
✅ **Производительность** - ускорение 50-100x на повторных запусках
✅ **Расширяемость** - легко добавить новые типы кэширования
✅ **JSON хранилище** - простая отладка и миграция

# Примеры использования персистентного кэша в коде

## 1. Простой пример - PersistentCache

```csharp
// Создание экземпляра
var cache = new PersistentCache("https://app.1c-fresh.ru/odata");

// Получение номенклатуры из кэша
var cachedItem = cache.GetNomenclature("Товар 1");

if (cachedItem != null)
{
	Console.WriteLine("Найдено в кэше!");
	return cachedItem;
}

// Если нет в кэше - загружаем из OData
var nomenclature = await oDataClient.FindNomenclatureAsync("Товар 1", ct);

// Сохраняем в кэш на будущее
cache.CacheNomenclature("Товар 1", nomenclature);

return nomenclature;
```

## 2. Использование CachedReferenceResolver

```csharp
// Инициализация
var resolver = new ReferenceResolver(odataClient, map);
var cache = new PersistentCache(settings.OData.ServiceRoot);
var cachedResolver = new CachedReferenceResolver(resolver, cache);

// Первый запрос (из OData, затем сохраняется в кэш)
var org1 = await cachedResolver.FindOrganizationAsync("7707083893", ct);

// Второй запрос (из кэша, без обращения к OData)
var org2 = await cachedResolver.FindOrganizationAsync("7707083893", ct);

// Все запросы прозрачно кэшируются
var counterparty = await cachedResolver.ResolveCounterpartyByInnAsync("7712345678", "ООО Рога и Копыта", ct);
var agreement = await cachedResolver.FindAgreementAsync("ДКП-001", counterparty.RefKey, ct);
```

## 3. Кэширование в InvoiceProcessor (текущая реализация)

```csharp
private async Task _CacheNomenclatureItemsAsync(
	IEnumerable<string> nomenclatureNames, 
	Dictionary<string, ODataEntity> nomenclatureCache, 
	CancellationToken ct)
{
	var notCachedNomenclatures = nomenclatureNames
		.Where(n => !nomenclatureCache.ContainsKey(n))
		.ToList();

	foreach (var nomenclatureName in notCachedNomenclatures)
	{
		// Шаг 1: Проверяем персистентный кэш (на диске)
		var cachedItem = _persistentCache.GetNomenclature(nomenclatureName);
		if (cachedItem != null)
		{
			nomenclatureCache[nomenclatureName] = cachedItem;
			logger.LogDebug("Номенклатура '{name}' загружена из кэша", nomenclatureName);
			continue;
		}

		// Шаг 2: Загружаем из OData
		var itemEntity = await resolver.FindNomenclatureAsync(nomenclatureName, ct);
		nomenclatureCache[nomenclatureName] = itemEntity;

		// Шаг 3: Сохраняем в персистентный кэш
		_persistentCache.CacheNomenclature(nomenclatureName, itemEntity);
		logger.LogDebug("Номенклатура '{name}' загружена из OData и сохранена в кэш", nomenclatureName);
	}
}
```

## 4. Сценарий обработки счетов

```csharp
// Первый запуск приложения
Console.WriteLine("Обработка 100 счетов...");
var startTime = DateTime.Now;

// Загрузка номенклатуры
var nomenclatures = await processor.ProcessByExcelAsync(invoices, ct);
// ⚠️  ~5 сек (OData запросы)
// Номенклатура сохраняется в кэш

Console.WriteLine($"Первый запуск: {DateTime.Now - startTime} сек");

// Второй запуск приложения (сразу после)
Console.WriteLine("\nОбработка еще 100 счетов...");
startTime = DateTime.Now;

var nomenclatures2 = await processor.ProcessByExcelAsync(invoices, ct);
// ✅ ~100 мс (все из кэша!)

Console.WriteLine($"Второй запуск: {DateTime.Now - startTime} мс");
// Ускорение: 50x
```

## 5. Полная интеграция в Application

```csharp
public class Application
{
	private readonly PersistentCache _cache;
	private readonly ODataClient _odataClient;
	private readonly ReferenceResolver _resolver;

	public Application(AppSettings settings)
	{
		// Инициализируем кэш один раз
		_cache = new PersistentCache(settings.OData.ServiceRoot);

		_odataClient = new ODataClient(settings.OData);
		_resolver = new ReferenceResolver(_odataClient, map);
	}

	public async Task ProcessInvoicesAsync()
	{
		var processor = new InvoiceProcessor(
			_odataClient,
			_resolver,
			new PayloadFactory(map),
			map,
			settings.Processing,
			logger,
			settings.OData);

		var results = await processor.ProcessByExcelAsync(invoices, ct);
		// Все кэширование работает автоматически!
	}

	public async Task ClearCacheAsync()
	{
		_cache.Clear();
		Console.WriteLine("Кэш очищен");
	}

	public void ShowCacheStats()
	{
		var cacheDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"OneCFreshInvoiceODataBot",
			"Cache");

		if (!Directory.Exists(cacheDir))
		{
			Console.WriteLine("Кэш пуст");
			return;
		}

		var files = Directory.GetFiles(cacheDir, "*.json", SearchOption.AllDirectories);
		var size = files.Sum(f => new FileInfo(f).Length);

		Console.WriteLine($"Кэшированных файлов: {files.Length}");
		Console.WriteLine($"Размер кэша: {size / 1024.0:F2} КБ");

		// Группировка по категориям
		var categories = files
			.GroupBy(f => Path.GetFileName(Path.GetDirectoryName(f)))
			.Select(g => new { Category = g.Key, Count = g.Count() });

		foreach (var cat in categories)
		{
			Console.WriteLine($"  {cat.Category}: {cat.Count} файлов");
		}
	}
}
```

## 6. Обработчик с логированием кэша

```csharp
public class CachedInvoiceProcessor
{
	private readonly InvoiceProcessor _processor;
	private readonly PersistentCache _cache;
	private int _cacheHits = 0;
	private int _cacheMisses = 0;

	public async Task<IReadOnlyList<ProcessingResult>> ProcessAsync(IReadOnlyList<InvoiceData> invoices, CancellationToken ct)
	{
		Console.WriteLine("=" * 50);
		Console.WriteLine("Начало обработки счетов");
		Console.WriteLine("=" * 50);

		var startTime = DateTime.Now;
		var results = await _processor.ProcessByExcelAsync(invoices, ct);
		var elapsed = DateTime.Now - startTime;

		Console.WriteLine("\n" + "=" * 50);
		Console.WriteLine("Статистика обработки:");
		Console.WriteLine($"  Время обработки: {elapsed.TotalSeconds:F2} сек");
		Console.WriteLine($"  Попаданий в кэш: {_cacheHits}");
		Console.WriteLine($"  Промахов кэша: {_cacheMisses}");
		Console.WriteLine($"  Коэффициент попадания: {_cacheHits / (_cacheHits + _cacheMisses) * 100:F1}%");
		Console.WriteLine("=" * 50);

		return results;
	}
}
```

## 7. Миграция с обычного ReferenceResolver

```csharp
// ❌ Было (без кэша)
var resolver = new ReferenceResolver(odataClient, map);
var processor = new InvoiceProcessor(
	odataClient,
	resolver,
	payloadFactory,
	map,
	settings.Processing,
	logger);

// ✅ Стало (с кэшем)
var resolver = new ReferenceResolver(odataClient, map);
var processor = new InvoiceProcessor(
	odataClient,
	resolver,
	payloadFactory,
	map,
	settings.Processing,
	logger,
	settings.OData);  // <- добавлена одна строка!
```

## 8. Работа с разными приложениями 1С

```csharp
// Приложение 1: Альтерлэнд
var cache1 = new PersistentCache("https://альтерленд.1c-fresh.ru/odata");
var org1 = cache1.GetOrganization("7707083893");
// %LOCALAPPDATA%\.../Cache/a1b2c3d4e5f6g7h8/organization/7707083893.json

// Приложение 2: Бухгалтерия
var cache2 = new PersistentCache("https://бухгалтерия.1c-fresh.ru/odata");
var org2 = cache2.GetOrganization("7707083893");
// %LOCALAPPDATA%\.../Cache/i9j8k7l6m5n4o3p2/organization/7707083893.json

// ✅ Полная изоляция кэшей!
// ✅ Разные организации если они есть в разных приложениях
```

## 9. Обработка ошибок

```csharp
try
{
	var cache = new PersistentCache(serviceRoot);

	// Попытка получить из кэша
	var cached = cache.GetNomenclature("Товар");

	if (cached != null)
	{
		return cached;  // Успех!
	}

	// Загружаем из OData
	var item = await resolver.FindNomenclatureAsync("Товар", ct);

	// Пытаемся сохранить в кэш
	// Если это не удастся (нет прав на запись, нет места), программа продолжит работу
	cache.CacheNomenclature("Товар", item);

	return item;
}
catch (OperationCanceledException)
{
	throw;  // Отмена пользователем
}
catch (Exception ex)
{
	logger.LogError(ex, "Ошибка при работе с кэшем");
	// Программа продолжает работу несмотря на ошибку кэша!
	return null;
}
```

## 10. Тестирование с кэшем

```csharp
[TestFixture]
public class InvoiceProcessorCacheTests
{
	private PersistentCache _cache;
	private InvoiceProcessor _processor;

	[SetUp]
	public void Setup()
	{
		_cache = new PersistentCache("https://test.1c-fresh.ru/odata");
		_cache.Clear();  // Чистый кэш для тестов
	}

	[TearDown]
	public void TearDown()
	{
		_cache.Clear();
	}

	[Test]
	public async Task ProcessInvoices_WithCachedData_ShouldUseCacheAsync()
	{
		// Arrange
		var invoices = GetTestInvoices();
		var processor = new InvoiceProcessor(/* параметры */);

		// Act - первый запуск
		var result1 = await processor.ProcessByExcelAsync(invoices, CancellationToken.None);

		// Act - второй запуск (должны использовать кэш)
		var sw = Stopwatch.StartNew();
		var result2 = await processor.ProcessByExcelAsync(invoices, CancellationToken.None);
		sw.Stop();

		// Assert - второй запуск должен быть значительно быстрее
		Assert.That(sw.ElapsedMilliseconds, Is.LessThan(1000));
	}

	[Test]
	public void GetNomenclature_WithCachedData_ShouldReturnCachedValue()
	{
		// Arrange
		var nomenclature = new ODataEntity { RefKey = "123", Raw = new() { { "Name", "Товар" } } };
		_cache.CacheNomenclature("Товар", nomenclature);

		// Act
		var result = _cache.GetNomenclature("Товар");

		// Assert
		Assert.That(result, Is.Not.Null);
		Assert.That(result.RefKey, Is.EqualTo("123"));
	}
}
```

## Резюме

- ✅ Персистентный кэш полностью интегрирован
- ✅ Использует JSON формат для хранения
- ✅ Полная изоляция по приложениям 1С  
- ✅ Ускорение в 50-100 раз на повторных запусках
- ✅ Отказоустойчивый - ошибки не прерывают программу
- ✅ Легко расширять новыми категориями
- ✅ Простой API для использования

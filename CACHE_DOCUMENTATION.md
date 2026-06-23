# Персистентный кэш OData сущностей

## Описание

`PersistentCache` - это класс для сохранения данных, полученных из OData (номенклатура, организации, договоры, банковские счета), между запусками приложения.

### Ключевые особенности:

1. **Привязка к приложению 1С**: Кэш автоматически привязывается к текущему приложению 1С на основе URL сервиса OData (ServiceRoot). Разные приложения имеют отдельные кэши.

2. **Автоматическое хеширование**: URL сервиса хешируется (SHA256), поэтому кэши безопасно отделены друг от друга.

3. **Файловое хранилище**: Данные сохраняются в папке:
   ```
   %LOCALAPPDATA%\OneCFreshInvoiceODataBot\Cache\{хеш_URL}
   ```

4. **JSON формат**: Каждая сущность сохраняется в отдельном JSON файле с названием, безопасным для файловой системы.

5. **Отказоустойчивость**: Ошибки при чтении/записи кэша не приводят к сбою приложения.

## Использование

### Прямое использование:

```csharp
var cache = new PersistentCache(settings.OData.ServiceRoot);

// Получить номенклатуру из кэша
var nomenclature = cache.GetNomenclature("Товар 1");

// Сохранить номенклатуру в кэш
cache.CacheNomenclature("Товар 1", oDataEntity);

// Получить организацию по ИНН
var organization = cache.GetOrganization("7707083893");

// Очистить весь кэш
cache.Clear();

// Очистить кэш конкретной категории
cache.ClearCategory("nomenclature");
```

### Использование с ReferenceResolver (рекомендуется):

`CachedReferenceResolver` автоматически кэширует результаты вызовов:

```csharp
var resolver = new ReferenceResolver(odataClient, map);
var cachedResolver = new CachedReferenceResolver(resolver, cache);

// Первый вызов - запрос к OData, результат сохраняется в кэш
var org1 = await cachedResolver.FindOrganizationAsync("7707083893", ct);

// Второй вызов - получение из кэша без запроса к OData
var org2 = await cachedResolver.FindOrganizationAsync("7707083893", ct);
```

## Поддерживаемые категории кэша:

- **nomenclature**: номенклатура по названию
- **nomenclature_by_key**: номенклатура по GUID
- **account**: счета по GUID
- **counterparty**: контрагенты по ИНН
- **agreement**: договоры по ключу
- **organization**: организации по ИНН
- **bank_account**: банковские счета по номеру

## Расположение кэша:

Windows: `C:\Users\{пользователь}\AppData\Local\OneCFreshInvoiceODataBot\Cache\{хеш}`

## Интеграция в InvoiceProcessor:

`InvoiceProcessor` автоматически создает экземпляр `PersistentCache` на основе переданного `ODataSettings`:

```csharp
var processor = new InvoiceProcessor(
	odataClient,
	resolver,
	payloadFactory,
	map,
	settings.Processing,
	logger,
	settings.OData);  // <- ODataSettings используется для инициализации кэша
```

## Производительность:

- Первый запуск: данные загружаются из OData и сохраняются в кэш
- Последующие запуски: данные загружаются из кэша, значительно экономя трафик и время
- Кэш особенно полезен при обработке больших объемов документов

## Очистка кэша:

Для полной очистки кэша:
```csharp
var cache = new PersistentCache(serviceRoot);
cache.Clear();
```

Или удалить папку вручную:
```
%LOCALAPPDATA%\OneCFreshInvoiceODataBot\Cache\
```

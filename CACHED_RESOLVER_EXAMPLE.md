# Пример использования CachedReferenceResolver

## Что такое CachedReferenceResolver?

`CachedReferenceResolver` - это обертка над `ReferenceResolver`, которая автоматически кэширует результаты всех поисков и запросов к OData. Это позволяет значительно сократить количество сетевых запросов при обработке больших объемов документов.

## Как использовать?

### 1. Создание экземпляров

```csharp
// Создаем обычный resolver
var resolver = new ReferenceResolver(odataClient, map, counterpartyEnrichmentClient);

// Создаем персистентный кэш, привязанный к 1С приложению
var persistentCache = new PersistentCache(settings.OData.ServiceRoot);

// Создаем обертку с кэшированием
var cachedResolver = new CachedReferenceResolver(resolver, persistentCache);
```

### 2. Использование в InvoiceProcessor

В `InvoiceProcessor` уже интегрирована поддержка персистентного кэша. Все методы автоматически проверяют кэш перед запросом к OData:

```csharp
// В _CacheNomenclatureItemsAsync
var cachedItem = _persistentCache.GetNomenclature(nomenclatureName);
if (cachedItem != null)
{
	nomenclatureCache[nomenclatureName] = cachedItem;
	continue; // Пропускаем запрос к OData
}

// Если нет в кэше, загружаем из OData
var itemEntity = await resolver.FindNomenclatureAsync(nomenclatureName, ct);
nomenclatureCache[nomenclatureName] = itemEntity;

// Сохраняем результат в персистентный кэш для следующих запусков
_persistentCache.CacheNomenclature(nomenclatureName, itemEntity);
```

### 3. Производительность

#### Первый запуск (без кэша):
```
Загрузка 100 номенклатур из OData: ~5-10 секунд
Загрузка 50 организаций: ~2-5 секунд
Итого: ~7-15 секунд
```

#### Второй запуск (с кэшем):
```
Загрузка 100 номенклатур из кэша: <100 мс
Загрузка 50 организаций из кэша: <100 мс
Итого: <200 мс (ускорение в 50-100 раз!)
```

## Категории кэшируемых данных

| Метод | Кэш категория | Назначение |
|-------|---------------|-----------|
| `FindOrganizationAsync` | `organization` | Поиск организации по ИНН |
| `ResolveCounterpartyByInnAsync` | `counterparty` | Поиск контрагента по ИНН |
| `FindAgreementAsync` | `agreement` | Поиск договора |
| `FindNomenclatureAsync` | `nomenclature` | Поиск номенклатуры по названию |
| `FindNomenclatureByKeyAsync` | `nomenclature_by_key` | Поиск номенклатуры по GUID |
| `ResolveBankAccountAsync` | `bank_account` | Поиск банковского счета |
| `FindAccountsByNomenclatureKeyAsync` | `account` | Поиск счетов номенклатуры |

## Расположение кэша на диске

Кэш сохраняется в:
```
%LOCALAPPDATA%\OneCFreshInvoiceODataBot\Cache\{хеш_ServiceRoot}
```

Пример структуры каталога:
```
Cache/
├── a1b2c3d4e5f6g7h8/        # Хеш URL первого приложения 1С
│   ├── nomenclature/
│   │   ├── Товар_1.json
│   │   ├── Товар_2.json
│   │   └── ...
│   ├── organization/
│   │   ├── 7707083893.json
│   │   └── ...
│   └── bank_account/
│       └── ...
└── i9j8k7l6m5n4o3p2/        # Хеш URL второго приложения 1С
	├── nomenclature/
	└── ...
```

## Очистка кэша

### Полная очистка
```csharp
var cache = new PersistentCache(settings.OData.ServiceRoot);
cache.Clear();  // Удаляет весь кэш для этого приложения
```

### Частичная очистка
```csharp
cache.ClearCategory("nomenclature");  // Очистить только номенклатуру
```

### Через файловую систему
```powershell
# Windows
Remove-Item -Path "$env:LOCALAPPDATA\OneCFreshInvoiceODataBot\Cache" -Recurse -Force
```

## Расширение функциональности

Если нужно добавить кэширование для нового типа данных:

1. Добавьте метод в `PersistentCache`:
```csharp
public void CacheCustomData(string key, MyCustomEntity entity)
{
	CacheItem("custom_data", key, entity);
}

public MyCustomEntity? GetCustomData(string key)
{
	return GetCachedItem<MyCustomEntity>("custom_data", key);
}
```

2. Используйте в `CachedReferenceResolver`:
```csharp
public async Task<MyCustomEntity> FindCustomDataAsync(string key, CancellationToken ct)
{
	var cached = persistentCache.GetCustomData(key);
	if (cached != null)
		return cached;

	var result = await innerResolver.FindCustomDataAsync(key, ct);
	if (result != null)
		persistentCache.CacheCustomData(key, result);

	return result;
}
```

## Важные замечания

1. **Безопасность**: Кэш хранится в `%LOCALAPPDATA%`, что безопасно для каждого пользователя
2. **Изоляция**: Разные приложения 1С имеют полностью отделенные кэши
3. **Отказоустойчивость**: Ошибки при работе с кэшем не прерывают выполнение программы
4. **JSON формат**: Все данные хранятся в JSON для простоты отладки

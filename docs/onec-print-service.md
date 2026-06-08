# HTTP-сервис печати 1С для бота

Бот умеет получать штатные PDF-печатные формы из 1С через HTTP-сервис расширения.

## Контракт

Бот отправляет `POST` на URL из `PrintService:Url`.

```json
{
  "entitySet": "Document_СчетНаОплатуПокупателю",
  "refKey": "11111111-1111-1111-1111-111111111111",
  "printForm": "Счет",
  "format": "pdf"
}
```

Сервис должен вернуть один из вариантов:

- `Content-Type: application/pdf` и байты PDF;
- или JSON:

```json
{
  "fileName": "Счет.pdf",
  "contentBase64": "JVBERi0x..."
}
```

## Логика расширения 1С

1. Принять `entitySet`, `refKey`, `printForm`.
2. По `entitySet` определить тип документа.
3. Получить ссылку документа по GUID.
4. Сформировать штатную печатную форму 1С.
5. Сохранить табличный документ в PDF.
6. Вернуть PDF в HTTP-ответе.

## Настройки бота

```json
{
  "Processing": {
    "GeneratePdf": true
  },
  "PrintService": {
    "Enabled": true,
    "Url": "https://your-1c-base/hs/odata-bot-print/print",
    "Login": "user",
    "Password": "password",
    "TimeoutSeconds": 120
  }
}
```

Если сервис выключен, бот продолжит создавать DOCX-формы как запасной вариант.

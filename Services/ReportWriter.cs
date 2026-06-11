using OneCFreshInvoiceODataBot.Models;

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OneCFreshInvoiceODataBot.Services;

public static class ReportWriter
{
    public static string WriteReport(string outputDir, IReadOnlyList<ProcessingResult> results)
    {
        Directory.CreateDirectory(outputDir);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var jsonPath = Path.Combine(outputDir, $"report_{timestamp}.json");
        var csvPath = Path.Combine(outputDir, $"report_{timestamp}.csv");

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(results, options), Encoding.UTF8);

        var sb = new StringBuilder();
        sb.AppendLine("ExcelRow;CounterpartyInn;Nomenclature;Status;InvoiceNumber;InvoiceRefKey;RealizationNumber;RealizationRefKey;IssuedInvoiceNumber;IssuedInvoiceRefKey;Error");
        foreach (var r in results)
        {
            sb.AppendJoin(';',
            [
                r.ExcelRowNumbers?.ToString() ?? string.Empty,
                _Escape(r.CounterpartyInn),
                _Escape(r.NomenclatureNames  ),
                _Escape(r.Status),
                _Escape(r.InvoiceNumber),
                _Escape(r.InvoiceRefKey),
                _Escape(r.RealizationNumber),
                _Escape(r.RealizationRefKey),
                _Escape(r.IssuedInvoiceNumber),
                _Escape(r.IssuedInvoiceRefKey),
                _Escape(r.Error)
            ]).AppendLine();
        }
        File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        return jsonPath;
    }

    private static string _Escape(string? value)
    {
        value ??= string.Empty;
        value = value.Replace("\r", " ").Replace("\n", " ");
        if (value.Contains(';') || value.Contains('"'))
            value = '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}

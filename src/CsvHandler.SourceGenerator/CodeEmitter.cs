using System;
using System.Globalization;
using System.Linq;
using Microsoft.CodeAnalysis;
using CsvHandler.SourceGenerator.Helpers;
using CsvHandler.SourceGenerator.Models;

namespace CsvHandler.SourceGenerator;

/// <summary>
/// Encapsulates code generation logic for CSV reading and writing.
/// </summary>
internal static class CodeEmitter
{
    /// <summary>
    /// Emits the ReadFromCsv method for synchronous CSV reading.
    /// </summary>
    public static void EmitReader(RecordModel model, CodeBuilder code)
    {
        code.AppendXmlDoc("Parses a single CSV line into a record instance.");
        code.AppendLine("/// <param name=\"csvLine\">The UTF-8 encoded CSV line data.</param>");
        code.AppendLine("/// <returns>A new record instance populated from the CSV data.</returns>");
        code.AppendLine($"public static {model.TypeName} ReadFromCsv(global::System.ReadOnlySpan<byte> csvLine)");
        code.OpenBrace();

        // Create parser options
        code.AppendLine($"var options = new global::CsvHandler.Core.Utf8CsvParserOptions");
        code.OpenBrace();
        code.AppendLine($"Delimiter = (byte){model.Delimiter.ToLiteral()},");
        code.AppendLine($"Quote = (byte)'\"',");
        code.AppendLine($"Escape = (byte)'\"',");
        code.AppendLine($"TrimFields = {model.TrimWhitespace.ToString().ToLowerInvariant()},");
        code.AppendLine($"HasHeaders = false,");
        code.AppendLine($"AllowComments = false,");
        code.AppendLine($"Mode = global::CsvHandler.Core.CsvParseMode.{(model.StrictMode ? "Rfc4180" : "Lenient")}");
        code.CloseBrace();
        code.AppendLine(";");
        code.AppendLine();

        // Create parser
        code.AppendLine("var parser = new global::CsvHandler.Core.Utf8CsvParser(csvLine, options);");

        // Create result instance
        code.AppendLine($"var result = new {model.TypeName}();");
        code.AppendLine();

        // Read each field
        for (int i = 0; i < model.Fields.Length; i++)
        {
            var field = model.Fields[i];
            code.AppendLine($"// Field {i}: {field.MemberName}");
            EmitFieldRead(field, code, model, i);
            code.AppendLine();
        }

        code.AppendLine("return result;");
        code.CloseBrace();
        code.AppendLine();
    }

    /// <summary>
    /// Emits the ReadAllFromCsvAsync method for async streaming.
    /// </summary>
    public static void EmitAsyncReader(RecordModel model, CodeBuilder code)
    {
        code.AppendXmlDoc("Reads all records from a CSV stream asynchronously.");
        code.AppendLine("/// <param name=\"stream\">The stream containing CSV data.</param>");
        code.AppendLine("/// <param name=\"skipHeader\">Whether to skip the first line as a header.</param>");
        code.AppendLine("/// <param name=\"cancellationToken\">Cancellation token.</param>");
        code.AppendLine("/// <returns>An async enumerable of record instances.</returns>");

        code.AppendLine($"public static async global::System.Collections.Generic.IAsyncEnumerable<{model.TypeName}> ReadAllFromCsvAsync(");
        code.Indent();
        code.AppendLine("global::System.IO.Stream stream,");
        code.AppendLine($"bool skipHeader = {model.HasHeader.ToString().ToLowerInvariant()},");
        code.AppendLine("[global::System.Runtime.CompilerServices.EnumeratorCancellation] global::System.Threading.CancellationToken cancellationToken = default)");
        code.Unindent();
        code.OpenBrace();

        code.AppendLine("using var reader = new global::System.IO.StreamReader(stream, global::System.Text.Encoding.UTF8);");
        code.AppendLine("bool isFirstLine = true;");
        code.AppendLine("string? line;");
        code.AppendLine();

        code.AppendLine("while ((line = await reader.ReadLineAsync()) != null)");
        code.OpenBrace();

        code.AppendLine("if (cancellationToken.IsCancellationRequested)");
        code.Indent();
        code.AppendLine("yield break;");
        code.Unindent();
        code.AppendLine();

        code.AppendLine("if (isFirstLine && skipHeader)");
        code.OpenBrace();
        code.AppendLine("isFirstLine = false;");
        code.AppendLine("continue;");
        code.CloseBrace();
        code.AppendLine();

        code.AppendLine("isFirstLine = false;");
        code.AppendLine();

        code.AppendLine("if (string.IsNullOrWhiteSpace(line))");
        code.Indent();
        code.AppendLine("continue;");
        code.Unindent();
        code.AppendLine();

        code.AppendLine("yield return ReadFromCsv(global::System.Text.Encoding.UTF8.GetBytes(line));");
        code.CloseBrace();

        code.CloseBrace();
        code.AppendLine();
    }

    /// <summary>
    /// Emits the WriteToCsv method for synchronous CSV writing.
    /// </summary>
    public static void EmitWriter(RecordModel model, CodeBuilder code)
    {
        code.AppendXmlDoc("Writes a record instance to a CSV buffer.");
        code.AppendLine("/// <param name=\"writer\">The buffer writer to write to.</param>");
        code.AppendLine("/// <param name=\"value\">The record instance to serialize.</param>");
        code.AppendLine($"public static void WriteToCsv(global::System.Buffers.IBufferWriter<byte> writer, {model.TypeName} value)");
        code.OpenBrace();

        code.AppendLine("var span = writer.GetSpan(4096);");
        code.AppendLine("int written = 0;");
        code.AppendLine();

        for (int i = 0; i < model.Fields.Length; i++)
        {
            var field = model.Fields[i];

            if (i > 0)
            {
                code.AppendLine("// Write delimiter");
                code.AppendLine($"span[written++] = (byte){model.Delimiter.ToLiteral()};");
                code.AppendLine();
            }

            code.AppendLine($"// Field {i}: {field.MemberName}");
            EmitFieldWrite(field, code, model, i);
            code.AppendLine();
        }

        code.AppendLine("writer.Advance(written);");
        code.CloseBrace();
        code.AppendLine();
    }

    /// <summary>
    /// Emits the WriteToCsvAsync method for async streaming.
    /// </summary>
    public static void EmitAsyncWriter(RecordModel model, CodeBuilder code)
    {
        code.AppendXmlDoc("Writes a record instance to a CSV stream asynchronously.");
        code.AppendLine("/// <param name=\"stream\">The stream to write to.</param>");
        code.AppendLine("/// <param name=\"value\">The record instance to serialize.</param>");
        code.AppendLine("/// <param name=\"cancellationToken\">Cancellation token.</param>");
        code.AppendLine($"public static async global::System.Threading.Tasks.Task WriteToCsvAsync(");
        code.Indent();
        code.AppendLine("global::System.IO.Stream stream,");
        code.AppendLine($"{model.TypeName} value,");
        code.AppendLine("global::System.Threading.CancellationToken cancellationToken = default)");
        code.Unindent();
        code.OpenBrace();

        code.AppendLine("var writer = new global::System.Buffers.ArrayBufferWriter<byte>(4096);");
        code.AppendLine("WriteToCsv(writer, value);");
        code.AppendLine();
        code.AppendLine("await stream.WriteAsync(writer.WrittenMemory, cancellationToken).ConfigureAwait(false);");
        code.AppendLine();
        code.AppendLine("// Write newline");
        code.AppendLine("await stream.WriteAsync(new byte[] { (byte)'\\n' }, cancellationToken).ConfigureAwait(false);");

        code.CloseBrace();
        code.AppendLine();
    }

    /// <summary>
    /// Emits code to read a single field and assign it to a property/field.
    /// </summary>
    private static void EmitFieldRead(FieldModel field, CodeBuilder code, RecordModel model, int fieldIndex)
    {
        var varName = $"fieldData{fieldIndex}";
        code.AppendLine($"if (parser.TryReadField(out var {varName}))");
        code.OpenBrace();

        // Handle nullable fields
        if (field.IsNullable)
        {
            code.AppendLine($"if ({varName}.Length == 0)");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = null;");
            code.CloseBrace();
            code.AppendLine("else");
            code.OpenBrace();
        }

        // Emit type-specific parsing
        EmitFieldParsing(field, code, model, varName);

        if (field.IsNullable)
        {
            code.CloseBrace();
        }

        code.CloseBrace();
    }

    /// <summary>
    /// Emits type-specific parsing code for a field.
    /// </summary>
    private static void EmitFieldParsing(FieldModel field, CodeBuilder code, RecordModel model, string varName)
    {
        var typeSymbol = field.FieldType;

        // Handle nullable wrapper
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            typeSymbol = nullableType.TypeArguments[0];
        }

        var specialType = typeSymbol.SpecialType;
        var typeName = typeSymbol.ToDisplayString();

        // Use custom converter if specified
        if (field.ConverterType != null)
        {
            code.AppendLine($"result.{field.MemberName} = {field.ConverterType.ToDisplayString()}.Parse({varName});");
            return;
        }

        // Handle string specially
        if (specialType == SpecialType.System_String)
        {
            code.AppendLine($"result.{field.MemberName} = global::System.Text.Encoding.UTF8.GetString({varName});");
            return;
        }

        // Use Utf8Parser for numeric types
        string? parseMethod = specialType switch
        {
            SpecialType.System_Boolean => "TryParse",
            SpecialType.System_Byte => "TryParse",
            SpecialType.System_SByte => "TryParse",
            SpecialType.System_Int16 => "TryParse",
            SpecialType.System_UInt16 => "TryParse",
            SpecialType.System_Int32 => "TryParse",
            SpecialType.System_UInt32 => "TryParse",
            SpecialType.System_Int64 => "TryParse",
            SpecialType.System_UInt64 => "TryParse",
            SpecialType.System_Single => "TryParse",
            SpecialType.System_Double => "TryParse",
            SpecialType.System_Decimal => "TryParse",
            SpecialType.System_DateTime => "TryParse",
            _ => null
        };

        if (parseMethod != null)
        {
            EmitUtf8ParserCall(field, code, typeName, field.Format, varName);
        }
        else
        {
            // Handle well-known types
            if (typeName == "System.DateTimeOffset")
            {
                EmitUtf8ParserCall(field, code, "global::System.DateTimeOffset", field.Format, varName);
            }
            else if (typeName == "System.Guid")
            {
                EmitUtf8ParserCall(field, code, "global::System.Guid", null, varName);
            }
            else if (typeName == "System.TimeSpan")
            {
                EmitUtf8ParserCall(field, code, "global::System.TimeSpan", field.Format, varName);
            }
            else
            {
                code.AppendLine($"// Unsupported type: {typeName}");
                code.AppendLine($"result.{field.MemberName} = default;");
            }
        }
    }

    /// <summary>
    /// Emits Utf8Parser call for a field.
    /// </summary>
    private static void EmitUtf8ParserCall(FieldModel field, CodeBuilder code, string typeName, string? format, string varName)
    {
        // DateTime and DateTimeOffset require special handling because Utf8Parser.TryParse doesn't support them properly
        if (typeName == "global::System.DateTime" || typeName == "System.DateTime")
        {
            code.AppendLine($"var dateString = global::System.Text.Encoding.UTF8.GetString({varName});");
            code.AppendLine($"if (global::System.DateTime.TryParse(dateString, out {typeName} parsedValue))");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = parsedValue;");
            code.CloseBrace();
            code.AppendLine("else");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = default!;");
            code.CloseBrace();
        }
        else if (typeName == "global::System.DateTimeOffset" || typeName == "System.DateTimeOffset")
        {
            code.AppendLine($"var dateString = global::System.Text.Encoding.UTF8.GetString({varName});");
            code.AppendLine($"if (global::System.DateTimeOffset.TryParse(dateString, out {typeName} parsedValue))");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = parsedValue;");
            code.CloseBrace();
            code.AppendLine("else");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = default!;");
            code.CloseBrace();
        }
        else
        {
            code.AppendLine($"if (global::System.Buffers.Text.Utf8Parser.TryParse({varName}, out {typeName} parsedValue, out _))");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = parsedValue;");
            code.CloseBrace();
            code.AppendLine("else");
            code.OpenBrace();
            code.AppendLine($"result.{field.MemberName} = default!;");
            code.CloseBrace();
        }
    }

    /// <summary>
    /// Emits code to write a single field to the buffer.
    /// </summary>
    private static void EmitFieldWrite(FieldModel field, CodeBuilder code, RecordModel model, int fieldIndex)
    {
        // Handle nullable check
        if (field.IsNullable)
        {
            code.AppendLine($"if (value.{field.MemberName} == null)");
            code.OpenBrace();
            code.AppendLine("// Write empty field");
            code.CloseBrace();
            code.AppendLine("else");
            code.OpenBrace();
        }

        EmitFieldFormatting(field, code, model, fieldIndex);

        if (field.IsNullable)
        {
            code.CloseBrace();
        }
    }

    /// <summary>
    /// Emits type-specific formatting code for a field.
    /// </summary>
    private static void EmitFieldFormatting(FieldModel field, CodeBuilder code, RecordModel model, int fieldIndex)
    {
        var typeSymbol = field.FieldType;

        // Handle nullable wrapper
        if (typeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullableType)
        {
            typeSymbol = nullableType.TypeArguments[0];
        }

        var specialType = typeSymbol.SpecialType;
        var typeName = typeSymbol.ToDisplayString();
        var varName = $"fieldBytes{fieldIndex}";
        var bytesWrittenVar = $"bytesWritten{fieldIndex}";

        // Use custom converter if specified
        if (field.ConverterType != null)
        {
            code.AppendLine($"var {varName} = {field.ConverterType.ToDisplayString()}.Format(value.{field.MemberName});");
            code.AppendLine($"{varName}.CopyTo(span.Slice(written));");
            code.AppendLine($"written += {varName}.Length;");
            return;
        }

        // Handle string specially
        if (specialType == SpecialType.System_String)
        {
            code.AppendLine($"var {varName} = global::System.Text.Encoding.UTF8.GetBytes(value.{field.MemberName} ?? string.Empty);");
            code.AppendLine($"{varName}.CopyTo(span.Slice(written));");
            code.AppendLine($"written += {varName}.Length;");
            return;
        }

        // Use Utf8Formatter for numeric types
        bool hasUtf8Formatter = specialType switch
        {
            SpecialType.System_Boolean or
            SpecialType.System_Byte or
            SpecialType.System_SByte or
            SpecialType.System_Int16 or
            SpecialType.System_UInt16 or
            SpecialType.System_Int32 or
            SpecialType.System_UInt32 or
            SpecialType.System_Int64 or
            SpecialType.System_UInt64 or
            SpecialType.System_Single or
            SpecialType.System_Double or
            SpecialType.System_Decimal or
            SpecialType.System_DateTime => true,
            _ => typeName is "System.DateTimeOffset" or "System.Guid" or "System.TimeSpan"
        };

        if (hasUtf8Formatter)
        {
            var valueAccess = field.IsNullable ? $"value.{field.MemberName}.Value" : $"value.{field.MemberName}";

            code.AppendLine($"if (global::System.Buffers.Text.Utf8Formatter.TryFormat({valueAccess}, span.Slice(written), out int {bytesWrittenVar}))");
            code.OpenBrace();
            code.AppendLine($"written += {bytesWrittenVar};");
            code.CloseBrace();
        }
        else
        {
            code.AppendLine($"// Unsupported type: {typeName}");
        }
    }
}

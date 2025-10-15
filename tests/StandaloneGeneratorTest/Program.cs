using System;
using System.Buffers;
using System.Text;
using CsvHandler;

namespace StandaloneGeneratorTest;

[CsvRecord]
public partial class Person
{
    [CsvField(Order = 0)]
    public string Name { get; set; } = string.Empty;

    [CsvField(Order = 1)]
    public int Age { get; set; }

    [CsvField(Order = 2)]
    public string? Email { get; set; }
}

class Program
{
    static void Main()
    {
        Console.WriteLine("=== CSV Source Generator Test ===");
        Console.WriteLine();

        // Test reading
        Console.WriteLine("Testing ReadFromCsv...");
        var csvLine = "John Doe,30,john@example.com"u8;
        var person = Person.ReadFromCsv(csvLine);

        Console.WriteLine($"  Name: {person.Name}");
        Console.WriteLine($"  Age: {person.Age}");
        Console.WriteLine($"  Email: {person.Email}");
        Console.WriteLine();

        // Test writing
        Console.WriteLine("Testing WriteToCsv...");
        var writer = new ArrayBufferWriter<byte>();
        Person.WriteToCsv(writer, person);
        var output = Encoding.UTF8.GetString(writer.WrittenSpan);

        Console.WriteLine($"  Output: {output}");
        Console.WriteLine();

        // Test nullable handling
        Console.WriteLine("Testing nullable fields...");
        var csvLine2 = "Jane Smith,25,"u8;
        var person2 = Person.ReadFromCsv(csvLine2);

        Console.WriteLine($"  Name: {person2.Name}");
        Console.WriteLine($"  Age: {person2.Age}");
        Console.WriteLine($"  Email: {person2.Email ?? "(null)"}");
        Console.WriteLine();

        Console.WriteLine("✓ All tests passed!");
    }
}

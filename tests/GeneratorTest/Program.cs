using System;
using System.Buffers;
using System.Text;
using CsvHandler;

namespace GeneratorTest;

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
        Console.WriteLine("Testing CSV Source Generator...");
        Console.WriteLine();

        // Test reading
        var csvLine = "John Doe,30,john@example.com"u8;
        var person = Person.ReadFromCsv(csvLine);

        Console.WriteLine($"Read: {person.Name}, {person.Age}, {person.Email}");
        Console.WriteLine();

        // Test writing
        var writer = new ArrayBufferWriter<byte>();
        Person.WriteToCsv(writer, person);
        var output = Encoding.UTF8.GetString(writer.WrittenSpan);

        Console.WriteLine($"Written: {output}");
        Console.WriteLine();
        Console.WriteLine("Generator test completed successfully!");
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CsvHandler.Core;

namespace CsvHandler.Tests;

/// <summary>
/// Manual implementation of TestCsvContext methods.
/// This provides the GetTypeMetadata and GetTypeHandler methods needed by CsvWriter&lt;T&gt; and CsvReader&lt;T&gt;.
/// </summary>
public partial class TestCsvContext : CsvContext
{
    public override CsvTypeInfo<T>? GetTypeInfo<T>() where T : class
    {
        // Required abstract method - not used in current implementation
        return null;
    }

    public override CsvTypeMetadata<T>? GetTypeMetadata<T>() where T : class
    {
        var type = typeof(T);

        if (type == typeof(Person))
        {
            return (CsvTypeMetadata<T>)(object)GetPersonMetadata();
        }
        else if (type == typeof(Employee))
        {
            return (CsvTypeMetadata<T>)(object)GetEmployeeMetadata();
        }
        else if (type == typeof(Product))
        {
            return (CsvTypeMetadata<T>)(object)GetProductMetadata();
        }

        return null;
    }

    public override ICsvTypeHandler<T>? GetTypeHandler<T>()
    {
        var type = typeof(T);

        if (type == typeof(Person))
        {
            return (ICsvTypeHandler<T>)(object)new PersonTypeHandler();
        }
        else if (type == typeof(Employee))
        {
            return (ICsvTypeHandler<T>)(object)new EmployeeTypeHandler();
        }
        else if (type == typeof(Product))
        {
            return (ICsvTypeHandler<T>)(object)new ProductTypeHandler();
        }
        else if (type == typeof(SimpleRecord))
        {
            return (ICsvTypeHandler<T>)(object)new SimpleRecordTypeHandler();
        }
        else if (type == typeof(ContactRecord))
        {
            return (ICsvTypeHandler<T>)(object)new ContactRecordTypeHandler();
        }
        else if (type == typeof(UnicodeRecord))
        {
            return (ICsvTypeHandler<T>)(object)new UnicodeRecordTypeHandler();
        }
        else if (type == typeof(StatusRecord))
        {
            return (ICsvTypeHandler<T>)(object)new StatusRecordTypeHandler();
        }
        else if (type == typeof(DataTypesRecord))
        {
            return (ICsvTypeHandler<T>)(object)new DataTypesRecordTypeHandler();
        }
        else if (type == typeof(NullableFieldsRecord))
        {
            return (ICsvTypeHandler<T>)(object)new NullableFieldsRecordTypeHandler();
        }

        return null;
    }

    private static CsvTypeMetadata<Person> GetPersonMetadata()
    {
        return new CsvTypeMetadata<Person>(
            typeof(Person),
            new[]
            {
                new CsvFieldMetadata("Name", 0, typeof(string), false),
                new CsvFieldMetadata("Age", 1, typeof(int), true),
                new CsvFieldMetadata("City", 2, typeof(string), false)
            },
            writeValue: (ref Utf8CsvWriter writer, Person value) =>
            {
                writer.WriteField(value.Name);
                writer.WriteField(value.Age);
                writer.WriteField(value.City ?? string.Empty);
            },
            writeHeader: (ref Utf8CsvWriter writer) =>
            {
                writer.WriteField("Name");
                writer.WriteField("Age");
                writer.WriteField("City");
            }
        );
    }

    private static CsvTypeMetadata<Employee> GetEmployeeMetadata()
    {
        return new CsvTypeMetadata<Employee>(
            typeof(Employee),
            new[]
            {
                new CsvFieldMetadata("FullName", 0, typeof(string), false),
                new CsvFieldMetadata("Department", 1, typeof(string), false),
                new CsvFieldMetadata("Salary", 2, typeof(decimal), true),
                new CsvFieldMetadata("HireDate", 3, typeof(DateTime), true),
                new CsvFieldMetadata("IsActive", 4, typeof(bool), true)
            },
            writeValue: (ref Utf8CsvWriter writer, Employee value) =>
            {
                writer.WriteField(value.Name);
                writer.WriteField(value.Department);
                writer.WriteField(value.Salary);
                writer.WriteField(value.HireDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                writer.WriteField(value.IsActive);
            },
            writeHeader: (ref Utf8CsvWriter writer) =>
            {
                writer.WriteField("FullName");
                writer.WriteField("Department");
                writer.WriteField("Salary");
                writer.WriteField("HireDate");
                writer.WriteField("IsActive");
            }
        );
    }

    private static CsvTypeMetadata<Product> GetProductMetadata()
    {
        return new CsvTypeMetadata<Product>(
            typeof(Product),
            new[]
            {
                new CsvFieldMetadata("Id", 0, typeof(int), true),
                new CsvFieldMetadata("Name", 1, typeof(string), false),
                new CsvFieldMetadata("Price", 2, typeof(decimal), true),
                new CsvFieldMetadata("Stock", 3, typeof(int?), false)
            },
            writeValue: (ref Utf8CsvWriter writer, Product value) =>
            {
                writer.WriteField(value.Id);
                writer.WriteField(value.Name);
                writer.WriteField(value.Price);
                if (value.Stock.HasValue)
                    writer.WriteField(value.Stock.Value);
                else
                    writer.WriteField(string.Empty);
            },
            writeHeader: (ref Utf8CsvWriter writer) =>
            {
                writer.WriteField("Id");
                writer.WriteField("Name");
                writer.WriteField("Price");
                writer.WriteField("Stock");
            }
        );
    }

    // Type handlers for reading
    private sealed class PersonTypeHandler : ICsvTypeHandler<Person>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 3;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public Person Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new Person
            {
                Name = fields.Count > 0 ? fields[0] : string.Empty,
                Age = fields.Count > 1 && int.TryParse(fields[1], out var age) ? age : 0,
                City = fields.Count > 2 ? fields[2] : null
            };
        }
    }

    private sealed class EmployeeTypeHandler : ICsvTypeHandler<Employee>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 5;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public Employee Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new Employee
            {
                Name = fields.Count > 0 ? fields[0] : string.Empty,
                Department = fields.Count > 1 ? fields[1] : string.Empty,
                Salary = fields.Count > 2 && decimal.TryParse(fields[2], out var salary) ? salary : 0m,
                HireDate = fields.Count > 3 && DateTime.TryParse(fields[3], out var hireDate) ? hireDate : DateTime.MinValue,
                IsActive = fields.Count > 4 && bool.TryParse(fields[4], out var isActive) ? isActive : false
            };
        }
    }

    private sealed class ProductTypeHandler : ICsvTypeHandler<Product>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 4;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public Product Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new Product
            {
                Id = fields.Count > 0 && int.TryParse(fields[0], out var id) ? id : 0,
                Name = fields.Count > 1 ? fields[1] : string.Empty,
                Price = fields.Count > 2 && decimal.TryParse(fields[2], out var price) ? price : 0m,
                Stock = fields.Count > 3 && int.TryParse(fields[3], out var stock) ? stock : null
            };
        }
    }

    private sealed class SimpleRecordTypeHandler : ICsvTypeHandler<SimpleRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 2;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public SimpleRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new SimpleRecord
            {
                Name = fields.Count > 0 ? fields[0] : string.Empty,
                Description = fields.Count > 1 ? fields[1] : string.Empty
            };
        }
    }

    private sealed class ContactRecordTypeHandler : ICsvTypeHandler<ContactRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 3;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public ContactRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new ContactRecord
            {
                Name = fields.Count > 0 ? fields[0] : string.Empty,
                Email = fields.Count > 1 && !string.IsNullOrWhiteSpace(fields[1]) ? fields[1] : null,
                Phone = fields.Count > 2 && !string.IsNullOrWhiteSpace(fields[2]) ? fields[2] : null
            };
        }
    }

    private sealed class UnicodeRecordTypeHandler : ICsvTypeHandler<UnicodeRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 2;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public UnicodeRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new UnicodeRecord
            {
                Language = fields.Count > 0 ? fields[0] : string.Empty,
                Text = fields.Count > 1 ? fields[1] : string.Empty
            };
        }
    }

    private sealed class StatusRecordTypeHandler : ICsvTypeHandler<StatusRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 2;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public StatusRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new StatusRecord
            {
                Id = fields.Count > 0 && int.TryParse(fields[0], out var id) ? id : 0,
                Status = fields.Count > 1 && Enum.TryParse<Status>(fields[1], out var status) ? status : Status.Inactive
            };
        }
    }

    private sealed class DataTypesRecordTypeHandler : ICsvTypeHandler<DataTypesRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 12;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public DataTypesRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new DataTypesRecord
            {
                ByteValue = fields.Count > 0 && byte.TryParse(fields[0], out var b) ? b : (byte)0,
                ShortValue = fields.Count > 1 && short.TryParse(fields[1], out var s) ? s : (short)0,
                IntValue = fields.Count > 2 && int.TryParse(fields[2], out var i) ? i : 0,
                LongValue = fields.Count > 3 && long.TryParse(fields[3], out var l) ? l : 0L,
                FloatValue = fields.Count > 4 && float.TryParse(fields[4], out var f) ? f : 0f,
                DoubleValue = fields.Count > 5 && double.TryParse(fields[5], out var d) ? d : 0.0,
                DecimalValue = fields.Count > 6 && decimal.TryParse(fields[6], out var dec) ? dec : 0m,
                BoolValue = fields.Count > 7 && bool.TryParse(fields[7], out var bo) ? bo : false,
                CharValue = fields.Count > 8 && fields[8].Length > 0 ? fields[8][0] : '\0',
                GuidValue = fields.Count > 9 && Guid.TryParse(fields[9], out var g) ? g : Guid.Empty,
                DateTimeValue = fields.Count > 10 && DateTime.TryParse(fields[10], out var dt) ? dt : DateTime.MinValue,
                DateTimeOffsetValue = fields.Count > 11 && DateTimeOffset.TryParse(fields[11], out var dto) ? dto : DateTimeOffset.MinValue
            };
        }
    }

    private sealed class NullableFieldsRecordTypeHandler : ICsvTypeHandler<NullableFieldsRecord>
    {
        private string[]? _headers;

        public int ExpectedFieldCount => 5;

        public void SetHeaders(IReadOnlyList<string> headers)
        {
            _headers = headers.ToArray();
        }

        public NullableFieldsRecord Deserialize(IReadOnlyList<string> fields, long lineNumber)
        {
            return new NullableFieldsRecord
            {
                Name = fields.Count > 0 && !string.IsNullOrWhiteSpace(fields[0]) ? fields[0] : null,
                Age = fields.Count > 1 && int.TryParse(fields[1], out var age) ? age : null,
                BirthDate = fields.Count > 2 && DateTime.TryParse(fields[2], out var bd) ? bd : null,
                Salary = fields.Count > 3 && decimal.TryParse(fields[3], out var salary) ? salary : null,
                IsActive = fields.Count > 4 && bool.TryParse(fields[4], out var isActive) ? isActive : null
            };
        }
    }
}

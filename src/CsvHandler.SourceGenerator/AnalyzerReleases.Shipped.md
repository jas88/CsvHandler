## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSV001  | CsvHandler | Error | Types decorated with [CsvRecord] must be declared as partial
CSV002  | CsvHandler | Error | Multiple fields have the same order value
CSV003  | CsvHandler | Error | Field type is not supported for CSV serialization
CSV004  | CsvHandler | Error | CsvRecord cannot be applied to nested types
CSV005  | CsvHandler | Warning | No CSV fields found in record
CSV006  | CsvHandler | Error | Field order must be non-negative
CSV007  | CsvHandler | Error | Custom converter type is invalid
CSV008  | CsvHandler | Error | Field name cannot be empty
CSV009  | CsvHandler | Warning | Duplicate field names detected
CSV010  | CsvHandler | Error | Type must be a class or record

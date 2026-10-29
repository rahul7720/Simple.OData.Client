# Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) (`--job short`: 3 warm-up and 3 measured iterations) from `src/Simple.OData.Client.Benchmarks`.

```bash
dotnet run -c Release --project src/Simple.OData.Client.Benchmarks -- --filter '*' --job short
```

Environment of the recorded run:

```
BenchmarkDotNet v0.15.8, Linux Ubuntu 24.04.5 LTS (Noble Numbat)
Intel Xeon Processor 2.80GHz, 1 CPU, 4 logical and 4 physical cores
.NET SDK 10.0.403
  [Host]   : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4
  ShortRun : .NET 10.0.14 (10.0.14, 10.0.1426.52326), X64 RyuJIT x86-64-v4

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3
```

## Feed materialization

Reading a canned 20-person TripPin feed (each person with expanded trips and friends, about 60 KB of JSON) served by an in-memory `HttpMessageHandler`; metadata is preloaded. Untyped results are dictionaries, typed results are POCOs, dynamic results are `ODataEntry` objects.

| Method                   | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|------------------------- |----------:|----------:|----------:|------:|--------:|---------:|--------:|----------:|------------:|
| Untyped                  |  9.628 ms | 13.567 ms | 0.7436 ms |  1.00 |    0.10 | 203.1250 | 15.6250 |   3.55 MB |        1.00 |
| Typed                    | 10.238 ms |  4.765 ms | 0.2612 ms |  1.07 |    0.08 | 203.1250 | 93.7500 |   3.59 MB |        1.01 |
| Dynamic                  |  8.652 ms |  4.048 ms | 0.2219 ms |  0.90 |    0.07 | 203.1250 | 62.5000 |   3.56 MB |        1.00 |
| TypedWithFeedAnnotations |  9.889 ms | 15.388 ms | 0.8434 ms |  1.03 |    0.10 | 187.5000 | 31.2500 |   3.59 MB |        1.01 |
| UntypedThroughClientApi  | 10.026 ms | 28.976 ms | 1.5883 ms |  1.05 |    0.16 | 203.1250 | 15.6250 |   3.55 MB |        1.00 |

## URL building

Turning fluent commands into request URLs with the TripPin metadata preloaded; nothing is sent.

| Method                       | Mean       | Error      | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|----------------------------- |-----------:|-----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| UntypedFilterExpandSelect    |  11.418 μs |   1.683 μs |  0.0923 μs |  1.00 |    0.01 | 0.5798 |      - |   9.93 KB |        1.00 |
| TypedFilterExpandSelect      | 186.197 μs | 362.653 μs | 19.8783 μs | 16.31 |    1.51 | 0.9766 | 0.4883 |  22.07 KB |        2.22 |
| DynamicFilterExpandSelect    |  10.474 μs |   7.861 μs |  0.4309 μs |  0.92 |    0.03 | 0.7172 |      - |   12.3 KB |        1.24 |
| TypedKeyLookup               |   1.818 μs |   4.446 μs |  0.2437 μs |  0.16 |    0.02 | 0.2079 |      - |   3.52 KB |        0.35 |
| TypedNestedExpandWithOptions |  16.878 μs |  49.876 μs |  2.7339 μs |  1.48 |    0.21 | 0.6104 |      - |  11.55 KB |        1.16 |

## Expression translation

Translating LINQ and dynamic filter expressions into `$filter` text.

| Method                  | Mean       | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------ |-----------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| SimpleComparison        |   5.406 μs |   1.311 μs |  0.0719 μs |   1.00 |    0.02 | 0.2747 |      - |      5 KB |        1.00 |
| StringFunctionsAndLogic |  22.402 μs |  33.706 μs |  1.8475 μs |   4.14 |    0.30 | 0.6104 |      - |  11.46 KB |        2.29 |
| NavigationAnyAll        | 154.818 μs | 392.727 μs | 21.5267 μs |  28.64 |    3.46 | 0.9766 | 0.4883 |  20.71 KB |        4.14 |
| DateAndArithmetic       | 551.323 μs | 580.274 μs | 31.8068 μs | 102.00 |    5.23 |      - |      - |  14.12 KB |        2.82 |
| DynamicExpression       |   7.234 μs |   1.217 μs |  0.0667 μs |   1.34 |    0.02 | 0.4959 |      - |   8.38 KB |        1.68 |

## Observations

- Materializing a feed costs about the same for dictionaries, POCOs and dynamic entries: the time is spent in ODataLib's
  JSON reader, not in the client's mapping layer.
- Dynamic expressions and untyped strings translate in about 10 μs. Typed LINQ expressions pay for `Expression.Compile`
  when constant sub-expressions (dates, arithmetic on captured values) are evaluated; a cheap interpreter for
  constant folding is the obvious next optimization.
- Short-run numbers carry wide error bars (3 iterations); use the default job for comparisons between versions.

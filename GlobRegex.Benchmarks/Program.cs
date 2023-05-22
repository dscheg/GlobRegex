// SPDX-FileCopyrightText: (C) 2023 Dmitriy Titarenko https://github.com/dscheg
// SPDX-License-Identifier: BSD-3-Clause

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace GlobRegex.Benchmarks;

[MemoryDiagnoser]
public class Program
{
    static void Main()
        => BenchmarkRunner.Run<Program>();

    [Benchmark]
    public void ToRegexPattern()
        => GlobConvert.ToRegexPattern("/home/user/sources/test??/*/files_*/**/*.");
}

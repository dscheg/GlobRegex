# GlobRegex &ensp;[![License](https://img.shields.io/github/license/dscheg/GlobRegex.svg)](https://raw.githubusercontent.com/dscheg/GlobRegex/main/LICENSE)
A simple fully tested Glob to Regex conversion library for .NET with no external dependencies targeting netstadard2.0

[![Build and test](https://github.com/dscheg/GlobRegex/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/dscheg/GlobRegex/actions/workflows/build-and-test.yml)
[![NuGet](https://img.shields.io/nuget/v/GlobRegex.svg)](https://www.nuget.org/packages/GlobRegex/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/GlobRegex.svg)](https://www.nuget.org/packages/GlobRegex)

Glob is a pattern that defines a set of file system entries using wildcards. `GlobRegex` allows you to convert a glob into a regular expression pattern splitting the glob into a base directory from which a recursive traversal can be started, and a stem that can be used to match a relative path.

## Usage
```csharp
var glob = GlobConvert.ToRegexPattern("../usr/doc?/**/*.txt", GlobRegexOptions.MatchFullString);
var regex = new Regex(glob.StemRegexPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
Directory.EnumerateFiles(glob.BasePath, "*", new EnumerationOptions {RecurseSubdirectories = true})
    .Select(path => Path.GetRelativePath(glob.BasePath, path))
    .Where(path => regex.IsMatch(path))
    .ForEach(Console.WriteLine);
```

## Patterns
The following patterns are supported:

| Pattern | Description | Example |
|:-------:| ----------- | ------- |
| `?`     | Any single character except the directory separator | `202305??.log` |
| `*`     | Zero or more of any characters except the directory separator | `src/*.md` |
| `**/`   | Zero or more path segments ending with the directory separator | `**/bin/` |
| `**`    | Equivalent of `**/*`, only valid at the **beginning of the segment**, if it occurs not at the beginning of the segment it is interpreted as consecutive `*` | `**`, `**.cs` |
| `*.`    | Trailing dot changes the behavior of the wildcards (`?`, `*`, `**`) of the **last segment** of the path to not include a dot, i.e. allows to match file system entries without extension. If there are no wildcards in the last segment, the trailing dot is treated as a regular character | `test/*.`, `split/x?a.` |

Both `/` and `\` are treated as directory separator characters. So `**/*` is the same as `**\*`.
Consecutive `*` after `**` (and `**` if not at the beginning of the segment) are treated as a single `*`. So `***/abc***` is the same as `**/abc*`.
Escaping of wildcard characters is not supported.

The passed glob is not normalized, in particular `./` and `../`, consecutive directory separators like `//` or `\\`, etc. processed literally. `./` and `../` in normal use are expected to appear only in the `BasePath` and can be normalized afterwards. In the usage example above `Path.GetRelativePath` method resolve paths by calling the `GetFullPath` normalization method before calculating the difference.

## Reproducible builds
`GlobRegex` nuget package is built using [ReproducibleBuilds](https://github.com/dotnet/reproducible-builds) with the [SourceLink](https://github.com/dotnet/sourcelink). Reproducible builds give confidence by allowing to validate that the package has actually been built using public sources. To be able to reproduce a build, you need the source files, the referenced DLLs, the compiler version, and the compiler options (language version, defines, nullables, etc.). All this information is available using [Nuget Package Explorer](https://nuget.info/packages/GlobRegex). Or you can use [dotnet-validate](https://www.nuget.org/packages/dotnet-validate) tool to validate the package.

## Provenance attestation
A [provenance attestation](https://docs.github.com/en/actions/concepts/security/artifact-attestations) is configured for `GlobRegex` nuget package. The attestation is a JSON document describing the build environment and the resulting artifact, which is signed and stored publicly. All attestations are available at [Attestations Page](https://github.com/dscheg/GlobRegex/attestations).
Using `gh` you can verify `nupkg` file. The tricky part is that `nuget.org` changes the package by adding signatures during publication, but you can still normalize the package before validation.

```sh
# Delete file(s) that nuget.org adds to the package
zip -d globregex.VERSION.nupkg .signature.p7s

# Run attestation verification for the package
gh attestation verify --repo dscheg/GlobRegex globregex.VERSION.nupkg

# ✓ Verification succeeded!
```

## Author
Copyright (c) Dmitriy [dscheg](https://github.com/dscheg) Titarenko 2023-2026

**GlobRegex** is distributed under [BSD 3-Clause License](LICENSE)

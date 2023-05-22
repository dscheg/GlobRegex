// SPDX-FileCopyrightText: (C) 2023 Dmitriy Titarenko https://github.com/dscheg
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly:InternalsVisibleTo("GlobRegex.Tests")]

namespace GlobRegex;

public static class GlobConvert
{
    /// <summary>
    /// Converts an input glob to a regular expression pattern. All segments starting with the first containing wildcards
    /// are wrapped in a named matched subexpression <c>(?&lt;stem&gt;...)</c>, the captured value of this named group can
    /// be used to determine the relative path. Stem can be empty if the glob contains no wildcards. Additionally the base
    /// path is returned, relative to which the relative path is calculated.
    /// <para>In order to get expected behavior the path that is tested against the resulting regular expression should be
    /// normalized (trimmed spaces, evaluated '.' and '..' relative directory components, ...). This can be done for example
    /// using <c>Path.GetFullPath</c>. See more: https://learn.microsoft.com/dotnet/standard/io/file-path-formats#path-normalization</para>
    /// </summary>
    /// <param name="glob">The wildcard pattern; '?', '*', '**/', '**', '*.' patterns are supported:
    /// <list type="bullet">
    /// <item><description><c>'?'</c> – any single character except the path separator</description></item>
    /// <item><description><c>'*'</c> – any number of any characters except the path separator</description></item>
    /// <item><description><c>'**/'</c> – zero or more path segments ending with the path separator</description></item>
    /// <item><description><c>'**'</c> – equivalent of <c>'**/*'</c>, only valid at the beginning of the segment</description></item>
    /// <item><description><c>'*.'</c> – when used at the end of a glob changes the behavior of the wildcards of the last
    /// segment of the path in order to not include a dot, i.e. allows to match file system entries without extension</description></item>
    /// </list>
    /// </param>
    /// <param name="options">A bitwise combination of the enumeration values that changes the conversion process</param>
    /// <returns>A <c>Glob</c> with the following properties:<br/>
    /// <list type="bullet">
    /// <item><description>
    /// <c>BasePath</c> – the prefix of the original glob ending with path separator which doesn't contain any wildcard.
    /// This prefix can be used to determine the starting directory for a recursive directory traversal. <c>BasePath</c>
    /// can be empty if the glob starts with a wildcard segment or a globstar
    /// </description></item>
    /// <item><description><c>RegexPattern</c> – the regular expression pattern for the glob</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">input glob is null</exception>
    public static Glob ToRegexPattern(string glob, GlobRegexOptions options = GlobRegexOptions.MatchFullString | GlobRegexOptions.AllowGlobstarPrefixWithoutPathSeparator)
    {
        var parts = ToRegexPatternParts(glob, options).ToList();

        int baseCount = 0;
        for(int i = 0; i < parts.Count && parts[i].Type is not (PartType.Globstar or PartType.Wildcard); i++)
            baseCount = parts[i].Type == PartType.PathSeparator ? i + 1 : baseCount;

        var regexParts = parts.Take(baseCount).Select(part => part.RegexPattern)
            .Append("(?<stem>")
            .Concat(parts.Skip(baseCount).Select(part => part.RegexPattern))
            .Append(")");

        if((options & GlobRegexOptions.MatchFullString) != 0)
            regexParts = regexParts.Append("$").Prepend("^");

        return new Glob
        {
            BasePath = string.Concat(parts.Take(baseCount).Select(part => part.Raw)),
            RegexPattern = string.Concat(regexParts)
        };
    }

    /// <summary>
    /// Splits an input glob to parts and converts each part into a regular expression pattern. The returned collection
    /// contains as items both path segments and path separators
    /// <example>
    /// "src/test/**/bin/*.json" => "src", "/", "test", "**/", "bin", "/", "*.json"
    /// </example>
    /// </summary>
    /// <param name="glob">The wildcard pattern; '?', '*', '**/', '**', '*.' patterns are supported:
    /// <list type="bullet">
    /// <item><description><c>'?'</c> – any single character except the path separator</description></item>
    /// <item><description><c>'*'</c> – any number of any characters except the path separator</description></item>
    /// <item><description><c>'**/'</c> – zero or more path segments ending with the path separator</description></item>
    /// <item><description><c>'**'</c> – equivalent of <c>'**/*'</c>, only valid at the beginning of the segment</description></item>
    /// <item><description><c>'*.'</c> – when used at the end of a glob changes the behavior of the wildcards of the last
    /// segment of the path in order to not include a dot, i.e. allows to match file system entries without extension</description></item>
    /// </list>
    /// </param>
    /// <param name="options">A bitwise combination of the enumeration values that changes the conversion process</param>
    /// <returns>A lazy enumerable of <c>GlobPart</c> items with the following properties:<br/>
    /// <list type="bullet">
    /// <item><description><c>Raw</c> – the original part (segment, globstar or path separator) of the input glob string</description></item>
    /// <item><description><c>Type</c> – the type of the part</description></item>
    /// <item><description><c>RegexPattern</c> – the regular expression pattern for the part</description></item>
    /// </list>
    /// </returns>
    /// <exception cref="ArgumentNullException">input glob is null</exception>
    public static IEnumerable<GlobPart> ToRegexPatternParts(string glob, GlobRegexOptions options)
    {
        if(glob == null)
            throw new ArgumentNullException(nameof(glob));

        var noExt = EndsWithNoExtension(glob);
        if(noExt) glob = glob.Substring(0, glob.Length - 1);

        Func<(string, bool IsLast, PartType), bool> isLastPartNoExt = noExt
            ? part => part.IsLast
            : _ => false;

        var parts = SplitLazy(glob, PathSeparators)
            // combine '**' and the following path separator to a single globstar item '**/'
            .GroupAdjacent((prev, curr) => prev.Value.Length >= 2 && prev.Value.HasOnly('*') && curr.IsSeparator)
            .Select(part => part.Count == 2
                ? (Raw: part.First.Value + part.Last.Value, part.First.IsLast, Type: PartType.Globstar)
                : (Raw: part.Last.Value, part.Last.IsLast, Type: part.First.IsSeparator
                    ? PartType.PathSeparator
                    : part.Last.Value.HasAny(GlobSpecialChars)
                        ? PartType.Wildcard
                        : PartType.Plain));

        // split the segment prefixed '**' into a Globstar and '*suffix'
        if((options & GlobRegexOptions.AllowGlobstarPrefixWithoutPathSeparator) != 0)
            parts = parts.SelectMany(part => part.Type != PartType.Globstar && part.Raw.TryGetGlobstarPrefix(out var prefix, out var suffix)
                ? Yield(
                    (Raw: prefix, false, Type: PartType.Globstar),
                    (Raw: suffix, part.IsLast, Type: PartType.Wildcard))
                : Yield(part));

        return parts.Select(part => new GlobPart
        {
            Raw = part.Raw,
            Type = part.Type,
            RegexPattern = part.Type switch
            {
                PartType.PathSeparator => @"[/\\]",
                PartType.Globstar      => @"(?:[^/\\]*[/\\])*",
                PartType.Plain         => Regex.Escape(part.Raw),
                PartType.Wildcard      => string.Concat(SplitLazy(part.Raw, GlobSpecialChars)
                    .GroupAdjacent((prev, item) => prev.Value == "*" && item.Value == "*")
                    .Select(value => value.Last.Value switch
                    {
                        "*"     => isLastPartNoExt(part) ? @"[^/\\.]*" : @"[^/\\]*",
                        "?"     => isLastPartNoExt(part) ? @"[^/\\.]" : @"[^/\\]",
                        var str => Regex.Escape(str)
                    })),
                _ => throw new ArgumentOutOfRangeException(nameof(PartType))
            }
        });
    }

    internal static IEnumerable<(string Value, bool IsSeparator, bool IsLast)> SplitLazy(this string value, params char[] delims)
    {
        int start = 0, end;
        while((end = value.IndexOfAny(delims, start)) >= 0)
        {
            if(end != start) yield return (value.Substring(start, end - start), IsSeparator: false, IsLast: false);
            yield return (value.Substring(end, 1), IsSeparator: true, IsLast: false);
            start = end + 1;
        }

        if(value.Length != start) yield return (value.Substring(start, value.Length - start), IsSeparator: false, IsLast: true);
    }

    internal static IEnumerable<(T First, T Last, int Count)> GroupAdjacent<T>(this IEnumerable<T> enumerable, Func<T, T, bool> adjacent)
    {
        using var enumerator = enumerable.GetEnumerator();
        if(!enumerator.MoveNext())
            yield break;

        T first = enumerator.Current;
        T item = first;

        int count = 1;
        while(enumerator.MoveNext())
        {
            T prev = item;
            item = enumerator.Current;
            if(adjacent(prev, item))
            {
                count++;
                continue;
            }

            yield return (first, prev, count);
            first = item;
            count = 1;
        }

        yield return (first, item, count);
    }

    private static IEnumerable<T> Yield<T>(this T item) { yield return item; }

    private static IEnumerable<T> Yield<T>(T item1, T item2)
    {
        yield return item1;
        yield return item2;
    }

    private static bool EndsWithNoExtension(string val)
        => val.Length > 1 && val[val.Length - 1] == '.' && PathSeparators.All(c => val[val.Length - 2] != c);

    private static bool TryGetGlobstarPrefix(this string value, out string prefix, out string suffix)
    {
        var idx = value.IndexOfAnyExcept('*');
        idx = idx < 0 ? value.Length : idx;
        if(idx < 2)
        {
            (prefix, suffix) = (string.Empty, value);
            return false;
        }
        (prefix, suffix) = (value.Substring(0, idx), value.Substring(idx - 1)); //suffix starts with '*'
        return true;

    }

    private static bool HasOnly(this string value, char chr)
        => value.IndexOfAnyExcept(chr) < 0;

    private static bool HasAny(this string value, char[] chars)
        => value.IndexOfAny(chars) >= 0;

    private static int IndexOfAnyExcept(this string value, char chr)
    {
        for(var i = 0; i < value.Length; i++)
        {
            if(value[i] != chr)
                return i;
        }

        return -1;
    }

    private static readonly char[] GlobSpecialChars = { '*', '?' };
    private static readonly char[] PathSeparators = { '/', '\\' };
}

[Flags]
public enum GlobRegexOptions
{
    /// <summary>Adds <c>'^'</c> to the beginning of the resulting regex pattern and <c>'$'</c> to the end</summary>
    MatchFullString = 1,
    /// <summary>Allows segment <c>'**suffix'</c> to be interpreted <c>globstar</c> + <c>'*suffix'</c></summary>
    AllowGlobstarPrefixWithoutPathSeparator = 2
}

public enum PartType
{
    /// <summary>One or more consecutive '/' or '\' characters</summary>
    PathSeparator = 1,
    /// <summary>A segment that contains one or more '*' or '?' characters</summary>
    Wildcard = 2,
    /// <summary>'**' or '**/' matching zero or more directories or subdirectories</summary>
    Globstar = 3,
    /// <summary>A segment that contains only escaped plain text without wildcards</summary>
    Plain = 4
}

public struct Glob
{
    /// <summary>
    /// The prefix of the original glob ending with path separator which doesn't contain any wildcard. This prefix can be
    /// used to determine the starting directory for a recursive directory traversal. <c>BasePath</c> can be empty if the
    /// glob starts with a wildcard segment or a globstar
    /// </summary>
    public string BasePath { get; internal set; }
    /// <summary>
    /// The regular expression pattern for the entire glob. All segments starting with the first containing wildcards
    /// are wrapped in a named matched subexpression <c>(?&lt;stem&gt;...)</c>, the captured value of this named group can
    /// be used to determine the relative path. Stem can be empty if the glob contains no wildcards
    /// </summary>
    public string RegexPattern { get; internal set; }
}

public struct GlobPart
{
    /// <summary>The original part (segment, globstar or path separator) of the input glob string</summary>
    public string Raw { get; internal set; }
    /// <summary>The type of the part</summary>
    public PartType Type { get; internal set; }
    /// <summary>The regular expression pattern for the part</summary>
    public string RegexPattern { get; internal set; }
}

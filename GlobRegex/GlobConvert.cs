// SPDX-FileCopyrightText: (C) 2023 Dmitriy Titarenko https://github.com/dscheg
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly:InternalsVisibleTo("GlobRegex.Tests")]

namespace GlobRegex;

/// <summary>Converts a glob pattern to a regular expression, extracting the base plain path and wildcard relative stem</summary>
public static class GlobConvert
{
    /// <summary>
    /// Converts an input glob to a regular expression pattern. All segments starting with the first containing wildcards
    /// are wrapped in a named matched subexpression <c>(?&lt;stem&gt;...)</c>, the captured value of this named group can
    /// be used to determine the relative path. Stem can be empty if the glob contains no wildcards. Additionally the base
    /// path is returned, relative to which the relative path is calculated.
    /// <para>The passed glob is not normalized, in particular <c>./</c> and <c>../</c>, consecutive path separators like
    /// <c>//</c> or <c>\\</c>, etc. processed literally. Usually <c>./</c> and <c>../</c> are expected to appear only in
    /// the `BasePath` and can be normalized afterwards by using for example <c>Path.GetFullPath</c></para>
    /// </summary>
    /// <param name="glob">The wildcard pattern; '?', '*', '**/', '**', '*.' patterns are supported:
    /// <list type="bullet">
    /// <item><description><c>'?'</c> – any single character except the path separator</description></item>
    /// <item><description><c>'*'</c> – any number of any characters except the path separator</description></item>
    /// <item><description><c>'**/'</c> – zero or more path segments ending with the path separator</description></item>
    /// <item><description><c>'**'</c> – equivalent of <c>'**/*'</c>, only valid at the beginning of the segment</description></item>
    /// <item><description><c>'*.'</c> – trailing dot changes the behavior of the wildcards of the last segment of the path
    /// in order to not include a dot, i.e. allows to match file system entries without extension</description></item>
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
    public static Glob ToRegexPattern(string glob, GlobRegexOptions options)
    {
        var parts = ToRegexPatternParts(glob, options).ToList();

        int baseCount = 0;
        for(int i = 0; i < parts.Count && parts[i].Type is not (GlobPartType.Globstar or GlobPartType.Wildcard); i++)
            baseCount = parts[i].Type == GlobPartType.PathSeparator ? i + 1 : baseCount;

        IEnumerable<string> stemRegexParts = parts.Skip(baseCount).Select(part => part.RegexPattern).ToList();
        var regexParts = parts.Take(baseCount).Select(part => part.RegexPattern)
            .Append("(?<stem>")
            .Concat(stemRegexParts)
            .Append(")");

        if((options & GlobRegexOptions.MatchFullString) != 0)
        {
            regexParts = regexParts.Append("$").Prepend("^");
            stemRegexParts = stemRegexParts.Append("$").Prepend("^");
        }

        return new Glob
        {
            BasePath = string.Concat(parts.Take(baseCount).Select(part => part.Raw)),
            RegexPattern = string.Concat(regexParts),
            StemRegexPattern = string.Concat(stemRegexParts)
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
    /// <item><description><c>'*.'</c> – trailing dot changes the behavior of the wildcards of the last segment of the path
    /// in order to not include a dot, i.e. allows to match file system entries without extension</description></item>
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

        var noExt = (options & GlobRegexOptions.WildcardsWithTrailingDotMatchNoExtension) != 0 && glob.EndsWith(".", StringComparison.Ordinal);
        var parts = SplitLazy(glob, PathSeparators)
            // combine '**' and the following path separator to a single globstar item '**/'
            .GroupAdjacent((prev, curr) => prev.Value.Length >= 2 && prev.Value.HasOnly('*') && curr.IsSeparator)
            .Select(part => part.Count == 2
                ? (Raw: part.First.Value + part.Last.Value, part.First.IsLast, Type: GlobPartType.Globstar)
                : (Raw: part.Last.Value, part.Last.IsLast, Type: part.First.IsSeparator
                    ? GlobPartType.PathSeparator
                    : part.Last.Value.HasAny(GlobSpecialChars)
                        ? GlobPartType.Wildcard
                        : GlobPartType.Plain));

        // split the segment prefixed '**' into a Globstar and '*suffix'
        if((options & GlobRegexOptions.AllowGlobstarPrefixWithoutPathSeparator) != 0)
            parts = parts.SelectMany(part => part.Type != GlobPartType.Globstar && part.Raw.TryGetGlobstarPrefix(out var prefix, out var suffix)
                ? Yield(
                    (Raw: prefix, false, Type: GlobPartType.Globstar),
                    (Raw: suffix, part.IsLast, Type: GlobPartType.Wildcard))
                : Yield(part));

        return parts.Select(part => new GlobPart
        {
            Raw = part.Raw,
            Type = part.Type,
            RegexPattern = part.Type switch
            {
                GlobPartType.PathSeparator => @"[/\\]",
                GlobPartType.Globstar      => @"(?:[^/\\]*[/\\])*",
                GlobPartType.Plain         => Regex.Escape(part.Raw),
                GlobPartType.Wildcard      => string.Concat(SplitLazy(part.Raw, GlobSpecialChars, part.Raw.Length - (part.IsLast && noExt ? 1 : 0))
                    .GroupAdjacent((prev, item) => prev.Value == "*" && item.Value == "*")
                    .Select(value => value.Last.Value switch
                    {
                        "*"     => part.IsLast && noExt ? @"[^/\\.]*" : @"[^/\\]*",
                        "?"     => part.IsLast && noExt ? @"[^/\\.]" : @"[^/\\]",
                        var str => Regex.Escape(str)
                    })),
                _ => throw new ArgumentOutOfRangeException(nameof(GlobPartType))
            }
        });
    }

    internal static IEnumerable<(string Value, bool IsSeparator, bool IsLast)> SplitLazy(this string value, char[] separators, int length = int.MaxValue)
    {
        int start = 0, end;
        length = Math.Min(length, value.Length);
        while((end = value.IndexOfAny(separators, start, length - start)) >= 0)
        {
            if(end != start) yield return (value.Substring(start, end - start), IsSeparator: false, IsLast: false);
            yield return (value.Substring(end, 1), IsSeparator: true, IsLast: false);
            start = end + 1;
        }

        if(length != start) yield return (value.Substring(start, length - start), IsSeparator: false, IsLast: true);
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

    private static bool TryGetGlobstarPrefix(this string value, out string prefix, out string suffix)
    {
        var idx = value.IndexOfAnyExcept('*');
        idx = idx < 0 ? value.Length : idx;
        if(idx < 2)
        {
            (prefix, suffix) = (string.Empty, value);
            return false;
        }
        (prefix, suffix) = (value.Substring(0, idx - 1), value.Substring(idx - 1)); //suffix starts with '*'
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

/// <summary>A bitwise combination of the enumeration values that changes the conversion process</summary>
[Flags]
public enum GlobRegexOptions
{
    /// <summary>Adds <c>'^'</c> to the beginning of the resulting regex pattern and <c>'$'</c> to the end</summary>
    MatchFullString = 1,
    /// <summary>Allows segment <c>'**suffix'</c> to be interpreted as <c>globstar</c> + <c>'*suffix'</c></summary>
    AllowGlobstarPrefixWithoutPathSeparator = 2,
    /// <summary>Trailing dot changes the behavior of the wildcards of the last segment of the path in order to not include
    /// a dot, i.e. allows to match file system entries without extension. If there are no wildcards in the last segment,
    /// the trailing dot is treated as a regular character</summary>
    WildcardsWithTrailingDotMatchNoExtension = 4
}

/// <summary>Type of the part of the glob</summary>
public enum GlobPartType
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

/// <summary>A conversion result</summary>
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
    /// <summary>
    /// The regular expression pattern for the stem part only. Stem starts with the first segment containing wildcards.
    /// Stem regex pattern can be empty if the glob contains no wildcards
    /// </summary>
    public string StemRegexPattern { get; internal set; }
}

/// <summary>The part of the glob</summary>
public struct GlobPart
{
    /// <summary>The original part (segment, globstar or path separator) of the input glob string</summary>
    public string Raw { get; internal set; }
    /// <summary>The type of the part</summary>
    public GlobPartType Type { get; internal set; }
    /// <summary>The regular expression pattern for the part</summary>
    public string RegexPattern { get; internal set; }
}

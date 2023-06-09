// SPDX-FileCopyrightText: (C) 2023 Dmitriy Titarenko https://github.com/dscheg
// SPDX-License-Identifier: BSD-3-Clause

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GlobRegex.Tests;

[TestClass]
public class GlobRegexTests
{
    [TestMethod]
    //       INPUT glob           EXPECTED parts
    [DataRow(@"")]
    [DataRow(@"**/",              "G:**/")]
    [DataRow(@"**",               "W:**")]
    [DataRow(@"**ab.c",           "W:**ab.c")]
    [DataRow(@"**/ab.c",          "G:**/", "P:ab.c")]
    [DataRow(@"**/**.",           "G:**/", "W:**.")]
    [DataRow(@"**/**/a/**/**.",   "G:**/", "G:**/", "P:a", "S:/", "G:**/", "W:**.")]
    public void TestToRegexPatternPartsZeroOptions(string glob, params string[] parts)
        => AssertParts(GlobConvert.ToRegexPatternParts(glob, 0).ToList(), parts);

    [TestMethod]
    //       INPUT glob           EXPECTED parts
    [DataRow(@"")]
    [DataRow(@"/",                "S:/")]
    [DataRow(@"a",                "P:a")]
    [DataRow(@"?",                "W:?")]
    [DataRow(@"*",                "W:*")]
    [DataRow(@"**/",              "G:**/")]
    [DataRow(@"**",               "G:*", "W:*")]
    [DataRow(@"?.",               "W:?.")]
    [DataRow(@"*.",               "W:*.")]
    [DataRow(@"///",              "S:/", "S:/", "S:/")]
    [DataRow(@"a/b/c/",           "P:a", "S:/", "P:b", "S:/", "P:c", "S:/")]
    [DataRow(@"/a/b/c",           "S:/", "P:a", "S:/", "P:b", "S:/", "P:c")]
    [DataRow(@"/a/b**c",          "S:/", "P:a", "S:/", "W:b**c")]
    [DataRow(@"**ab.c",           "G:*", "W:*ab.c")]
    [DataRow(@"**/ab.c",          "G:**/", "P:ab.c")]
    [DataRow(@"**/**.",           "G:**/", "G:*", "W:*.")]
    [DataRow(@"**/**/a/**/**.",   "G:**/", "G:**/", "P:a", "S:/", "G:**/", "G:*", "W:*.")]
    public void TestToRegexPatternPartsAllOptions(string glob, params string[] parts)
        => AssertParts(GlobConvert.ToRegexPatternParts(glob, (GlobRegexOptions)(~0)).ToList(), parts);

    private void AssertParts(IList<GlobPart> result, string[] parts)
    {
        Assert.IsTrue(parts.Select(part => part.Split(':')[1]).SequenceEqual(result.Select(part => part.Raw)));
        Assert.IsTrue(parts.Select(part => part.Split(':')[0] switch
        {
            "S" => GlobPartType.Separator,
            "W" => GlobPartType.Wildcard,
            "G" => GlobPartType.Globstar,
            "P" => GlobPartType.Plain,
            _   => default
        }).SequenceEqual(result.Select(part => part.Type)));
    }

    [TestMethod]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"",                 @"",                 @"^(?<stem>)$")]
    [DataRow(@"/",                @"/",                @"^[/\\](?<stem>)$")]
    [DataRow(@"\",                @"\",                @"^[/\\](?<stem>)$")]
    [DataRow(@"a",                @"",                 @"^(?<stem>a)$")]
    [DataRow(@"/a",               @"/",                @"^[/\\](?<stem>a)$")]
    [DataRow(@"a/",               @"a/",               @"^a[/\\](?<stem>)$")]
    [DataRow(@"/a/",              @"/a/",              @"^[/\\]a[/\\](?<stem>)$")]
    [DataRow(@"/a/b",             @"/a/",              @"^[/\\]a[/\\](?<stem>b)$")]
    [DataRow(@"a/b/",             @"a/b/",             @"^a[/\\]b[/\\](?<stem>)$")]
    [DataRow(@"/a/b/",            @"/a/b/",            @"^[/\\]a[/\\]b[/\\](?<stem>)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"///",              @"///",              @"^[/\\][/\\][/\\](?<stem>)$")]
    [DataRow(@"\\\",              @"\\\",              @"^[/\\][/\\][/\\](?<stem>)$")]
    [DataRow(@"\/\",              @"\/\",              @"^[/\\][/\\][/\\](?<stem>)$")]
    [DataRow(@"/\/",              @"/\/",              @"^[/\\][/\\][/\\](?<stem>)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@" ",                @"",                 @"^(?<stem>\ )$")]
    [DataRow("\t",                @"",                 @"^(?<stem>\t)$")]
    [DataRow("\r\n",              @"",                 @"^(?<stem>\r\n)$")]
    [DataRow(@"'a'",              @"",                 @"^(?<stem>'a')$")]
    [DataRow(@"""a""",            @"",                 @"^(?<stem>""a"")$")]
    [DataRow(@"^$(+[{",           @"",                 @"^(?<stem>\^\$\(\+\[\{)$")]
    [DataRow(@"^$(+[{/",          @"^$(+[{/",          @"^\^\$\(\+\[\{[/\\](?<stem>)$")]
    [DataRow(@"^$(+[{\",          @"^$(+[{\",          @"^\^\$\(\+\[\{[/\\](?<stem>)$")]
    [DataRow(@"/^$(+[{/",         @"/^$(+[{/",         @"^[/\\]\^\$\(\+\[\{[/\\](?<stem>)$")]
    [DataRow(@"\^$(+[{\",         @"\^$(+[{\",         @"^[/\\]\^\$\(\+\[\{[/\\](?<stem>)$")]
    [DataRow(@"a+b/c+d",          @"a+b/",             @"^a\+b[/\\](?<stem>c\+d)$")]
    [DataRow(@"a+b\c+d",          @"a+b\",             @"^a\+b[/\\](?<stem>c\+d)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"?",                @"",                 @"^(?<stem>[^/\\])$")]
    [DataRow(@"/?/",              @"/",                @"^[/\\](?<stem>[^/\\][/\\])$")]
    [DataRow(@"\?\",              @"\",                @"^[/\\](?<stem>[^/\\][/\\])$")]
    [DataRow(@"a?b",              @"",                 @"^(?<stem>a[^/\\]b)$")]
    [DataRow(@"???",              @"",                 @"^(?<stem>[^/\\][^/\\][^/\\])$")]
    [DataRow(@"?.c",              @"",                 @"^(?<stem>[^/\\]\.c)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"*",                @"",                 @"^(?<stem>[^/\\]*)$")]
    [DataRow(@".*",               @"",                 @"^(?<stem>\.[^/\\]*)$")]
    [DataRow(@"*.*",              @"",                 @"^(?<stem>[^/\\]*\.[^/\\]*)$")]
    [DataRow(@"*./",              @"",                 @"^(?<stem>[^/\\]*\.[/\\])$")]
    [DataRow(@"*.\",              @"",                 @"^(?<stem>[^/\\]*\.[/\\])$")]
    [DataRow(@".*/.*",            @"",                 @"^(?<stem>\.[^/\\]*[/\\]\.[^/\\]*)$")]
    [DataRow(@".*\.*",            @"",                 @"^(?<stem>\.[^/\\]*[/\\]\.[^/\\]*)$")]
    [DataRow(@"*a*/*b*",          @"",                 @"^(?<stem>[^/\\]*a[^/\\]*[/\\][^/\\]*b[^/\\]*)$")]
    [DataRow(@"*a*\*b*",          @"",                 @"^(?<stem>[^/\\]*a[^/\\]*[/\\][^/\\]*b[^/\\]*)$")]
    [DataRow(@"a*b/c*d",          @"",                 @"^(?<stem>a[^/\\]*b[/\\]c[^/\\]*d)$")]
    [DataRow(@"a*b\c*d",          @"",                 @"^(?<stem>a[^/\\]*b[/\\]c[^/\\]*d)$")]
    [DataRow(@"/a/*/b/*.c",       @"/a/",              @"^[/\\]a[/\\](?<stem>[^/\\]*[/\\]b[/\\][^/\\]*\.c)$")]
    [DataRow(@"\a\*\b\*.c",       @"\a\",              @"^[/\\]a[/\\](?<stem>[^/\\]*[/\\]b[/\\][^/\\]*\.c)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@".",                @"",                 @"^(?<stem>\.)$")]
    [DataRow(@"a.",               @"",                 @"^(?<stem>a\.)$")]
    [DataRow(@"/.",               @"/",                @"^[/\\](?<stem>\.)$")]
    [DataRow(@"\.",               @"\",                @"^[/\\](?<stem>\.)$")]
    [DataRow(@"?.",               @"",                 @"^(?<stem>[^/\\.])$")]
    [DataRow(@"*.",               @"",                 @"^(?<stem>[^/\\.]*)$")]
    [DataRow(@"??.",              @"",                 @"^(?<stem>[^/\\.][^/\\.])$")]
    [DataRow(@"**.",              @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\.]*)$")]
    [DataRow(@"***.",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\.]*)$")]
    [DataRow(@"/a/*./b/?c*.",     @"/a/",              @"^[/\\]a[/\\](?<stem>[^/\\]*\.[/\\]b[/\\][^/\\.]c[^/\\.]*)$")]
    [DataRow(@"\a\*.\b\?c*.",     @"\a\",              @"^[/\\]a[/\\](?<stem>[^/\\]*\.[/\\]b[/\\][^/\\.]c[^/\\.]*)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"**",               @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"***",              @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"**.c",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.c)$")]
    [DataRow(@"***.c",            @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.c)$")]
    [DataRow(@"**/",              @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"**\",              @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"/**/",             @"/",                @"^[/\\](?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"\**\",             @"\",                @"^[/\\](?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"***/",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"***\",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*)$")]
    [DataRow(@"***///",           @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[/\\][/\\])$")]
    [DataRow(@"***\\\",           @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[/\\][/\\])$")]
    [DataRow(@"**/*",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"**\*",             @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"***/*",            @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"***\*",            @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*)$")]
    [DataRow(@"**/*.*",           @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.[^/\\]*)$")]
    [DataRow(@"**\*.*",           @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.[^/\\]*)$")]
    [DataRow(@"***/*.*",          @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.[^/\\]*)$")]
    [DataRow(@"***\*.*",          @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*\.[^/\\]*)$")]
    [DataRow(@"a/**/b",           @"a/",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a\**\b",           @"a\",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a/***/b",          @"a/",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a\***\b",          @"a\",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a/**/**/b",        @"a/",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a\**\**\b",        @"a\",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*(?:[^/\\]*[/\\])*b)$")]
    [DataRow(@"a/**/b/**/c",      @"a/",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b[/\\](?:[^/\\]*[/\\])*c)$")]
    [DataRow(@"a\**\b\**\c",      @"a\",               @"^a[/\\](?<stem>(?:[^/\\]*[/\\])*b[/\\](?:[^/\\]*[/\\])*c)$")]
    [DataRow(@"**a**",            @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*a[^/\\]*)$")]
    [DataRow(@"**a*b**",          @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]*a[^/\\]*b[^/\\]*)$")]
    //       INPUT glob           EXPECTED path        EXPECTED regex
    [DataRow(@"**/?a*b?/??c*.",   @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]a[^/\\]*b[^/\\][/\\][^/\\.][^/\\.]c[^/\\.]*)$")]
    [DataRow(@"**\?a*b?\??c*.",   @"",                 @"^(?<stem>(?:[^/\\]*[/\\])*[^/\\]a[^/\\]*b[^/\\][/\\][^/\\.][^/\\.]c[^/\\.]*)$")]
    public void TestRegexConversion(string glob, string path, string regex)
    {
        var result = GlobConvert.ToRegexPattern(glob, (GlobRegexOptions)(~0));
        Assert.AreEqual(path, result.BasePath);
        Assert.AreEqual(regex, result.RegexPattern);
    }

    [TestMethod]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"///",              @"///",              @"///",              @"")]
    [DataRow(@"///",              @"\\\",              @"///",              @"")]
    [DataRow(@"\\\",              @"\\\",              @"\\\",              @"")]
    [DataRow(@"\\\",              @"///",              @"\\\",              @"")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"?b.c",             @"ab.c",             @"",                 @"ab.c")]
    [DataRow(@"/a/?b.c",          @"/a/ab.c",          @"/a/",              @"ab.c")]
    [DataRow(@"\a\??.?",          @"\a\ab.c",          @"\a\",              @"ab.c")]
    [DataRow(@"\??\??.?",         @"\ab\ab.c",         @"\",                @"ab\ab.c")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"*",                @"",                 @"",                 @"")]
    [DataRow(@"*",                @"a",                @"",                 @"a")]
    [DataRow(@"*",                @"abc",              @"",                 @"abc")]
    [DataRow(@"*",                @"a.b.c",            @"",                 @"a.b.c")]
    [DataRow(@"*",                @"*",                @"",                 @"*")]
    [DataRow(@"*",                @".",                @"",                 @".")]
    [DataRow(@"*",                @"..",               @"",                 @"..")]
    [DataRow(@"*",                @" ... ",            @"",                 @" ... ")]
    [DataRow(@"*",                @"^.[x'""].*$",      @"",                 @"^.[x'""].*$")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"*.",               @"",                 @"",                 @"")]
    [DataRow(@"*.",               @"a",                @"",                 @"a")]
    [DataRow(@"*.",               @"abc",              @"",                 @"abc")]
    [DataRow(@"*.",               @"*",                @"",                 @"*")]
    [DataRow(@"*.",               @"^[x'""]*$",        @"",                 @"^[x'""]*$")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"**.",              @"",                 @"",                 @"")]
    [DataRow(@"**.",              @"a",                @"",                 @"a")]
    [DataRow(@"**.",              @"abc",              @"",                 @"abc")]
    [DataRow(@"**.",              @"*",                @"",                 @"*")]
    [DataRow(@"**.",              @"^[x'""]*$",        @"",                 @"^[x'""]*$")]
    [DataRow(@"**.",              @"/",                @"",                 @"/")]
    [DataRow(@"**.",              @"\",                @"",                 @"\")]
    [DataRow(@"**.",              @"a/b",              @"",                 @"a/b")]
    [DataRow(@"**.",              @"a\b",              @"",                 @"a\b")]
    [DataRow(@"**.",              @"/a/b",             @"",                 @"/a/b")]
    [DataRow(@"**.",              @"\a\b",             @"",                 @"\a\b")]
    [DataRow(@"**.",              @"././",             @"",                 @"././")]
    [DataRow(@"**.",              @".\.\",             @"",                 @".\.\")]
    [DataRow(@"**.",              @"././a",            @"",                 @"././a")]
    [DataRow(@"**.",              @".\.\a",            @"",                 @".\.\a")]
    [DataRow(@"**.",              @"^[x']*$/^[x']*$",  @"",                 @"^[x']*$/^[x']*$")]
    [DataRow(@"**.",              @"^[x']*$\^[x']*$",  @"",                 @"^[x']*$\^[x']*$")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"*.c",              @".c",               @"",                 @".c")]
    [DataRow(@"*.c",              @"a.c",              @"",                 @"a.c")]
    [DataRow(@"*.c",              @"ab.c",             @"",                 @"ab.c")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"**",               @"",                 @"",                 @"")]
    [DataRow(@"**",               @".",                @"",                 @".")]
    [DataRow(@"**",               @"1",                @"",                 @"1")]
    [DataRow(@"**",               @"a.c",              @"",                 @"a.c")]
    [DataRow(@"**",               @"ab.c",             @"",                 @"ab.c")]
    [DataRow(@"**",               @"a/b.c",            @"",                 @"a/b.c")]
    [DataRow(@"**",               @"a\b.c",            @"",                 @"a\b.c")]
    [DataRow(@"**",               @"/a/b.c",           @"",                 @"/a/b.c")]
    [DataRow(@"**",               @"\a\b.c",           @"",                 @"\a\b.c")]
    [DataRow(@"**",               @"c:\a\b.c",         @"",                 @"c:\a\b.c")]
    [DataRow(@"**",               @".c/.c/.ca",        @"",                 @".c/.c/.ca")]
    [DataRow(@"**",               @".c\.c\.ca",        @"",                 @".c\.c\.ca")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"**.c",             @".c",               @"",                 @".c")]
    [DataRow(@"**.c",             @"a.c",              @"",                 @"a.c")]
    [DataRow(@"**.c",             @"ab.c",             @"",                 @"ab.c")]
    [DataRow(@"**.c",             @"a/b.c",            @"",                 @"a/b.c")]
    [DataRow(@"**.c",             @"a\b.c",            @"",                 @"a\b.c")]
    [DataRow(@"**.c",             @"/a/b.c",           @"",                 @"/a/b.c")]
    [DataRow(@"**.c",             @"\a\b.c",           @"",                 @"\a\b.c")]
    [DataRow(@"**.c",             @"c:\a\b.c",         @"",                 @"c:\a\b.c")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"**/*.c",           @".c",               @"",                 @".c")]
    [DataRow(@"**/*.c",           @"/.c",              @"",                 @"/.c")]
    [DataRow(@"**/*.c",           @"\.c",              @"",                 @"\.c")]
    [DataRow(@"**/*.c",           @"a.c",              @"",                 @"a.c")]
    [DataRow(@"**/*.c",           @"ab.c",             @"",                 @"ab.c")]
    [DataRow(@"**/*.c",           @"a/b.c",            @"",                 @"a/b.c")]
    [DataRow(@"**/*.c",           @"a\b.c",            @"",                 @"a\b.c")]
    [DataRow(@"**/*.c",           @"/a/b.c",           @"",                 @"/a/b.c")]
    [DataRow(@"**/*.c",           @"\a\b.c",           @"",                 @"\a\b.c")]
    [DataRow(@"**/*.c",           @"c:\a\b.c",         @"",                 @"c:\a\b.c")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"/a?/**/b*c/*.",    @"/a0/bc/1",         @"/",                @"a0/bc/1")]
    [DataRow(@"/a?/**/b*c/*.",    @"\a0\bc\1",         @"/",                @"a0\bc\1")]
    [DataRow(@"/a?/**/b*c/*.",    @"/a0/bAAAc/1",      @"/",                @"a0/bAAAc/1")]
    [DataRow(@"/a?/**/b*c/*.",    @"\a0\bAAAc\1",      @"/",                @"a0\bAAAc\1")]
    [DataRow(@"/a?/**/b*c/*.",    @"/a0/x/y/z/bc/1",   @"/",                @"a0/x/y/z/bc/1")]
    [DataRow(@"/a?/**/b*c/*.",    @"\a0\x\y\z\bc\1",   @"/",                @"a0\x\y\z\bc\1")]
    //       INPUT glob           INPUT string         EXPECTED path        EXPECTED stem
    [DataRow(@"//**",             @"\\a/b/c",          @"//",               @"a/b/c")]
    [DataRow(@"/a/b/**/*.",       @"\a\b\1",           @"/a/b/",            @"1")]
    [DataRow(@"/a/b/**/*.",       @"\a\b\x\y\1",       @"/a/b/",            @"x\y\1")]
    [DataRow(@"a/b/**/*.",        @"a/b/x/y/z/1",      @"a/b/",             @"x/y/z/1")]
    [DataRow(@"c:\a\b\**\??\*.",  @"c:/a/b/x/y/zz/1",  @"c:\a\b\",          @"x/y/zz/1")]
    [DataRow(@"\\a\b?\**\**\*.",  @"\\a\b0\x\y\z\1",   @"\\a\",             @"b0\x\y\z\1")]
    public void TestMatch(string glob, string input, string path, string stem)
    {
        var result = GlobConvert.ToRegexPattern(glob, (GlobRegexOptions)(~0));
        Assert.AreEqual(path, result.BasePath);
        var match = Regex.Match(input, result.RegexPattern);
        Assert.IsTrue(match.Success);
        Assert.AreEqual(stem, match.Groups["stem"].Value);
    }

    [TestMethod]
    //       INPUT glob           INPUT string
    [DataRow("?",                 @"/")]
    [DataRow("?",                 @"\")]
    [DataRow("?",                 @"aa")]
    [DataRow("???",               @"aa")]
    //       INPUT glob           INPUT string
    [DataRow("*",                 @"/")]
    [DataRow("*",                 @"\")]
    [DataRow("*",                 @"/a")]
    [DataRow("*",                 @"\a")]
    [DataRow("*",                 @".c/")]
    [DataRow("*",                 @".c\")]
    [DataRow("*",                 @".c/.c")]
    [DataRow("*",                 @".c\.c")]
    //       INPUT glob           INPUT string
    [DataRow("*.",                @"/")]
    [DataRow("*.",                @"\")]
    [DataRow("*.",                @".")]
    [DataRow("*.",                @".c")]
    //       INPUT glob           INPUT string
    [DataRow("**.",               @".")]
    [DataRow("**.",               @".c")]
    [DataRow("**.",               @"/.")]
    [DataRow("**.",               @"\.")]
    [DataRow("**.",               @"a/.")]
    [DataRow("**.",               @"a\.")]
    [DataRow("**.",               @"/a/.")]
    [DataRow("**.",               @"\a\.")]
    [DataRow("**.",               @"a/b.c")]
    [DataRow("**.",               @"a\b.c")]
    //       INPUT glob           INPUT string
    [DataRow("*.c",               @"/.c")]
    [DataRow("*.c",               @"\.c")]
    [DataRow("*.c",               @"b.c/")]
    [DataRow("*.c",               @"b.c\")]
    [DataRow("*.c",               @"a/b.c")]
    [DataRow("*.c",               @"a\b.c")]
    [DataRow("*.c",               @"/a/b.c")]
    [DataRow("*.c",               @"\a\b.c")]
    [DataRow("*.c",               @"c:\a\b.c")]
    //       INPUT glob           INPUT string
    [DataRow("**.c",              @".cs")]
    [DataRow("**.c",              @"..c.")]
    [DataRow("**.c",              @".c/.c/.cs")]
    [DataRow("**.c",              @".c\.c\.cs")]
    //       INPUT glob           INPUT string
    [DataRow("**/*.",             @".")]
    [DataRow("**/*.",             @".c")]
    [DataRow("**/*.",             @"/.")]
    [DataRow("**/*.",             @"\.")]
    [DataRow("**/*.",             @"a/.")]
    [DataRow("**/*.",             @"a\.")]
    [DataRow("**/*.",             @"/a/.")]
    [DataRow("**/*.",             @"\a\.")]
    [DataRow("**/*.",             @"a/b.c")]
    [DataRow("**/*.",             @"a\b.c")]
    //       INPUT glob           INPUT string
    [DataRow("**/*.c",            @".cs")]
    [DataRow("**/*.c",            @"..c.")]
    [DataRow("**/*.c",            @".c/.c/.cs")]
    [DataRow("**/*.c",            @".c\.c\.cs")]
    //       INPUT glob           INPUT string
    [DataRow("/a?/**/b*c/*.",     @"/aaa/b/c/1")]
    [DataRow("/a?/**/b*c/*.",     @"/aaa/x/b/c/1")]
    [DataRow("/a?/**/b*c/*.",     @"/aa/xbc/1")]
    [DataRow("/a?/**/b*c/*.",     @"/aa/bcx/1")]
    [DataRow("/a?/**/b*c/*.",     @"/aa/bc/1.txt")]
    [DataRow("/a?/**/b*c/*.",     @"/aa/bc/.")]
    [DataRow("/a?/**/b*c/*.",     @"/aa/bc/.c")]
    [DataRow("/a?/**/b*c/*.",     @"c:/aa/bc/1")]
    public void TestNotMatch(string glob, string input)
        => Assert.IsFalse(Regex.IsMatch(input, GlobConvert.ToRegexPattern(glob, (GlobRegexOptions)(~0)).RegexPattern));

    [TestMethod]
    public void TestMathFullStringOption()
    {
        Assert.ThrowsException<ArgumentNullException>(() => GlobConvert.ToRegexPatternParts(null, GlobRegexOptions.MatchFullString));

        var pattern = GlobConvert.ToRegexPattern("/a?b/*.c", ~GlobRegexOptions.MatchFullString).RegexPattern;
        Assert.IsFalse(pattern.StartsWith('^'));
        Assert.IsFalse(pattern.EndsWith('$'));

        pattern = GlobConvert.ToRegexPattern("/a?b/*.c", GlobRegexOptions.MatchFullString).RegexPattern;
        Assert.IsTrue(pattern.StartsWith('^'));
        Assert.IsTrue(pattern.EndsWith('$'));
    }

    [TestMethod]
    public void TestJoinedRawPartsEqualInputGlobFuzzing()
    {
        var rnd = new Random(1337);
        foreach(var glob in Enumerable.Range(0, 20000).Select(_ => RandomGlob(rnd)))
        {
            var parts = GlobConvert.ToRegexPatternParts(glob, (GlobRegexOptions)(~0));
            Assert.AreEqual(glob, string.Concat(parts.Select(part => part.Raw)));
        }
    }

    [TestMethod]
    public void TestResultRegexMatchInputGlobFuzzing()
    {
        var rnd = new Random(1337);
        foreach(var glob in Enumerable.Range(0, 20000).Select(_ => RandomGlob(rnd)))
        {
            var regex = new Regex(GlobConvert.ToRegexPattern(glob, ~GlobRegexOptions.WildcardsWithTrailingDotMatchNoExtension).RegexPattern, RegexOptions.CultureInvariant);
            Assert.IsTrue(regex.IsMatch(glob));
        }
    }

    private string RandomGlob(Random rnd, int length = 20)
    {
        const string alphabet = @"/\a?*.";
        Span<char> span = stackalloc char[rnd.Next(length)];
        for(int i = 0; i < span.Length; i++)
            span[i] = alphabet[rnd.Next(alphabet.Length)];
        return span.ToString();
    }

    [TestMethod]
    //       INPUT                EXPECTED
    [DataRow("")]
    [DataRow("/",                 "/")]
    [DataRow("//",                "/", "/")]
    [DataRow("/|/",               "/", "|", "/")]
    [DataRow("a",                 "a")]
    [DataRow("abc",               "abc")]
    [DataRow("abc/",              "abc", "/")]
    [DataRow("/abc",              "/", "abc")]
    [DataRow("/ab|bc/",           "/", "ab", "|", "bc", "/")]
    [DataRow("ab/bc||cd/de",      "ab", "/", "bc", "|", "|", "cd", "/", "de")]
    public void TestSplitLazy(string input, params string[] expected)
    {
        var delim = new[] { '/', '|' };
        var parts = input.SplitLazy(delim).ToArray();

        Assert.IsTrue(expected.SequenceEqual(parts.Select(part => part.Value), StringComparer.Ordinal));
        Assert.IsTrue(parts.All(part => !part.IsSeparator ^ delim.Select(c => c.ToString()).Contains(part.Value)));

        if(parts.Length == 0)
            return;

        var last = parts.Last();
        Assert.IsTrue(last.IsSeparator || last.IsLast);
        Assert.IsFalse(parts.SkipLast(1).Any(part => part.IsLast));
    }

    [TestMethod]
    //       INPUT                EXPECTED
    [DataRow("",                  "")]
    [DataRow("",                  "")]
    [DataRow("a",                 "(a..a){1}")]
    [DataRow("abc",               "(a..a){1}(b..b){1}(c..c){1}")]
    [DataRow("aA",                "(a..a){1}(A..A){1}")]
    [DataRow("Aa",                "(A..a){2}")]
    [DataRow("aAa",               "(a..a){1}(A..a){2}")]
    [DataRow("AAAaa",             "(A..a){5}")]
    [DataRow("AaaAAAaaaaAAAAA",   "(A..a){3}(A..a){7}(A..A){5}")]
    [DataRow("aAAaaaAAAAaaaaa",   "(a..a){1}(A..a){5}(A..a){9}")]
    public void TestGroupAdjacent(string input, string expected)
        => Assert.AreEqual(expected, string.Concat(input.GroupAdjacent((c1, c2) => c1 == c2 || char.ToLower(c1) == c2)
            .Select(c => $"({c.First}..{c.Last}){{{c.Count}}}")));
}

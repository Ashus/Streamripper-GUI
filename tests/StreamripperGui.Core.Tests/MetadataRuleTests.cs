namespace StreamripperGui.Core.Tests;

public sealed class MetadataRuleTests
{
    [Xunit.Fact]
    public void NativeActionFlagsAreSeparateFromTheRegex()
    {
        var engine = new CapturingValidator();
        var result = Models.MetadataRules.Validate("m/advert/ix\nm/^(.*?) - (.*)$/A1T2\n", engine);
        Xunit.Assert.DoesNotContain(result.Diagnostics, d => !d.IsWarning);
        Xunit.Assert.Equal(("advert", true), engine.Patterns[0]);
        Xunit.Assert.Equal(("^(.*?) - (.*)$", false), engine.Patterns[1]);
        Xunit.Assert.DoesNotContain(engine.Patterns, p => p.Item1.Contains("/x", StringComparison.Ordinal));
    }

    [Xunit.Fact]
    public void DelimiterEscapingAndEmptySubstitutionArePreserved()
    {
        var result = Models.MetadataRules.Parse(" s/foo\\/bar$//i\nm//x\n");
        Xunit.Assert.Empty(result.Diagnostics);
        Xunit.Assert.Equal("foo\\/bar$", result.Rules[0].Pattern);
        Xunit.Assert.Equal("", result.Rules[0].Replacement);
        Xunit.Assert.Equal("", result.Rules[1].Pattern);
    }

    [Xunit.Theory]
    [Xunit.InlineData("m/broken")]
    [Xunit.InlineData("s/pattern/replacement")]
    [Xunit.InlineData("m /pattern/x")]
    [Xunit.InlineData("m/pattern/z")]
    [Xunit.InlineData("m/pattern/A25")]
    [Xunit.InlineData("m/pattern/A0")]
    [Xunit.InlineData("m/pattern/A")]
    [Xunit.InlineData("s/pattern//x")]
    [Xunit.InlineData("m/pattern/x # trailing")]
    [Xunit.InlineData("\uFEFFm/pattern/x")]
    [Xunit.InlineData("# comment\0")]
    [Xunit.InlineData("# comment\r\r\n")]
    public void MalformedNativeRuleSyntaxIsRejected(string text) =>
        Xunit.Assert.Contains(Models.MetadataRules.Parse(text).Diagnostics, d => !d.IsWarning);

    [Xunit.Fact]
    public void Utf8ByteLimitsCountMultibyteCharacters()
    {
        Xunit.Assert.Contains(Models.MetadataRules.Parse("m/" + new string('ž', 1024) + "/x").Diagnostics, d => !d.IsWarning);
    }

    [Xunit.Fact]
    public void TemplateHasFinalExtractionAndNoActiveExampleExclusions()
    {
        var rules = Models.MetadataRules.Parse(Models.MetadataRules.Template);
        Xunit.Assert.Empty(rules.Diagnostics);
        var rule = Xunit.Assert.Single(rules.Rules);
        Xunit.Assert.True(rule.IsExtraction);
        Xunit.Assert.Equal("A1T2", rule.Flags);
    }

    [Xunit.Fact]
    public void LiteralHelpersEscapeRegexPunctuationAndSupportAnEmptyDocument()
    {
        var rule = Models.MetadataRules.LiteralRule("ad/with (dots).", true, false);
        Xunit.Assert.Equal("m/ad\\/with\\ \\(dots\\)\\./x", rule);
        var parsed = Models.MetadataRules.Parse(rule);
        Xunit.Assert.Empty(parsed.Diagnostics);
        var empty = Models.MetadataRules.InsertGenerated("", rule);
        Xunit.Assert.Equal(rule + "\n", empty.Text);
        Xunit.Assert.Throws<ArgumentException>(() => Models.MetadataRules.LiteralRule("one\ntwo", false, false));
        Xunit.Assert.Equal("s/suffix$//", Models.MetadataRules.LiteralRule("suffix", false, true));
        Xunit.Assert.Equal("m/suffix$/x", Models.MetadataRules.LiteralRule("suffix", true, true));
        Xunit.Assert.Equal("s/^prefix//", Models.MetadataRules.LiteralRule("prefix", false, false, true));
        Xunit.Assert.Equal("m/^prefix/x", Models.MetadataRules.LiteralRule("prefix", true, false, true));
        Xunit.Assert.Equal("s/^title$//", Models.MetadataRules.LiteralRule("title", false, true, true));
        Xunit.Assert.Equal("m/^title$/x", Models.MetadataRules.LiteralRule("title", true, true, true));
    }

    [Xunit.Fact]
    public void NativeCompilerDiagnosticsKeepRuleLineNumbers()
    {
        var result = Models.MetadataRules.Validate("# comment\nm/[broken/x", new CapturingValidator());
        Xunit.Assert.Contains(result.Diagnostics, d => !d.IsWarning && d.Line == 2 && d.Column == 3);
    }

    [Xunit.Fact]
    public void MetadataLogEscapesDecodeBeforePreparingRuleInput()
    {
        var text = Infrastructure.MetadataLogTail.DecodeLine("2026-10-24T15:30:00+0200\tA\\\\B\\tTitle\\x1F\\n");
        Xunit.Assert.Equal("A\\B\tTitle\u001F\n", text);
    }

    [Xunit.Fact]
    public void TailIsBoundedAndSkipsPartialFinalLines()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "rule-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var file = Path.Combine(directory, "metadata.log");
            File.WriteAllText(file, "old line\ncomplete line\nunfinished");
            Xunit.Assert.Equal("old line\ncomplete line\n", Infrastructure.MetadataLogTail.Read(file));
            Xunit.Assert.DoesNotContain("unfinished", Infrastructure.MetadataLogTail.Read(file, 24));
            Xunit.Assert.True(Infrastructure.MetadataLogTail.Read(file, 24).Length <= 24);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Xunit.Fact]
    public void RulesAreRealUtf8FilesUnderRulesAndReplacementDoesNotCreateBackup()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "rule-tests", Guid.NewGuid().ToString("N"));
        var files = new Infrastructure.MetadataRuleFiles(directory);
        try
        {
            files.Write("rules/example.txt", Models.MetadataRules.Template);
            files.Write("rules/example.txt", "m/žluťoučký/x\n");
            Xunit.Assert.Equal("m/žluťoučký/x\n", files.Read("rules/example.txt"));
            Xunit.Assert.False(File.Exists(files.Resolve("rules/example.txt") + ".bak"));
            Xunit.Assert.Throws<ArgumentException>(() => files.Resolve("../escape.txt"));
            Xunit.Assert.DoesNotContain((byte)0xEF, File.ReadAllBytes(files.Resolve("rules/example.txt")).Take(1));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Xunit.Fact]
    public void RuleArgumentsAreOnlyAddedForAnExplicitSelection()
    {
        var args = Models.RecordingArguments.Build("https://example.com", "recordings", new(), "rules/radio.txt");
        Xunit.Assert.Contains("--metadata-rules", args);
        Xunit.Assert.Contains("rules/radio.txt", args);
        Xunit.Assert.Contains("--reload-rules", args);
        var without = Models.RecordingArguments.Build("https://example.com", "recordings", new());
        Xunit.Assert.DoesNotContain("--metadata-rules", without);
        Xunit.Assert.DoesNotContain("--reload-rules", without);
    }

    private sealed class CapturingValidator : Models.INativeRegexValidator
    {
        public List<(string, bool)> Patterns { get; } = [];
        public Models.RegexCheck Check(string pattern, bool ignoreCase)
        {
            Patterns.Add((pattern, ignoreCase));
            return new(pattern == "[broken" ? "Native regex error" : null, 2);
        }
    }

    [Xunit.Fact]
    public void RuleDiscoveryOffersOnlyUnregisteredTxtFilesWithoutWritingThem()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "rule-tests", Guid.NewGuid().ToString("N"));
        var files = new Infrastructure.MetadataRuleFiles(directory);
        try
        {
            Xunit.Assert.Empty(files.DiscoverNew([]));
            files.Write("rules/registered.txt", "m/old/x\n");
            files.Write("rules/Zulu.TXT", "m/new/x\n");
            files.Write("rules/alpha.txt", "m/new/x\n");
            files.Write("rules/ignored.txt.bak", "backup");
            var found = files.DiscoverNew(["rules/registered.txt"]);
            Xunit.Assert.Equal(["rules/alpha.txt", "rules/Zulu.TXT"], found);
            Xunit.Assert.Equal("m/new/x\n", files.Read("rules/alpha.txt"));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Xunit.Fact]
    public async Task ClearingMetadataLogTruncatesExistingFileAndDoesNotCreateMissingFiles()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "rule-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "metadata.log");
        try
        {
            await Xunit.Assert.ThrowsAsync<FileNotFoundException>(() => Infrastructure.MetadataLogTail.ClearAsync(path));
            Xunit.Assert.False(File.Exists(path));
            File.WriteAllText(path, "old line\n");
            await Infrastructure.MetadataLogTail.ClearAsync(path);
            Xunit.Assert.True(File.Exists(path));
            Xunit.Assert.Equal(0, new FileInfo(path).Length);
            File.AppendAllText(path, "new line\n");
            Xunit.Assert.Equal("new line\n", Infrastructure.MetadataLogTail.Read(path));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Xunit.Fact]
    public void GeneratedRulesUseDeterministicPositionsAndKeepCrLf()
    {
        const string text = "# comment\r\nm/advert/x\r\ns/old//\r\nm/(.*)/T1\r\n";
        var exclusion = Models.MetadataRules.InsertGenerated(text, "m/new/x");
        Xunit.Assert.Equal("# comment\r\nm/advert/x\r\nm/new/x\r\ns/old//\r\nm/(.*)/T1\r\n", exclusion.Text);
        var replacement = Models.MetadataRules.InsertGenerated(text, "s/new//");
        Xunit.Assert.Contains("s/old//\r\ns/new//\r\nm/(.*)/T1", replacement.Text);
    }

    [Xunit.Theory]
    [Xunit.InlineData("# heading\n\n# s/example//\nm/(.*)/T1\n", "# heading\n\n# s/example//\nm/new/x\nm/(.*)/T1\n")]
    [Xunit.InlineData("# heading", "# heading\nm/new/x\n")]
    [Xunit.InlineData("# heading\n\n", "# heading\n\nm/new/x\n")]
    [Xunit.InlineData("", "m/new/x\n")]
    public void GeneratedExclusionsPreserveLeadingCommentsWithoutSubstitutions(string text, string expected)
    {
        var result = Models.MetadataRules.InsertGenerated(text, "m/new/x");
        Xunit.Assert.Equal(expected, result.Text);
        Xunit.Assert.Equal(expected.IndexOf("m/new/x", StringComparison.Ordinal) + "m/new/x\n".Length, result.Caret);
    }
}

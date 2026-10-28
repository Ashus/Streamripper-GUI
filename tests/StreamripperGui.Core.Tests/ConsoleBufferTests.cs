namespace StreamripperGui.Core.Tests;

public sealed class ConsoleBufferTests
{
    [Xunit.Theory]
    [Xunit.InlineData("long status\rshort", new[] { "short" })]
    [Xunit.InlineData("x\ra much longer status", new[] { "a much longer status" })]
    [Xunit.InlineData("status\r", new[] { "status" })]
    [Xunit.InlineData("a\r\r\r\nb\nc\r\nd", new[] { "a", "b", "c", "d" })]
    [Xunit.InlineData("\n\r\n\n", new[] { "", "", "" })]
    [Xunit.InlineData("\r\r", new string[0])]
    public void HandlesLineControlCharacters(string input, string[] expected)
    {
        var buffer = Parse(input);
        Xunit.Assert.Equal(expected, buffer.Snapshot().Select(line => line.Text));
    }

    [Xunit.Fact]
    public void DisplaysPartialTextAndSplitCrLfWithoutWaitingForNewline()
    {
        var buffer = new Console.ConsoleBuffer();
        Append(buffer, "connecting");
        Xunit.Assert.Equal("connecting", Xunit.Assert.Single(buffer.Snapshot()).Text);
        Append(buffer, "\r");
        Append(buffer, "\n");
        var line = Xunit.Assert.Single(buffer.Snapshot());
        Xunit.Assert.True(line.IsComplete);
        Xunit.Assert.Equal("connecting", line.Text);
        Append(buffer, "ripping");
        Xunit.Assert.Equal(2, buffer.Snapshot().Count);
    }

    [Xunit.Fact]
    public void EveryByteBoundaryPreservesUnicodeAndCarriageReturnSemantics()
    {
        const string text = "Žluťoučký 🎵 e\u0301\rKůň 🎶\r\n次の曲\n";
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var expected = Parse(text).Snapshot();
        for (var split = 0; split <= bytes.Length; split++)
        {
            var buffer = new Console.ConsoleBuffer();
            buffer.Append(Console.ConsoleChannel.StandardOutput, bytes.AsSpan(0, split));
            buffer.Append(Console.ConsoleChannel.StandardOutput, bytes.AsSpan(split));
            buffer.Complete(Console.ConsoleChannel.StandardOutput);
            Xunit.Assert.Equal(expected, buffer.Snapshot());
        }
        var bytewise = new Console.ConsoleBuffer();
        foreach (var value in bytes)
            bytewise.Append(Console.ConsoleChannel.StandardOutput, new[] { value });
        bytewise.Complete(Console.ConsoleChannel.StandardOutput);
        Xunit.Assert.Equal(expected, bytewise.Snapshot());
    }

    [Xunit.Fact]
    public void RandomChunkingMatchesSingleChunkIncludingLongDecoderBuffers()
    {
        var input = string.Concat(Enumerable.Repeat("Žluťoučký 🎵 e\u0301\rshort\r\nnext\n", 100));
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var expected = Parse(input).Snapshot();
        for (var seed = 0; seed < 100; seed++)
        {
            var random = new Random(seed);
            var buffer = new Console.ConsoleBuffer();
            for (var offset = 0; offset < bytes.Length;)
            {
                var length = Math.Min(random.Next(1, 80), bytes.Length - offset);
                buffer.Append(Console.ConsoleChannel.StandardOutput, bytes.AsSpan(offset, length));
                offset += length;
            }
            buffer.Complete(Console.ConsoleChannel.StandardOutput);
            Xunit.Assert.Equal(expected, buffer.Snapshot());
        }
    }

    [Xunit.Fact]
    public void InvalidAndUnfinishedUtf8IsVisibleAndCompletionIsIdempotent()
    {
        var buffer = new Console.ConsoleBuffer();
        buffer.Append(Console.ConsoleChannel.StandardOutput, new byte[] { 0xff, 0x41, 0xe2, 0x82 });
        Xunit.Assert.Equal("�A", Xunit.Assert.Single(buffer.Snapshot()).Text);
        buffer.Complete(Console.ConsoleChannel.StandardOutput);
        buffer.Complete(Console.ConsoleChannel.StandardOutput);
        var line = Xunit.Assert.Single(buffer.Snapshot());
        Xunit.Assert.Equal("�A�", line.Text);
        Xunit.Assert.True(line.IsComplete);
        Xunit.Assert.Throws<InvalidOperationException>(() => Append(buffer, "late"));
    }

    [Xunit.Fact]
    public void StreamsHaveIndependentDecodersAndReplaceableRows()
    {
        var buffer = new Console.ConsoleBuffer();
        var emoji = System.Text.Encoding.UTF8.GetBytes("🎵");
        buffer.Append(Console.ConsoleChannel.StandardOutput, emoji.AsSpan(0, 2));
        Append(buffer, "regex error\n", Console.ConsoleChannel.StandardError);
        buffer.Append(Console.ConsoleChannel.StandardOutput, emoji.AsSpan(2));
        Append(buffer, "\rfiltered\r");
        buffer.Complete(Console.ConsoleChannel.StandardOutput);
        buffer.Complete(Console.ConsoleChannel.StandardError);
        var rows = buffer.Snapshot();
        Xunit.Assert.Equal("regex error", rows[0].Text);
        Xunit.Assert.Equal(Console.ConsoleChannel.StandardError, rows[0].Channel);
        Xunit.Assert.Equal("filtered", rows[1].Text);
    }

    [Xunit.Fact]
    public void UpdatingStdoutCannotOverwriteStderrOrReorderItsOwnActiveRow()
    {
        var buffer = new Console.ConsoleBuffer();
        Append(buffer, "ripping\r");
        var id = Xunit.Assert.Single(buffer.Snapshot()).Id;
        Append(buffer, "bad regex\n", Console.ConsoleChannel.StandardError);
        Append(buffer, "filtered\r");
        var rows = buffer.Snapshot();
        Xunit.Assert.Equal(id, rows[0].Id);
        Xunit.Assert.Equal("filtered", rows[0].Text);
        Xunit.Assert.Equal("bad regex", rows[1].Text);
    }

    [Xunit.Fact]
    public void LineCountIsBoundedEvenWithAnActiveOldStatus()
    {
        var buffer = new Console.ConsoleBuffer(maxLines: 3);
        Append(buffer, "status\r");
        for (var index = 0; index < 1000; index++)
            Append(buffer, $"error {index}\n", Console.ConsoleChannel.StandardError);
        var rows = buffer.Snapshot();
        Xunit.Assert.Equal(3, rows.Count);
        Xunit.Assert.Equal(new[] { "status", "error 998", "error 999" }, rows.Select(line => line.Text));
        Append(buffer, "filtered");
        Xunit.Assert.Equal("filtered", buffer.Snapshot()[0].Text);
    }

    [Xunit.Fact]
    public void TextBudgetEvictsHistoryAndIncludesBothActiveRows()
    {
        var buffer = new Console.ConsoleBuffer(maxLines: 20, maxLineLength: 8, maxTextLength: 16);
        Append(buffer, "12345678\nabcdefgh\n");
        Append(buffer, "active");
        Append(buffer, "error", Console.ConsoleChannel.StandardError);
        var rows = buffer.Snapshot();
        Xunit.Assert.True(rows.Sum(line => line.Text.Length) <= 16);
        Xunit.Assert.Equal(new[] { "active", "error" }, rows.Select(line => line.Text));
    }

    [Xunit.Fact]
    public void TruncationDoesNotSplitEmojiAndCrStartsANewUpdate()
    {
        var buffer = new Console.ConsoleBuffer(maxLineLength: 3);
        Append(buffer, "ab🎵more");
        var line = Xunit.Assert.Single(buffer.Snapshot());
        Xunit.Assert.Equal("ab", line.Text);
        Xunit.Assert.True(line.IsTruncated);
        Append(buffer, "\r🎶x");
        line = Xunit.Assert.Single(buffer.Snapshot());
        Xunit.Assert.Equal("🎶x", line.Text);
        Xunit.Assert.False(line.IsTruncated);
    }

    [Xunit.Fact]
    public void LongUnicodeOutputAcrossDecoderBufferBoundaryIsUnchanged()
    {
        var text = new string('a', 1023) + "🎵" + new string('b', 1023) + "🎶\n";
        Xunit.Assert.Equal(text.TrimEnd('\n'), Xunit.Assert.Single(Parse(text).Snapshot()).Text);
    }

    [Xunit.Fact]
    public void ClearResetsDecodersAndStreamCompletionWithoutReusingIds()
    {
        var buffer = Parse("old");
        var id = Xunit.Assert.Single(buffer.Snapshot()).Id;
        buffer.Clear();
        Xunit.Assert.Empty(buffer.Snapshot());
        buffer.Append(Console.ConsoleChannel.StandardOutput, new byte[] { 0xe2 });
        buffer.Clear();
        Append(buffer, "new");
        var line = Xunit.Assert.Single(buffer.Snapshot());
        Xunit.Assert.Equal("new", line.Text);
        Xunit.Assert.True(line.Id > id);
    }

    [Xunit.Fact]
    public void UnchangedSnapshotRowsAreReusedWhileUpdatesKeepTheirIdentity()
    {
        var buffer = new Console.ConsoleBuffer();
        Append(buffer, "fixed\nprogress");
        var before = buffer.Snapshot();
        var repeated = buffer.Snapshot();
        Xunit.Assert.Same(before[0], repeated[0]);
        Xunit.Assert.Same(before[1], repeated[1]);
        Append(buffer, "\rnext");
        var after = buffer.Snapshot();
        Xunit.Assert.Same(before[0], after[0]);
        Xunit.Assert.Equal(before[1].Id, after[1].Id);
        Xunit.Assert.NotSame(before[1], after[1]);
    }

    [Xunit.Fact]
    public async Task ConcurrentProducersAndSnapshotReaderDoNotCorruptTheBuffer()
    {
        var buffer = new Console.ConsoleBuffer(maxLines: 100);
        await Task.WhenAll(
            Task.Run(() =>
            {
                for (var index = 0; index < 1000; index++) Append(buffer, $"out {index}\n");
                buffer.Complete(Console.ConsoleChannel.StandardOutput);
            }, Xunit.TestContext.Current.CancellationToken),
            Task.Run(() =>
            {
                for (var index = 0; index < 1000; index++) Append(buffer, $"err {index}\n", Console.ConsoleChannel.StandardError);
                buffer.Complete(Console.ConsoleChannel.StandardError);
            }, Xunit.TestContext.Current.CancellationToken),
            Task.Run(() =>
            {
                for (var index = 0; index < 1000; index++)
                    Xunit.Assert.True(buffer.Snapshot().Count <= 100);
            }, Xunit.TestContext.Current.CancellationToken));
        Xunit.Assert.All(buffer.Snapshot(), line => Xunit.Assert.True(line.IsComplete));
    }

    [Xunit.Fact]
    public void StartupFixtureKeepsHistoryAndReplacesOnlyTheLastProgressRow()
    {
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "streamripper-hotmix-startup.txt"));
        var expectedRows = Parse(fixture.TrimEnd('\r', '\n')).Snapshot().Count;
        var buffer = new Console.ConsoleBuffer();
        Append(buffer, fixture.TrimEnd('\r', '\n') + "\r[filtered... ] Advertisement [00:08]\r");
        var rows = buffer.Snapshot();
        Xunit.Assert.Contains(rows, line => line.Text == "format: MP3");
        Xunit.Assert.Equal("[filtered... ] Advertisement [00:08]", rows[^1].Text);
        Xunit.Assert.Equal(expectedRows, rows.Count);
        Xunit.Assert.DoesNotContain(rows, line => line.Text.StartsWith("[ripping...    ] YASIN"));
    }

    [Xunit.Theory]
    [Xunit.InlineData(1, 8, 16)]
    [Xunit.InlineData(2, 1, 16)]
    [Xunit.InlineData(2, 8, 15)]
    public void InvalidLimitsAreRejected(int lines, int lineLength, int textLength)
    {
        Xunit.Assert.Throws<ArgumentOutOfRangeException>(() => new Console.ConsoleBuffer(lines, lineLength, textLength));
    }

    private static Console.ConsoleBuffer Parse(string text)
    {
        var buffer = new Console.ConsoleBuffer();
        Append(buffer, text);
        buffer.Complete(Console.ConsoleChannel.StandardOutput);
        return buffer;
    }

    private static void Append(Console.ConsoleBuffer buffer, string text,
        Console.ConsoleChannel channel = Console.ConsoleChannel.StandardOutput) =>
        buffer.Append(channel, System.Text.Encoding.UTF8.GetBytes(text));
}

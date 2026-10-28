namespace StreamripperGui.Core.Tests;

public sealed class ConsoleTextProjectionTests
{
    [Xunit.Fact]
    public void TruncatedRowsIncludeAVisibleMarker()
    {
        var buffer = new Console.ConsoleBuffer(maxLineLength: 3);
        buffer.Append(Console.ConsoleChannel.StandardOutput, "abcdef"u8);
        var edit = Xunit.Assert.Single(new Console.ConsoleTextProjection().Update(buffer.Snapshot()));
        Xunit.Assert.Equal("abc … [truncated]", edit.Text);
    }

    [Xunit.Fact]
    public void IncrementalDocumentMatchesEveryBufferUpdateIncludingEvictionsAndReset()
    {
        var buffer = new Console.ConsoleBuffer(maxLines: 10);
        var projection = new Console.ConsoleTextProjection();
        var document = new System.Text.StringBuilder();
        for (var index = 0; index < 250; index++)
        {
            var channel = index % 3 == 0 ? Console.ConsoleChannel.StandardError : Console.ConsoleChannel.StandardOutput;
            var text = index % 4 == 0 ? $"progress 🎵 {index}\r" : $"line {index}\r\n";
            buffer.Append(channel, System.Text.Encoding.UTF8.GetBytes(text));
            ApplyAndCheck();
        }
        buffer.Clear();
        ApplyAndCheck();
        buffer.Append(Console.ConsoleChannel.StandardOutput, System.Text.Encoding.UTF8.GetBytes("\n\nlast"));
        ApplyAndCheck();
        buffer.Complete(Console.ConsoleChannel.StandardOutput);
        ApplyAndCheck();

        void ApplyAndCheck()
        {
            var snapshot = buffer.Snapshot();
            foreach (var edit in projection.Update(snapshot))
            {
                document.Remove(edit.Offset, edit.Length);
                document.Insert(edit.Offset, edit.Text);
            }
            Xunit.Assert.Equal(string.Join("\n", snapshot.Select(row => row.Text)), document.ToString());
            Xunit.Assert.Empty(projection.Update(snapshot));
        }
    }

    [Xunit.Fact]
    public void ProgressReplacementDoesNotReplaceUnrelatedHistory()
    {
        var buffer = new Console.ConsoleBuffer();
        buffer.Append(Console.ConsoleChannel.StandardOutput, "fixed\nlong progress\r"u8);
        var projection = new Console.ConsoleTextProjection();
        projection.Update(buffer.Snapshot());
        buffer.Append(Console.ConsoleChannel.StandardOutput, "short"u8);
        var edit = Xunit.Assert.Single(projection.Update(buffer.Snapshot()));
        Xunit.Assert.Equal(6, edit.Offset);
        Xunit.Assert.Equal("short", edit.Text);
        Xunit.Assert.Equal("long progress".Length, edit.Length);
    }
}

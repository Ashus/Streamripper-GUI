namespace StreamripperGui.Core.Console;

/// <summary>Projects buffer rows to descending-offset document edits, preserving unchanged text.</summary>
public sealed class ConsoleTextProjection
{
    private ProjectedRow[] _rows = [];

    public IReadOnlyList<ConsoleTextEdit> Update(IReadOnlyList<ConsoleLine> lines)
    {
        var previous = _rows.ToDictionary(row => row.Line.Id);
        var next = new ProjectedRow[lines.Count];
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var separator = index < lines.Count - 1;
            next[index] = previous.TryGetValue(line.Id, out var old) &&
                          ReferenceEquals(old.Line, line) && old.HasSeparator == separator
                ? old
                : new ProjectedRow(line, separator, DisplayText(line) + (separator ? "\n" : ""));
        }

        var oldLength = _rows.Sum(row => row.Text.Length);
        List<ConsoleTextEdit> edits = [];
        var retained = next.ToDictionary(row => row.Line.Id);
        var appended = next.Where(row => !previous.ContainsKey(row.Line.Id)).ToArray();
        if (appended.Length > 0)
            edits.Add(new ConsoleTextEdit(oldLength, 0, string.Concat(appended.Select(row => row.Text))));

        var offset = oldLength;
        for (var index = _rows.Length - 1; index >= 0; index--)
        {
            var old = _rows[index];
            offset -= old.Text.Length;
            if (!retained.TryGetValue(old.Line.Id, out var replacement))
            {
                // Coalesce adjacent evictions so large history trims are one document edit.
                if (edits.Count > 0 && edits[^1].Text.Length == 0 &&
                    offset + old.Text.Length == edits[^1].Offset)
                    edits[^1] = new ConsoleTextEdit(offset, old.Text.Length + edits[^1].Length, "");
                else if (old.Text.Length > 0)
                    edits.Add(new ConsoleTextEdit(offset, old.Text.Length, ""));
            }
            else if (old.Text != replacement.Text)
            {
                edits.Add(new ConsoleTextEdit(offset, old.Text.Length, replacement.Text));
            }
        }
        _rows = next;
        return edits;
    }

    private sealed record ProjectedRow(ConsoleLine Line, bool HasSeparator, string Text);

    private static string DisplayText(ConsoleLine line) =>
        line.Text + (line.IsTruncated ? " … [truncated]" : "");
}

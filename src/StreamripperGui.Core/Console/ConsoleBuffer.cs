namespace StreamripperGui.Core.Console;

/// <summary>
/// A bounded UTF-8 stream transcript, not an ANSI terminal. CR replaces an update
/// rather than preserving the suffix of the previous, longer update.
/// </summary>
public sealed class ConsoleBuffer
{
    private readonly Lock _gate = new();
    private readonly LinkedList<Entry> _lines = new();
    private readonly StreamState[] _streams = [new(), new()];
    private readonly int _maxLines;
    private readonly int _maxLineLength;
    private readonly int _maxTextLength;
    private long _nextId;
    private long _revision;
    private int _textLength;

    public ConsoleBuffer(int maxLines = 2000, int maxLineLength = 8192, int maxTextLength = 1_048_576)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLines, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLineLength, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTextLength, 2L * maxLineLength);
        _maxLines = maxLines;
        _maxLineLength = maxLineLength;
        _maxTextLength = maxTextLength;
    }

    public long Revision
    {
        get { lock (_gate) return _revision; }
    }

    public void Append(ConsoleChannel channel, ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            var state = GetState(channel);
            if (state.IsComplete)
                throw new InvalidOperationException("The output stream has already completed.");
            Decode(channel, state, bytes, false);
        }
    }

    public void Complete(ConsoleChannel channel)
    {
        lock (_gate)
        {
            var state = GetState(channel);
            if (state.IsComplete) return;
            Decode(channel, state, ReadOnlySpan<byte>.Empty, true);
            if (state.Current is not null)
            {
                state.Current.Value.IsComplete = true;
                state.Current.Value.Cached = null;
                state.Current = null;
                _revision++;
            }
            state.IsComplete = true;
            state.ReplaceOnText = false;
            Trim();
        }
    }

    public IReadOnlyList<ConsoleLine> Snapshot()
    {
        lock (_gate)
        {
            return [.. _lines.Select(entry => entry.Cached ??= new ConsoleLine(
                entry.Id, entry.Channel, entry.Text.ToString(), entry.IsComplete, entry.IsTruncated))];
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _lines.Clear();
            _textLength = 0;
            foreach (var state in _streams)
            {
                state.Decoder.Reset();
                state.Current = null;
                state.ReplaceOnText = false;
                state.IsComplete = false;
            }
            _revision++;
        }
    }

    private StreamState GetState(ConsoleChannel channel) => channel switch
    {
        ConsoleChannel.StandardOutput => _streams[0],
        ConsoleChannel.StandardError => _streams[1],
        _ => throw new ArgumentOutOfRangeException(nameof(channel))
    };

    private void Decode(ConsoleChannel channel, StreamState state, ReadOnlySpan<byte> bytes, bool flush)
    {
        Span<char> chars = stackalloc char[1024];
        bool completed;
        do
        {
            state.Decoder.Convert(bytes, chars, flush, out var bytesUsed, out var charsUsed, out completed);
            // Decoder.Convert emits valid surrogate pairs together when the output buffer has room.
            foreach (var rune in chars[..charsUsed].EnumerateRunes())
                Accept(channel, state, rune);
            bytes = bytes[bytesUsed..];
        } while (!completed);
    }

    private void Accept(ConsoleChannel channel, StreamState state, System.Text.Rune rune)
    {
        switch (rune.Value)
        {
            case '\r':
                state.ReplaceOnText = true;
                return;
            case '\n':
                var entry = GetCurrent(channel, state);
                entry.IsComplete = true;
                entry.Cached = null;
                state.Current = null;
                state.ReplaceOnText = false;
                _revision++;
                Trim();
                return;
        }

        var current = GetCurrent(channel, state);
        if (state.ReplaceOnText)
        {
            _textLength -= current.Text.Length;
            current.Text.Clear();
            current.IsTruncated = false;
            state.ReplaceOnText = false;
        }

        if (!current.IsTruncated)
        {
            if (current.Text.Length + rune.Utf16SequenceLength <= _maxLineLength)
            {
                Span<char> encoded = stackalloc char[2];
                var count = rune.EncodeToUtf16(encoded);
                current.Text.Append(encoded[..count]);
                _textLength += count;
            }
            else
            {
                current.IsTruncated = true;
            }
            current.Cached = null;
            _revision++;
        }
        Trim();
    }

    private Entry GetCurrent(ConsoleChannel channel, StreamState state)
    {
        if (state.Current is not null) return state.Current.Value;
        state.Current = _lines.AddLast(new Entry(++_nextId, channel));
        _revision++;
        return state.Current.Value;
    }

    private void Trim()
    {
        var node = _lines.First;
        while ((_lines.Count > _maxLines || _textLength > _maxTextLength) && node is not null)
        {
            var next = node.Next;
            // Never evict an active row: each stream needs its own replaceable status.
            if (node.Value.IsComplete)
            {
                _textLength -= node.Value.Text.Length;
                _lines.Remove(node);
                _revision++;
            }
            node = next;
        }
    }

    private sealed class StreamState
    {
        public System.Text.Decoder Decoder { get; } = new System.Text.UTF8Encoding(false, false).GetDecoder();
        public LinkedListNode<Entry>? Current { get; set; }
        public bool ReplaceOnText { get; set; }
        public bool IsComplete { get; set; }
    }

    private sealed class Entry(long id, ConsoleChannel channel)
    {
        public long Id { get; } = id;
        public ConsoleChannel Channel { get; } = channel;
        public System.Text.StringBuilder Text { get; } = new();
        public bool IsComplete { get; set; }
        public bool IsTruncated { get; set; }
        public ConsoleLine? Cached { get; set; }
    }
}

namespace StreamripperGui.Infrastructure;

public static class MetadataLogTail
{
    public static async Task ClearAsync(string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                // Open only an existing file and retain its identity for the running writer.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
                stream.SetLength(0);
                stream.Flush(true);
                return;
            }
            catch (IOException exception) when (attempt < 4 &&
                (exception.HResult == unchecked((int)0x80070020) || exception.HResult == unchecked((int)0x80070021)))
            {
                await Task.Delay(100).ConfigureAwait(false);
            }
        }
    }
    public static string Read(string path, int maximumBytes = 262144)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var skipped = stream.Length > maximumBytes;
        if (skipped) stream.Seek(-maximumBytes, SeekOrigin.End);
        var bytes = new byte[Math.Min(maximumBytes, (int)Math.Min(stream.Length, int.MaxValue))];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = stream.Read(bytes, count, bytes.Length - count);
            if (read == 0) break;
            count += read;
        }
        var text = System.Text.Encoding.UTF8.GetString(bytes, 0, count);
        if (skipped)
        {
            var first = text.IndexOf('\n');
            text = first < 0 ? "" : text[(first + 1)..];
        }
        var complete = text.LastIndexOf('\n');
        return complete < 0 ? "" : text[..(complete + 1)];
    }

    public static string DecodeLine(string line)
    {
        var separator = line.IndexOf('\t');
        var text = separator < 0 ? line : line[(separator + 1)..];
        var output = new System.Text.StringBuilder();
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\\' || index + 1 == text.Length) { output.Append(text[index]); continue; }
            var escaped = text[++index];
            switch (escaped)
            {
                case '\\': output.Append('\\'); break;
                case 't': output.Append('\t'); break;
                case 'r': output.Append('\r'); break;
                case 'n': output.Append('\n'); break;
                case 'x' when index + 2 < text.Length && byte.TryParse(text.AsSpan(index + 1, 2),
                    System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var value):
                    output.Append((char)value); index += 2; break;
                default: output.Append('\\').Append(escaped); break;
            }
        }
        return output.ToString();
    }
}

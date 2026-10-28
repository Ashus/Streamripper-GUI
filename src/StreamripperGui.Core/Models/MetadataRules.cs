namespace StreamripperGui.Core.Models;

public sealed record RuleDiagnostic(int Line, int Column, string Message, bool IsWarning = false)
{
    public bool HasLocation => Line > 0 && Column > 0;
    public override string ToString() => $"{Line}:{Column} · {Message}";
}
public sealed record NativeMetadataRule(int Line, char Command, string Pattern, string Replacement, string Flags, int PatternColumn, int PatternLength, int ReplacementColumn, int FlagsColumn)
{
    public bool IsExtraction => Command == 'm' && Flags.Any(c => "ACTNY".Contains(c));
}
public sealed record ParsedMetadataRules(IReadOnlyList<NativeMetadataRule> Rules, IReadOnlyList<RuleDiagnostic> Diagnostics);
public sealed record RegexCheck(string? Error, int CaptureCount = 0);
public interface INativeRegexValidator { RegexCheck Check(string pattern, bool ignoreCase); }

public static class MetadataRules
{
    public const string Template = """
    # Optional: exclude titles containing the standalone word advert.
    # m/\badvert\b/ix

    # Optional: remove the trailing text mp3pro.
    # s/\s*-?\s*mp3pro$//i

    # Artist/title extraction must remain last.
    m/^\s*(.*?[^-\s])\s+-\s+(.*)\s*$/A1T2

    """;

    public static ParsedMetadataRules Parse(string text)
    {
        var rules = new List<NativeMetadataRule>();
        var diagnostics = new List<RuleDiagnostic>();
        var lines = text.Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var line = index < lines.Length - 1 && lines[index].EndsWith('\r') ? lines[index][..^1] : lines[index];
            var number = index + 1;
            if (System.Text.Encoding.UTF8.GetByteCount(lines[index]) >= 2048)
            {
                diagnostics.Add(new(number, 1, "Each line must be shorter than 2048 UTF-8 bytes."));
                continue;
            }
            if (line.Contains('\0') || line.Contains('\r'))
            {
                diagnostics.Add(new(number, 1, "NUL and standalone CR are not allowed."));
                continue;
            }
            var start = 0;
            while (start < line.Length && char.IsWhiteSpace(line[start])) start++;
            if (start == line.Length || line[start] == '#') continue;
            if (start + 1 >= line.Length || line[start] is not ('m' or 's') || line[start + 1] != '/')
            {
                diagnostics.Add(new(number, start + 1, "Expected m/pattern/flags or s/pattern/replacement/flags."));
                continue;
            }
            var command = line[start];
            var cursor = start + 2;
            var patternColumn = cursor + 1;
            var pattern = Field(line, ref cursor);
            if (pattern is null)
            {
                diagnostics.Add(new(number, cursor + 1, "Missing closing regex delimiter."));
                continue;
            }
            var replacementColumn = cursor + 1;
            var replacement = command == 's' ? Field(line, ref cursor) : "";
            if (replacement is null)
            {
                diagnostics.Add(new(number, cursor + 1, "Missing closing replacement delimiter."));
                continue;
            }
            var flags = line[cursor..];
            var flagsColumn = cursor + 1;
            var invalid = false;
            for (var f = 0; f < flags.Length; f++)
            {
                var flag = flags[f];
                if (flag is 'i' or 'g') continue;
                if (command == 'm' && flag is 'x' or 's' or 'e') continue;
                if (command == 'm' && "ACTNY".Contains(flag))
                {
                    var firstDigit = ++f;
                    while (f < flags.Length && char.IsAsciiDigit(flags[f])) f++;
                    if (f > firstDigit && int.TryParse(flags[firstDigit..f], out var capture) && capture is >= 1 and <= 24) { f--; continue; }
                }
                diagnostics.Add(new(number, flagsColumn + Math.Min(f, flags.Length), "Invalid flag or capture index (1–24)."));
                invalid = true;
                break;
            }
            if (invalid) continue;
            if (command == 'm' && flags.Count(c => c is 'x' or 's' or 'e') > 1)
                diagnostics.Add(new(number, flagsColumn, "Do not combine the x, s and e actions in one rule.", true));
            if (flags.Contains('g')) diagnostics.Add(new(number, flagsColumn, "Streamripper accepts g but replaces only the first occurrence.", true));
            rules.Add(new(number, command, pattern, replacement, flags, patternColumn, pattern.Length, replacementColumn, flagsColumn));
        }
        return new(rules, diagnostics);
    }

    private static string? Field(string line, ref int cursor)
    {
        var start = cursor;
        var escaped = false;
        for (; cursor < line.Length; cursor++)
        {
            var character = line[cursor];
            if (character == '/' && !escaped)
            {
                var value = line[start..cursor];
                cursor++;
                return value;
            }
            escaped = character == '\\' ? !escaped : false;
        }
        return null;
    }

    public static ParsedMetadataRules Validate(string text, INativeRegexValidator validator)
    {
        var parsed = Parse(text);
        var diagnostics = parsed.Diagnostics.ToList();
        foreach (var rule in parsed.Rules)
        {
            var result = validator.Check(rule.Pattern, rule.Flags.Contains('i'));
            if (result.Error is not null) diagnostics.Add(new(rule.Line, rule.PatternColumn, result.Error));
            else
            {
                for (var index = 0; index < rule.Flags.Length; index++)
                {
                    if (!"ACTNY".Contains(rule.Flags[index])) continue;
                    var start = ++index;
                    while (index < rule.Flags.Length && char.IsAsciiDigit(rule.Flags[index])) index++;
                    if (int.TryParse(rule.Flags[start..index], out var capture) && capture > result.CaptureCount)
                        diagnostics.Add(new(rule.Line, rule.FlagsColumn + start, $"Capture group {capture} does not exist in the pattern.", true));
                    index--;
                }
            }
        }
        if (!parsed.Rules.Any(rule => rule.IsExtraction))
            diagnostics.Add(new(1, 1, "No artist/title extraction rule; the whole metadata string becomes the title.", true));
        return parsed with { Diagnostics = diagnostics };
    }

    public static string LiteralRule(string literal, bool exclude, bool suffix, bool prefix = false)
    {
        if (string.IsNullOrEmpty(literal) || literal.Any(c => c is '\r' or '\n' or '\0'))
            throw new ArgumentException("Enter nonempty literal text without line breaks.");
        var escaped = System.Text.RegularExpressions.Regex.Escape(literal).Replace("/", "\\/", StringComparison.Ordinal);
        var pattern = $"{(prefix ? "^" : "")}{escaped}{(suffix ? "$" : "")}";
        return exclude ? $"m/{pattern}/x" : $"s/{pattern}//";
    }

    public static (string Text, int Caret) InsertGenerated(string text, string rule)
    {
        var rules = Parse(text).Rules;
        var match = rule.StartsWith("s/", StringComparison.Ordinal)
            ? rules.LastOrDefault(r => r.Command == 'm')
            : rules.FirstOrDefault(r => r.Command == 's') ?? rules.FirstOrDefault();
        var position = text.Length;
        if (match is not null)
        {
            position = 0;
            for (var line = 1; line < match.Line; line++) position = text.IndexOf('\n', position) + 1;
        }
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var insertion = (position > 0 && text[position - 1] != '\n' ? newline : "") + rule + newline;
        return (text.Insert(position, insertion), position + insertion.Length);
    }
}

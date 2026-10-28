namespace StreamripperGui.Core.Console;

public sealed record ConsoleLine(long Id, ConsoleChannel Channel, string Text, bool IsComplete, bool IsTruncated);

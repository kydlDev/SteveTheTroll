using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SteveTheTroll.Tests.Integration;

/// <summary>
/// User rule (2026-11-07, PROJECT_RULES 8): a catch block is never empty and never silent. It writes one ModLog line with the exception type, its message and
/// what the code does instead, so the next log shows exactly which exception type to give its own catch.
/// A catch block counts as logging when its body calls ModLog, hands the exception to the module's Break, or rethrows.
/// </summary>
[TestFixture]
public class CatchLoggingRuleTests
{
    static readonly Regex CatchStart = new Regex(@"\bcatch\b\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{", RegexOptions.Compiled);

    static IEnumerable<string> SourceFiles()
    {
        return Directory.GetFiles(TestPaths.Src, "*.cs", SearchOption.AllDirectories);
    }

    /// The body of the block whose opening brace is at <paramref name="open"/> (nested braces counted; braces in strings and comments are rare in catch bodies).
    static string BlockBody(string text, int open)
    {
        int depth = 0;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{')
            {
                depth++;
            }
            else if (text[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return text.Substring(open + 1, i - open - 1);
                }
            }
        }

        return text.Substring(open + 1);
    }

    static bool Logs(string body)
    {
        return body.Contains("ModLog.") || body.Contains("Break(") || Regex.IsMatch(body, @"\bthrow\b");
    }

    [Test]
    public void EveryCatch_LogsTheException()
    {
        var silent = new List<string>();
        foreach (string file in SourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match m in CatchStart.Matches(text))
            {
                if (!Logs(BlockBody(text, m.Index + m.Length - 1)))
                {
                    int line = text.Substring(0, m.Index).Count(c => c == '\n') + 1;
                    silent.Add(Path.GetFileName(file) + ":" + line);
                }
            }
        }

        Assert.That(silent, Is.Empty, "PROJECT_RULES 8: every catch writes a ModLog line with the exception type, message and what the code does instead. Silent catch blocks: " + string.Join(", ", silent));
    }

    [Test]
    public void EveryLoggingCatch_NamesTheExceptionType()
    {
        var unnamed = new List<string>();
        foreach (string file in SourceFiles())
        {
            string text = File.ReadAllText(file);
            foreach (Match m in CatchStart.Matches(text))
            {
                string body = BlockBody(text, m.Index + m.Length - 1);
                bool named = body.Contains("GetType()") || body.Contains("Break(") || Regex.IsMatch(body, @"\bthrow\b");
                if (!named)
                {
                    int line = text.Substring(0, m.Index).Count(c => c == '\n') + 1;
                    unnamed.Add(Path.GetFileName(file) + ":" + line);
                }
            }
        }

        Assert.That(unnamed, Is.Empty, "PROJECT_RULES 8: the log line of a catch names the exception type (e.GetType().FullName). Catch blocks without it: " + string.Join(", ", unnamed));
    }
}

using SteveTheTroll.Configuration;

namespace SteveTheTroll.Testing.Commands;

/// <summary>
///     2026-11-06 (user: "a secret key word typed into the console unlocks the commands"): the stt_ console commands (test / debug helpers) run only after
///     stt_unlock &lt;word&gt; in this game session; the word is [General] UnlockWord in the player's own config file. No word set = locked. It is a convenience
///     lock, not security. No account ids or secrets are in the code.
/// </summary>
internal static class CommandAccess
{
    /// True after a correct stt_unlock in this game session.
    internal static bool Unlocked;

    /// Expected outcome: true when the commands may run; otherwise why not.
    internal static bool Allowed(out string why)
    {
        why = null;
        if (Unlocked)
        {
            return Unlocked;
        }

        bool haveWord = ModSettings.CommandsUnlockWord != null && !string.IsNullOrEmpty(ModSettings.CommandsUnlockWord.Value);
        why = haveWord ? "locked - type stt_unlock <word> first" : "locked - no unlock word is set in [General] UnlockWord (SteveTheTroll config)";
        return Unlocked;
    }
}
using System;
using Jotunn.Entities;
using SteveTheTroll.Configuration;

namespace SteveTheTroll.Testing.Commands;

/// <summary>stt_unlock &lt;word&gt;: unlocks the stt_ commands for this game session when the word is [General] UnlockWord.</summary>
internal sealed class UnlockCommand : ConsoleCommand
{
    public override string Name => "stt_unlock";

    public override string Help => "SteveTheTroll: unlock the stt_ commands for this game session - stt_unlock <word> (the word is [General] UnlockWord in the config)";

    public override void Run(string[] args)
    {
        string word = ModSettings.CommandsUnlockWord?.Value;
        if (string.IsNullOrEmpty(word))
        {
            Console.instance.Print("stt_unlock: no word is set - put one into [General] UnlockWord in the SteveTheTroll config");
        }
        else if (args != null && args.Length > 0 && string.Equals(string.Join(" ", args), word, StringComparison.Ordinal))
        {
            CommandAccess.Unlocked = true;
            Console.instance.Print("stt_unlock: the stt_ commands work until the game is closed");
        }
        else
        {
            Console.instance.Print("stt_unlock: wrong word");
        }
    }
}

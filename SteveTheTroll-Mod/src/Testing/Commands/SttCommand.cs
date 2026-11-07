using Jotunn.Entities;

namespace SteveTheTroll.Testing.Commands;

/// <summary>Base of every stt_ console command: checks CommandAccess, then runs Execute.</summary>
internal abstract class SttCommand : ConsoleCommand
{
    public sealed override void Run(string[] args)
    {
        string why;
        if (!CommandAccess.Allowed(out why))
        {
            Console.instance.Print($"{Name}: {why}");
            return;
        }

        Execute(args);
    }

    protected abstract void Execute(string[] args);
}
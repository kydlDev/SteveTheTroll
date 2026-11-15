using Jotunn.Entities;

namespace SteveTheTroll.Testing.Commands;

/// <summary>Base of every stt_ console command. The commands exist only while KydlTools is loaded (Plugin.Awake), so there is no unlock word.</summary>
internal abstract class SttCommand : ConsoleCommand
{
    public sealed override void Run(string[] args)
    {
        Execute(args);
    }

    protected abstract void Execute(string[] args);
}

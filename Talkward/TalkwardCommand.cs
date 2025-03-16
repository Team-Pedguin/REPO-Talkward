using REPOLib.Commands;

namespace Talkward;

[PublicAPI]
public static class TalkwardCommand
{
    [CommandInitializer]
    public static void Initialize()
    {
    }

    [CommandExecution(
        "Talkward",
        "Accesses Talkward functionality.",
        enabledByDefault: false,
        requiresDeveloperMode: false
    )]
    [CommandAlias("talkward"), CommandAlias("tw")]
    public static void Execute(string argStr)
    {
        var args = argStr.Split(' ');
        var firstArg = args.Length == 0 ? "" : args[0].ToLowerInvariant();

        switch (firstArg)
        {
            case "":
            case "help":
                Plugin.Logger!.LogInfo(
                    """
                    Talkward commands:
                    - help: Show this help message.
                    - on: Enable Talkward.
                    - off: Disable Talkward.
                    """);
                return;
            case "on":
                Plugin.Instance.Enabled = true;
                Plugin.Logger!.LogInfo("Talkward enabled.");
                return;
            case "off":
                Plugin.Instance.Enabled = false;
                Plugin.Logger!.LogInfo("Talkward disabled.");
                return;
        }
    }
}
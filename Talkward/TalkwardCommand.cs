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
     ), CommandAlias("talkward"), CommandAlias("tw")]
    public static void Execute(string argStr)
    {
        var args = argStr.Split(' ');
        var firstArg = args.Length == 0 ? "" : args[0].ToLowerInvariant();

        var log = Plugin.Logger!;
        switch (firstArg)
        {
            case "":
            case "help":
            {
                log.LogInfo(
                    """
                    Talkward commands:
                    - help: Show this help message.
                    - on: Enable Talkward.
                    - off: Disable Talkward.
                    - alert on/off: Enable/disable alerts for mobs.
                    """);
#if DEBUG
                log.LogInfo(
                    """
                    --- DEBUG ONLY ---
                    - speak <message>: Speak a message.
                    - whisper <message>: Whisper a message.
                    """);
#endif
                return;
            }
            case "on":
            {
                Plugin.Instance.Enabled = true;
                log.LogInfo("Talkward enabled.");
                return;
            }
            case "off":
            {
                Plugin.Instance.Enabled = false;
                log.LogInfo("Talkward disabled.");
                return;
            }
            case "alert":
            {
                if (args.Length < 2)
                {
                    log.LogWarning("No action taken. Usage: alert on/off");
                    return;
                }

                var alertArg = args[1].ToLowerInvariant();
                switch (alertArg)
                {
                    case "on":
                        Plugin.Instance.AlertsMobs = true;
                        log.LogInfo("Alerts for mobs enabled.");
                        break;
                    case "off":
                        Plugin.Instance.AlertsMobs = false;
                        log.LogInfo("Alerts for mobs disabled.");
                        break;
                    default:
                        log.LogWarning("No action taken. Usage: alert on/off");
                        break;
                }

                return;
            }
#if DEBUG
            case "speak":
            {
                var msg = Plugin.Sanitize(argStr.Substring(6)).Trim().ToString();
                Plugin.Instance.Speak(new()
                    {Voice = 0, Message = msg});
                return;
            }
            case "whisper":
            {
                var msg = Plugin.Sanitize(argStr.Substring(8)).Trim().ToString();
                Plugin.Instance.Speak(new()
                    {Voice = 1, Message = msg});
                return;
            }
#endif
        }
    }
}
using System.Text;
using System.Reflection;
using BepInEx.Logging;
using REPOLib.Commands;
using Unity.VisualScripting;

namespace Talkward;

[PublicAPI]
public static class TalkwardCommand
{
    private static class CommandManagerReflection
    {
        private static readonly Type CommandManagerType = typeof(Console).Assembly.GetType("REPOLib.Commands.CommandManager");
        private static readonly PropertyInfo CommandsEnabledProperty = CommandManagerType.GetProperty("CommandsEnabled", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly PropertyInfo CommandExecutionMethodsProperty = CommandManagerType.GetProperty("CommandExecutionMethods", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

        public static Dictionary<string, bool>? GetCommandsEnabled()
        {
            return CommandsEnabledProperty.GetValue(null) as Dictionary<string, bool>;
        }

        public static Dictionary<string, MethodInfo>? GetCommandExecutionMethods()
        {
            return CommandExecutionMethodsProperty.GetValue(null) as Dictionary<string, MethodInfo>;
        }

        public static bool TryGetCommandEnabled(string cmd, out bool enabled)
        {
            enabled = false;
            var commandsEnabled = GetCommandsEnabled();
            return commandsEnabled?.TryGetValue(cmd, out enabled) ?? false;
        }

        public static bool TryGetCommandExecutionMethod(string cmd, [MaybeNullWhen(false)] out MethodInfo mi)
        {
            mi = null;
            var executionMethods = GetCommandExecutionMethods();
            return executionMethods?.TryGetValue(cmd, out mi) ?? false;
        }
    }

    [CommandInitializer]
    public static void Initialize()
    {
        Console.TryExecute += line =>
        {
            var argStr = line.AsSpan();
            var indexOfFirstSpace = argStr.IndexOf(' ');
            var firstArg = indexOfFirstSpace == -1
                ? argStr
                : argStr.Slice(0, indexOfFirstSpace);

            var cmd = firstArg.ToString();

            if (!CommandManagerReflection.TryGetCommandEnabled(cmd, out var enabled))
            {
                Plugin.Logger?.LogWarning($"{cmd} was not found.");
                return false;
            }
            
            if (enabled)
            {
                Plugin.Logger?.LogWarning($"{cmd} is disabled.");
                return false;
            }

            if (!CommandManagerReflection.TryGetCommandExecutionMethod(cmd, out var mi))
                return false;

            var argStrAfterCmd = argStr.Slice(indexOfFirstSpace + 1).ToString();
            
            try
            {
                mi.InvokeOptimized(null, argStrAfterCmd);
            }
            catch (Exception ex)
            {
                Plugin.Logger?.LogError($"{ex.GetType().FullName} in {cmd}: {ex.Message}");
                return false;
            }

            return true;
        };
    }

    [CommandExecution(
         "Talkward",
         "Accesses Talkward functionality.",
         enabledByDefault: true,
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
                Plugin.Instance.TalkwardEnabled = true;
                log.LogInfo("Talkward enabled.");
                return;
            }
            case "off":
            {
                Plugin.Instance.TalkwardEnabled = false;
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
            case "log":
            {
                Plugin.Logger!.Log(LogLevel.Debug,argStr.Substring(4));
                return;
            }
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

# Talkward

A BepInEx mod for REPO that allows Twitch chat messages to be read aloud in-game using text-to-speech.

## Features

- Text-to-speech functionality for Twitch chat messages!
- Seamless integration with R.E.P.O. gameplay!
- Optional enemy alert behavior from Twitch chat TTS messages!
- Simple command system for controlling the mod!

## Installation

1. Install [R.E.P.O.](https://store.steampowered.com/app/3241660/REPO/) from Steam
2. Install the [Thunderstore Mod Manager](https://thunderstore.io/)
3. Find and install the latest release of Talkward!

## Commands

All chat commands start with `talkward` or the shorthand `tw`:

- `talkward` or `talkward help` - Show help information
- `talkward on` - Enable Talkward
- `talkward off` - Disable Talkward
- `talkward alert on/off` - Enable/disable alerts for mobs

## Configuration

Configuration options are available through the BepInEx configuration file located at:
`BepInEx/config/Talkward.cfg`

## Requirements

- R.E.P.O.
- REPOLib >= v1.4.2 (subreq. BepInEx >= v6.0.0)

## Building from Source

### Prerequisites
- Sufficiently new-ish .NET SDK
- REPO game installed (need to reference the assemblies)

### Build Steps
1. Clone the repository
2. Update the `RepoGameDir` property in `Talkward.csproj` if needed
3. Run `dotnet build` from the solution directory

## License

This project is licensed under the [MIT License](https://choosealicense.com/licenses/mit/) - see the [LICENSE.txt](LICENSE.txt) file for details.

| Permissions       | Conditions          | Limitations  |
|-------------------|---------------------|--------------|
| 🟢 Commercial use | 🔵 License          | 🔴 Liability |
| 🟢 Distribution   | 🔵 Copyright notice | 🔴 Warranty  |
| 🟢 Modification   |                     |              |
| 🟢 Private use    |                     |              |

## Credits

Developed by Team-Pedguin contributors.
- Tyler Young ([Tyler-IN](https://github.com/Tyler-IN))
# Talkward.TwitchLib

## Purpose

This project exists to solve runtime linkage issues with TwitchLib assemblies by merging dependencies into a single assembly.

## Problem

The TwitchLib ecosystem suffers from several compatibility issues:

- Released builds of TwitchLib.Client depend on TwitchLib.Communication version 1.0.3/1.0.4, but fail at runtime with missing methods and types
- The actual build of TwitchLib.Client uses an intermediate version of TwitchLib.Communication that doesn't match any published version
- The TwitchLib.Communication API changed drastically in later versions
- No non-prerelease versions of TwitchLib.Client are runtime-compatible with any available versions of TwitchLib.Communication

These issues create a frustrating developer experience where applications compile successfully but crash at runtime due to missing member exceptions.

## Solution

Talkward.TwitchLib addresses these problems by:

1. Using specific, compatible versions of the TwitchLib components
2. Merging all TwitchLib assemblies into a single DLL using ILRepack
3. Ensuring any reference or linkage issues are discovered during build time rather than runtime

By compile-time merging the TwitchLib assemblies, we:
- Eliminate runtime dependency resolution issues
- Create a single, consistent assembly with all required functionality
- Validate that all dependencies can be correctly resolved

## Implementation

This project uses:
- ILRepack.Lib.MSBuild.Task for assembly merging
- Finder.MsBuild.Task to locate .NET Standard compatible dependencies
- Specific preview versions of TwitchLib.Api and TwitchLib.Client that work together

## Usage

Reference the Talkward.TwitchLib assembly in your project instead of referencing the individual TwitchLib packages directly.

```csharp
// Use TwitchLib classes as normal
var clientOptions = new TwitchLib.Client.Models.ClientOptions();
var client = new TwitchLib.Client.TwitchClient(options: clientOptions);
```

## Notes

This is a workaround solution until the TwitchLib ecosystem stabilizes its versioning and dependency structure.

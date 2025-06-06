# com.utilities.encoder.ogg

[![Discord](https://img.shields.io/discord/855294214065487932.svg?label=&logo=discord&logoColor=ffffff&color=7389D8&labelColor=6A7EC2)](https://discord.gg/xQgMW9ufN4) [![openupm](https://img.shields.io/npm/v/com.utilities.encoder.ogg?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.utilities.encoder.ogg/) [![openupm](https://img.shields.io/badge/dynamic/json?color=brightgreen&label=downloads&query=%24.downloads&suffix=%2Fmonth&url=https%3A%2F%2Fpackage.openupm.com%2Fdownloads%2Fpoint%2Flast-month%2Fcom.utilities.encoder.ogg)](https://openupm.com/packages/com.utilities.encoder.ogg/)

Simple library for Ogg encoding support in the [Unity](https://unity.com/) Game Engine.

This package uses the open source .net ogg vorbis encoder found on [NuGet](https://www.nuget.org/packages/OggVorbisEncoder/)

## Installing

Requires Unity 2021.3 LTS or higher.

The recommended installation method is though the unity package manager and [OpenUPM](https://openupm.com/packages/com.utilities.encoder.ogg).

### Via Unity Package Manager and OpenUPM

#### Terminal

```terminal
openupm add com.utilities.encoder.ogg
```

#### Manual

- Open your Unity project settings
- Select the `Package Manager`
![scoped-registries](com.utilities.encoder.ogg/Packages/com.com.utilities.encoder.ogg/Documentation~/images/package-manager-scopes.png)
- Add the OpenUPM package registry:
  - Name: `OpenUPM`
  - URL: `https://package.openupm.com`
  - Scope(s):
    - `com.utilities`
- Open the Unity Package Manager window
- Change the Registry from Unity to `My Registries`
- Add the `Utilities.Encoder.Ogg` package

### Via Unity Package Manager and Git url

> [!WARNING]
> This repo has dependencies on other repositories! You are responsible for adding these on your own.

- Open your Unity Package Manager
- Add package from git url: `https://github.com/RageAgainstThePixel/com.utilities.encoder.ogg.git#upm`
  - [com.utilities.async](https://github.com/RageAgainstThePixel/com.utilities.async)
  - [com.utilities.audio](https://github.com/RageAgainstThePixel/com.utilities.audio)

---

## Documentation

### Table of Contents

- [Recording Behaviour](#recording-behaviour)
- [Audio Clip Extensions](#audio-clip-extensions)
  - [Encode OGG](#encode-ogg)
- [Related Packages](#related-packages)

## Recording Behaviour

Simply add the `OggRecorderBehaviour` to any GameObject to enable recording.

> This will stream the recording directly to disk as it is recorded.

## Audio Clip Extensions

Provides extensions to encode `AudioClip`s to OGG encoded bytes.
Supports 8, 16, 24, and 32 bit sample sizes.

### Encode OGG

```csharp
var bytes = audioClip.EncodeToOggVorbis();
var bytes = await audioClip.EncodeToOggVorbisAsync();
```

## Related Packages

- [Wav Encoder](https://github.com/RageAgainstThePixel/com.utilities.encoder.wav)

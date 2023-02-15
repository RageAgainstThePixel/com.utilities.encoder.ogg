# com.utilities.encoder.ogg

[![openupm](https://img.shields.io/npm/v/com.utilities.encoder.ogg?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.utilities.encoder.ogg/)

A com.utilities.encoder.ogg package for the [Unity](https://unity.com/) Game Engine.

This package uses the open source .net ogg vorbis encoder found on [NuGet](https://www.nuget.org/packages/OggVorbisEncoder/)

## Installing

### Via Unity Package Manager and OpenUPM

- Open your Unity project settings
- Select the `Package Manager`
![scoped-registries](com.utilities.encoder.ogg/Packages/com.com.utilities.encoder.ogg/Documentation~/images/package-manager-scopes.png)
- Add the OpenUPM package registry:
  - `Name: OpenUPM`
  - `URL: https://package.openupm.com`
  - `Scope(s):`
    - `com.utilities`
- Open the Unity Package Manager window
- Change the Registry from Unity to `My Registries`
- Add the `com.utilities.encoder.ogg` package

### Via Unity Package Manager and Git url

- Open your Unity Package Manager
- Add package from git url: `https://github.com/RageAgainstThePixel/com.utilities.encoder.ogg.git#upm`
  > Note: this repo has dependencies on other repositories! You are responsible for adding these on your own.
  - [com.utilities.async](https://github.com/RageAgainstThePixel/com.utilities.async)

---

## Documentation

```csharp
using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Utilities.Encoding.OggVorbis.Demo
{
    [RequireComponent(typeof(AudioSource))]
    public class DemoScript : MonoBehaviour
    {
        [SerializeField]
        private AudioSource audioSource;

        private void OnValidate()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
            }
        }

        private void Awake() => OnValidate();

        private void Start()
        {
            // Enable debugging
            RecordingManager.EnableDebug = true;

            // Set the default save location.
            RecordingManager.DefaultSaveLocation = $"{Application.streamingAssetsPath}/Resources/Recordings";

            // Set the max recording length (min 30 seconds, max 300 seconds or 5 min)
            RecordingManager.MaxRecordingLength = 60;

            // Event raised whenever a recording is completed.
            RecordingManager.OnClipRecorded += OnClipRecorded;
        }

        private void OnClipRecorded(Tuple<string, AudioClip> recording)
        {
            var (path, newClip) = recording;
            Debug.Log($"Recording saved at: {path}");
            audioSource.PlayOneShot(newClip);
        }

        private async void StartRecording()
        {
            if (RecordingManager.IsBusy)
            {
                if (RecordingManager.IsRecording)
                {
                    Debug.Log("Recording in progress");
                }

                if (RecordingManager.IsProcessing)
                {
                    Debug.Log("Processing last recording");
                }

                return;
            }

            try
            {
                // Starts the recording process
                var recording = await RecordingManager.StartRecordingAsync();
                var (path, newClip) = recording;
                Debug.Log($"Recording saved at: {path}");
                audioSource.clip = newClip;
            }
            catch (TaskCanceledException)
            {
                // Do Nothing
            }
            catch (Exception e)
            {
                Debug.LogError(e);
            }
        }

        private void EndRecording()
        {
            // Ends the recording if in progress.
            RecordingManager.EndRecording();
        }
    }
}
```

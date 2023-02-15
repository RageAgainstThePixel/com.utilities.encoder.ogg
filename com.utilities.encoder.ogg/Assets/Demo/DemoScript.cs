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

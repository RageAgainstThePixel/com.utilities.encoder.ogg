using OggVorbisEncoder;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;
using Random = System.Random;

namespace Utilities.Encoding.OggVorbis
{
    public static class RecordingManager
    {
        private static int maxRecordingLength = 300;

        private static readonly object recordingLock = new object();

        private static bool isRecording;

        private static bool isProcessing;

        private static CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// Max Recording length in seconds.
        /// The default value is 300 seconds (5 min)
        /// </summary>
        public static int MaxRecordingLength
        {
            get => maxRecordingLength;
            set
            {
                if (value != maxRecordingLength)
                {
                    if (value > 300)
                    {
                        maxRecordingLength = 300;
                    }
                    else if (value < 30)
                    {
                        maxRecordingLength = 30;
                    }
                    else
                    {
                        maxRecordingLength = value;
                    }
                }
            }
        }

        /// <summary>
        /// Is the recording manager currently recording?
        /// </summary>
        public static bool IsRecording
        {
            get
            {
                bool recording;

                lock (recordingLock)
                {
                    recording = isRecording;
                }

                return recording;
            }
        }

        /// <summary>
        /// Is the recording manager currently processing the last recording?
        /// </summary>
        public static bool IsProcessing
        {
            get
            {
                bool processing;

                lock (recordingLock)
                {
                    processing = isProcessing;
                }

                return processing;
            }
        }

        /// <summary>
        /// Indicates that the recording manager is either recording or processing the previous recording.
        /// </summary>
        public static bool IsBusy => IsProcessing || IsRecording;

        public static bool EnableDebug { get; set; }

        /// <summary>
        /// The event that is raised when an audio clip has finished recording and has been saved to disk.
        /// </summary>
        public static event Action<Tuple<string, AudioClip>> OnClipRecorded;

        private static string defaultSaveLocation;

        /// <summary>
        /// Defaults to /Assets/Resources/Recordings in editor.<br/>
        /// Defaults to /Application/TempCachePath/Recordings at runtime.
        /// </summary>
        public static string DefaultSaveLocation
        {
            get
            {
                if (string.IsNullOrWhiteSpace(defaultSaveLocation))
                {
#if UNITY_EDITOR
                    defaultSaveLocation = $"{Application.dataPath}/Resources/Recordings";
#else
                    defaultSaveLocation = $"{Application.temporaryCachePath}/Recordings";
#endif
                }

                return defaultSaveLocation;
            }
            set => defaultSaveLocation = value;
        }

        /// <summary>
        /// Starts the recording process.
        /// </summary>
        /// <param name="clipName">Optional, name for the clip.</param>
        /// <param name="saveDirectory">Optional, the directory to save the clip.</param>
        /// <param name="callback">Optional, callback when recording is complete.</param>
        /// <param name="cancellationToken">Optional, task cancellation token.</param>
        public static async void StartRecording(string clipName = null, string saveDirectory = null, Action<Tuple<string, AudioClip>> callback = null, CancellationToken cancellationToken = default)
        {
            var result = await StartRecordingAsync(clipName, saveDirectory, cancellationToken).ConfigureAwait(false);
            callback?.Invoke(result);
        }

        /// <summary>
        /// Starts the recording process.
        /// </summary>
        /// <param name="clipName">Optional, name for the clip.</param>
        /// <param name="saveDirectory">Optional, the directory to save the clip.</param>
        /// <param name="cancellationToken">Optional, task cancellation token.</param>
        public static async Task<Tuple<string, AudioClip>> StartRecordingAsync(string clipName = null, string saveDirectory = null, CancellationToken cancellationToken = default)
        {
            if (IsBusy)
            {
                Debug.LogWarning($"[{nameof(RecordingManager)}] Recording already in progress!");
                return null;
            }

            lock (recordingLock)
            {
                isRecording = true;
            }

            if (string.IsNullOrWhiteSpace(saveDirectory))
            {
                saveDirectory = DefaultSaveLocation;
            }

            var clip = Microphone.Start(null, false, MaxRecordingLength, Constants.Frequency);

            if (EnableDebug)
            {
                Microphone.GetDeviceCaps(null, out var minFreq, out var maxFreq);
                Debug.Log($"[{nameof(RecordingManager)}] Recording devices: {string.Join(", ", Microphone.devices)} | minFreq: {minFreq} | maxFreq {maxFreq} | clip freq: {clip.frequency} | samples: {clip.samples}");
            }

            clip.name = (string.IsNullOrWhiteSpace(clipName) ? Guid.NewGuid().ToString() : clipName)!;

            lock (recordingLock)
            {
                cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            }

#if UNITY_EDITOR
            if (EnableDebug)
            {
                Debug.Log($"[{nameof(RecordingManager)}] <>Disable auto refresh<>");
            }

            UnityEditor.AssetDatabase.DisallowAutoRefresh();
#endif

            try
            {
                return await StreamSaveToDiskAsync(clip, saveDirectory, cancellationTokenSource.Token).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{nameof(RecordingManager)}] Failed to record {clipName}!\n{e}");
            }
            finally
            {
                lock (recordingLock)
                {
                    isRecording = false;
                    isProcessing = false;
                }
#if UNITY_EDITOR
                await Awaiters.UnityMainThread;

                if (EnableDebug)
                {
                    Debug.Log($"[{nameof(RecordingManager)}] <>Enable auto refresh<>");
                }

                UnityEditor.AssetDatabase.AllowAutoRefresh();
#endif
            }

            return null;
        }

        private static async Task<Tuple<string, AudioClip>> StreamSaveToDiskAsync(AudioClip clip, string saveDirectory, CancellationToken cancellationToken)
        {
            if (EnableDebug)
            {
                Debug.Log($"[{nameof(RecordingManager)}] Recording started...");
            }

            lock (recordingLock)
            {
                isProcessing = true;
            }

            var lastPosition = 0;
            var clipName = clip.name;
            var maxClipLength = clip.samples;
            var samples = new float[clip.samples];

            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(1).ConfigureAwait(false);

            if (!Directory.Exists(saveDirectory))
            {
                Directory.CreateDirectory(saveDirectory);
            }

            var path = $"{saveDirectory}/{clipName}.ogg";

            if (File.Exists(path))
            {
                Debug.LogWarning($"[{nameof(RecordingManager)}] {path} already exists, attempting to delete");
                File.Delete(path);
            }

            var outStream = new FileStream(path, FileMode.Create, FileAccess.Write);

            try
            {
                var info = VorbisInfo.InitVariableBitRate(Constants.Channels, Constants.Frequency, 0.2f);

                // set up our packet->stream encoder
                var oggStream = new OggStream(new Random().Next());

                #region Header

                // =========================================================
                // HEADER
                // =========================================================
                var comments = new Comments();

                var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
                var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
                var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

                oggStream.PacketIn(infoPacket);
                oggStream.PacketIn(commentsPacket);
                oggStream.PacketIn(booksPacket);

                // Flush to force audio data onto its own page per the spec
                OggPage page;
                while (oggStream.PageOut(out page, true))
                {
                    // ReSharper disable once MethodSupportsCancellation
                    await outStream.WriteAsync(page.Header, 0, page.Header.Length).ConfigureAwait(false);
                    // ReSharper disable once MethodSupportsCancellation
                    await outStream.WriteAsync(page.Body, 0, page.Body.Length).ConfigureAwait(false);
                }

                // Flush to force audio data onto its own page per the spec
                while (oggStream.PageOut(out page, true))
                {
                    // ReSharper disable once MethodSupportsCancellation
                    await outStream.WriteAsync(page.Header, 0, page.Header.Length).ConfigureAwait(false);
                    // ReSharper disable once MethodSupportsCancellation
                    await outStream.WriteAsync(page.Body, 0, page.Body.Length).ConfigureAwait(false);
                }

                #endregion Header

                #region Body

                // =========================================================
                // BODY (Audio Data)
                // =========================================================
                var processingState = ProcessingState.Create(info);
                var modulatorData = new short[samples.Length * info.Channels];
                var readBuffer = new byte[samples.Length * sizeof(float)];
                var channelBuffer = new float[info.Channels][];

                for (int i = 0; i < info.Channels; i++)
                {
                    channelBuffer[i] = new float[samples.Length];
                }

                var shouldStop = false;

                while (!oggStream.Finished)
                {
                    await Awaiters.UnityMainThread;
                    int currentPosition = Microphone.GetPosition(null);
                    clip.GetData(samples, 0);

                    if (shouldStop)
                    {
                        Microphone.End(null);
                    }

                    // ReSharper disable once MethodSupportsCancellation
                    await Task.Delay(1).ConfigureAwait(false);

                    if (currentPosition != 0)
                    {
                        int sampleIndex = 0;

                        foreach (var pcm in samples)
                        {
                            var sample = (short)(pcm * Constants.RescaleFactor);
                            modulatorData[sampleIndex++] = sample;
                            modulatorData[sampleIndex++] = sample;
                        }

                        Buffer.BlockCopy(modulatorData, 0, readBuffer, 0, modulatorData.Length);

                        int length = currentPosition - lastPosition;

                        for (var i = 0; i < length; i++)
                        {
                            var leftReadBuffer1 = (lastPosition + i) * sizeof(float) + 1;
                            var leftReadBuffer2 = (lastPosition + i) * sizeof(float) + 0;
                            var leftRawValue = (readBuffer[leftReadBuffer1] << 8) | (0x00ff & readBuffer[leftReadBuffer2]);
                            channelBuffer[0][i] = (short)leftRawValue / Constants.RescaleFactor;
                            var rightReadBuffer1 = (lastPosition + i) * sizeof(float) + 3;
                            var rightReadBuffer2 = (lastPosition + i) * sizeof(float) + 2;
                            var rightRawValue = (readBuffer[rightReadBuffer1] << 8) | (0x00ff & readBuffer[rightReadBuffer2]);
                            channelBuffer[1][i] = (short)rightRawValue / Constants.RescaleFactor;
                        }

                        processingState.WriteData(channelBuffer, length);
                        lastPosition = currentPosition;
                    }

                    while (!oggStream.Finished &&
                           processingState.PacketOut(out var packet))
                    {
                        oggStream.PacketIn(packet);

                        while (!oggStream.Finished &&
                               oggStream.PageOut(out page, false))
                        {
                            // ReSharper disable once MethodSupportsCancellation
                            await outStream.WriteAsync(page.Header, 0, page.Header.Length).ConfigureAwait(false);
                            // ReSharper disable once MethodSupportsCancellation
                            await outStream.WriteAsync(page.Body, 0, page.Body.Length).ConfigureAwait(false);
                        }
                    }

                    await Awaiters.UnityMainThread;

                    if (EnableDebug)
                    {
                        Debug.Log($"[{nameof(RecordingManager)}] State: {nameof(IsRecording)}? {IsRecording} | {currentPosition} | isCancelled? {cancellationToken.IsCancellationRequested}");
                    }

                    if (currentPosition == maxClipLength ||
                        cancellationToken.IsCancellationRequested)
                    {
                        if (IsRecording)
                        {
                            lock (recordingLock)
                            {
                                isRecording = false;
                            }

                            if (EnableDebug)
                            {
                                Debug.Log($"[{nameof(RecordingManager)}] Finished recording...");
                            }
                        }

                        if (shouldStop)
                        {
                            if (EnableDebug)
                            {
                                Debug.Log($"[{nameof(RecordingManager)}] Writing end of stream...");
                            }

                            processingState.WriteEndOfStream();
                            break;
                        }

                        if (EnableDebug)
                        {
                            Debug.Log($"[{nameof(RecordingManager)}] Stop stream requested...");
                        }

                        // delays stopping to make sure we process the last bits of the clip
                        shouldStop = true;
                    }
                }

                #endregion Body

                if (EnableDebug)
                {
                    Debug.Log($"[{nameof(RecordingManager)}] Flush stream...");
                }

                // ReSharper disable once MethodSupportsCancellation
                await outStream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return null;
            }
            finally
            {
                if (EnableDebug)
                {
                    Debug.Log($"[{nameof(RecordingManager)}] Dispose stream...");
                }

                await outStream.DisposeAsync().ConfigureAwait(false);
            }

            if (EnableDebug)
            {
                Debug.Log($"[{nameof(RecordingManager)}] Copying recording data stream...");
            }

            var microphoneData = new float[lastPosition];
            Array.Copy(samples, microphoneData, microphoneData.Length - 1);

            await Awaiters.UnityMainThread;

            // Create a copy.
            var newClip = AudioClip.Create(clipName, microphoneData.Length, 1, Constants.Frequency, false);
            newClip.SetData(microphoneData, 0);
            var result = new Tuple<string, AudioClip>(path, newClip);

            lock (recordingLock)
            {
                isProcessing = false;
            }

            if (EnableDebug)
            {
                Debug.Log($"[{nameof(RecordingManager)}] Finished processing...");
            }

            OnClipRecorded?.Invoke(result);
            return result;
        }

        /// <summary>
        /// Ends the recording process if in progress.
        /// </summary>
        public static void EndRecording()
        {
            if (!IsRecording) { return; }

            lock (recordingLock)
            {
                if (cancellationTokenSource is { IsCancellationRequested: false })
                {
                    cancellationTokenSource.Cancel();

                    if (EnableDebug)
                    {
                        Debug.Log($"[{nameof(RecordingManager)}] End Recording requested...");
                    }
                }
            }
        }
    }
}

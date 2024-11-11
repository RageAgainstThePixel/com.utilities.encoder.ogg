// Licensed under the MIT License. See LICENSE in the project root for license information.

using OggVorbisEncoder;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Scripting;
using Utilities.Async;
using Utilities.Audio;
using Microphone = Utilities.Audio.Microphone;
using ProcessingState = OggVorbisEncoder.ProcessingState;
using Random = System.Random;

namespace Utilities.Encoding.OggVorbis
{
    public class OggEncoder : IEncoder
    {
        [Preserve]
        public OggEncoder() { }

        [Preserve]
        public static float[][] ConvertSamples(float[] samples, int channels)
        {
            var buffer = new float[channels][];

            for (var i = 0; i < channels; i++)
            {
                buffer[i] = new float[samples.Length];
            }

            for (var i = 0; i < samples.Length; i++)
            {
                var pcm = samples[i];

                for (var channel = 0; channel < channels; channel++)
                {
                    buffer[channel][i] = pcm;
                }
            }

            return buffer;
        }

        private static void ValidateSamples(float[][] samples, int channels)
        {
            for (var i = 0; i < channels - 1; i++)
            {
                if (samples[i].Length != samples[i + 1].Length)
                {
                    throw new ArgumentException("Input sample channel length must be the same size.");
                }
            }
        }

        [Preserve]
        public static byte[] ConvertToBytes(float[] samples, int sampleRate, int channels, float quality = 1f)
            => ConvertToBytes(ConvertSamples(samples, channels), sampleRate, channels, quality);

        [Preserve]
        public static byte[] ConvertToBytes(float[][] samples, int sampleRate, int channels, float quality = 1f)
        {
            ValidateSamples(samples, channels);
            WriteOggHeader(sampleRate, channels, quality, out var oggStream, out var processingState);
            using var outStream = new MemoryStream();
            var sampleLength = samples[0].Length;
            oggStream.FlushPages(outStream, false);
            ProcessChunk(oggStream, processingState, samples, sampleLength);
            processingState.WriteEndOfStream();
            oggStream.FlushPages(outStream, true);
            return outStream.ToArray();
        }

        [Preserve]
        public static async Task<byte[]> ConvertToBytesAsync(float[] samples, int sampleRate, int channels, float quality = 1f, CancellationToken cancellationToken = default)
            => await ConvertToBytesAsync(ConvertSamples(samples, channels), sampleRate, channels, quality, cancellationToken);

        [Preserve]
        public static async Task<byte[]> ConvertToBytesAsync(float[][] samples, int sampleRate, int channels, float quality = 1f, CancellationToken cancellationToken = default)
        {
            ValidateSamples(samples, channels);
            WriteOggHeader(sampleRate, channels, quality, out var oggStream, out var processingState);
            using var outStream = new MemoryStream();
            var sampleLength = samples[0].Length;
            await oggStream.FlushPagesAsync(outStream, false, cancellationToken).ConfigureAwait(false);
            ProcessChunk(oggStream, processingState, samples, sampleLength);
            processingState.WriteEndOfStream();
            await oggStream.FlushPagesAsync(outStream, true, cancellationToken).ConfigureAwait(false);
            var result = outStream.ToArray();
            await Awaiters.UnityMainThread;
            return result;
        }

        /// <inheritdoc />
        [Preserve]
        public Task StreamRecordingAsync(ClipData microphoneClipData, Action<ReadOnlyMemory<byte>> bufferCallback, CancellationToken cancellationToken, string callingMethodName = null)
            => throw new NotImplementedException("Use PCMEncoder instead");

        /// <inheritdoc />
        [Preserve]
        public async Task<Tuple<string, AudioClip>> StreamSaveToDiskAsync(ClipData clipData, string saveDirectory, Action<Tuple<string, AudioClip>> callback, CancellationToken cancellationToken, [CallerMemberName] string callingMethodName = null)
        {
            if (callingMethodName != nameof(RecordingManager.StartRecordingAsync))
            {
                throw new InvalidOperationException($"{nameof(StreamSaveToDiskAsync)} can only be called from {nameof(RecordingManager.StartRecordingAsync)}");
            }

            var outputPath = string.Empty;
            RecordingManager.IsProcessing = true;
            Tuple<string, AudioClip> result = null;

            try
            {
                Stream outStream;

                if (!string.IsNullOrWhiteSpace(saveDirectory))
                {

                    if (!Directory.Exists(saveDirectory))
                    {
                        Directory.CreateDirectory(saveDirectory);
                    }

                    outputPath = $"{saveDirectory}/{clipData.Name}.ogg";

                    if (File.Exists(outputPath))
                    {
                        Debug.LogWarning($"[{nameof(RecordingManager)}] {outputPath} already exists, attempting to delete...");
                        File.Delete(outputPath);
                    }

                    outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
                }
                else
                {
                    outStream = new MemoryStream();
                }

                var totalSampleCount = 0;
                var finalSamples = new float[clipData.MaxSamples];

                try
                {
                    WriteOggHeader(clipData.SampleRate, clipData.Channels, 0.5f, out OggStream oggStream, out ProcessingState processingState);

                    try
                    {
                        var sampleCount = 0;
                        var shouldStop = false;
                        var lastMicrophonePosition = 0;
                        var channelBuffer = new float[clipData.Channels][];
                        var sampleBuffer = new float[clipData.BufferSize];

                        for (var i = 0; i < clipData.Channels; i++)
                        {
                            channelBuffer[i] = new float[sampleBuffer.Length];
                        }

                        do
                        {
                            await Awaiters.UnityMainThread; // ensure we're on main thread to call unity apis
                            var microphonePosition = Microphone.GetPosition(clipData.Device);

                            if (microphonePosition <= 0 && lastMicrophonePosition == 0)
                            {
                                // Skip this iteration if there's no new data
                                // wait for next update
                                continue;
                            }

                            var isLooping = microphonePosition < lastMicrophonePosition;
                            int samplesToWrite;

                            if (isLooping)
                            {
                                // Microphone loopback detected.
                                samplesToWrite = clipData.BufferSize - lastMicrophonePosition;

                                if (RecordingManager.EnableDebug)
                                {
                                    Debug.LogWarning($"[{nameof(RecordingManager)}] Microphone loopback detected! [{microphonePosition} < {lastMicrophonePosition}] samples to write: {samplesToWrite}");
                                }
                            }
                            else
                            {
                                // No loopback, process normally.
                                samplesToWrite = microphonePosition - lastMicrophonePosition;
                            }

                            if (samplesToWrite > 0)
                            {
                                clipData.Clip.GetData(sampleBuffer, 0);

                                for (var i = 0; i < samplesToWrite; i++)
                                {
                                    var bufferIndex = (lastMicrophonePosition + i) % clipData.BufferSize; // Wrap around index.
                                    var sample = sampleBuffer[bufferIndex];

                                    for (var channel = 0; channel < clipData.Channels; channel++)
                                    {
                                        channelBuffer[channel][i] = sample;
                                    }

                                    // Store the sample in the final samples array.
                                    finalSamples[sampleCount * clipData.Channels + i] = sampleBuffer[bufferIndex];
                                }

                                lastMicrophonePosition = microphonePosition;
                                sampleCount += samplesToWrite;

                                if (RecordingManager.EnableDebug)
                                {
                                    Debug.Log($"[{nameof(RecordingManager)}] State: {nameof(RecordingManager.IsRecording)}? {RecordingManager.IsRecording} | Wrote {samplesToWrite} samples | last mic pos: {lastMicrophonePosition} | total samples: {sampleCount} | isCancelled? {cancellationToken.IsCancellationRequested}");
                                }

                                await FlushPagesAsync(oggStream, outStream, false).ConfigureAwait(false);
                                ProcessChunk(oggStream, processingState, channelBuffer, samplesToWrite);
                            }

                            // Check if we have recorded enough samples or if cancellation has been requested
                            if (oggStream.Finished || sampleCount >= clipData.MaxSamples || cancellationToken.IsCancellationRequested)
                            {
                                shouldStop = true;
                            }
                        } while (!shouldStop);

                        totalSampleCount = sampleCount;
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[{nameof(RecordingManager)}] Failed to write to clip file!\n{e}");
                    }
                    finally
                    {
                        // Expected to be on the Unity Main Thread.
                        await Awaiters.UnityMainThread;
                        RecordingManager.IsRecording = false;
                        Microphone.End(null);

                        if (RecordingManager.EnableDebug)
                        {
                            Debug.Log($"[{nameof(RecordingManager)}] Recording stopped, writing end of stream...");
                        }

                        processingState.WriteEndOfStream();

                        // Process any remaining packets after writing the end of stream
                        while (processingState.PacketOut(out var packet))
                        {
                            oggStream.PacketIn(packet);
                        }

                        await FlushPagesAsync(oggStream, outStream, true);

                        if (RecordingManager.EnableDebug)
                        {
                            Debug.Log($"[{nameof(RecordingManager)}] Flush stream...");
                        }

                        // ReSharper disable once MethodSupportsCancellation
                        await outStream.FlushAsync().ConfigureAwait(false);

                        if (RecordingManager.EnableDebug)
                        {
                            Debug.Log($"[{nameof(RecordingManager)}] Stream disposed. File write operation complete.");
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{nameof(RecordingManager)}] Failed to record clip!\n{e}");
                    RecordingManager.IsRecording = false;
                    RecordingManager.IsProcessing = false;
                    return null;
                }
                finally
                {
                    if (RecordingManager.EnableDebug)
                    {
                        Debug.Log($"[{nameof(RecordingManager)}] Dispose stream...");
                    }

                    await outStream.DisposeAsync().ConfigureAwait(false);
                }

                if (RecordingManager.EnableDebug)
                {
                    Debug.Log($"[{nameof(RecordingManager)}] Finalized file write. Copying recording into new AudioClip");
                }

                // Trim the final samples down into the recorded range.
                var microphoneData = new float[totalSampleCount * clipData.Channels];
                Array.Copy(finalSamples, microphoneData, microphoneData.Length);

                // Expected to be on the Unity Main Thread.
                await Awaiters.UnityMainThread;

                // Create a new copy of the final recorded clip.
                var newClip = AudioClip.Create(clipData.Name, microphoneData.Length, clipData.Channels, clipData.SampleRate, false);
                newClip.SetData(microphoneData, 0);
                result = new Tuple<string, AudioClip>(outputPath, newClip);
                callback?.Invoke(result);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            finally
            {
                RecordingManager.IsProcessing = false;

                if (RecordingManager.EnableDebug)
                {
                    Debug.Log($"[{nameof(RecordingManager)}] Finished processing...");
                }
            }
            return result;
        }

        private static void WriteOggHeader(int sampleRate, int channels, float quality, out OggStream oggStream, out ProcessingState processingState)
        {
            // Stores all the static vorbis bitstream settings
            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);

            // set up our packet->stream encoder
            var serial = new Random().Next();
            oggStream = new OggStream(serial);

            // =========================================================
            // HEADER
            // =========================================================
            // Vorbis streams begin with three headers; the initial header (with
            // most of the codec setup parameters) which is mandated by the Ogg
            // bitstream spec.  The second header holds any comment fields.  The
            // third header holds the bitstream codebook.
            var comments = new Comments();
            var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
            var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
            var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

            oggStream.PacketIn(infoPacket);
            oggStream.PacketIn(commentsPacket);
            oggStream.PacketIn(booksPacket);

            // =========================================================
            // BODY (Audio Data)
            // =========================================================
            processingState = ProcessingState.Create(info);
        }

        private static async Task FlushPagesAsync(OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out var page, force))
            {
                await output.WriteAsync(page.Header, 0, page.Header.Length).ConfigureAwait(false);
                await output.WriteAsync(page.Body, 0, page.Body.Length).ConfigureAwait(false);
            }
        }

        private static void ProcessChunk(OggStream oggStream, ProcessingState processingState, float[][] buffer, int samplesToWrite)
        {
            processingState.WriteData(buffer, samplesToWrite);

            while (!oggStream.Finished && processingState.PacketOut(out var packet))
            {
                oggStream.PacketIn(packet);
            }
        }
    }
}

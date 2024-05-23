// Licensed under the MIT License. See LICENSE in the project root for license information.

using NUnit.Framework;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using Utilities.Async;
using Utilities.Audio;

namespace Utilities.Encoding.OggVorbis.Tests
{
    public class TestFixture_01_OGG_Encoding
    {
        private const int Channels = 1;
        private const int Frequency = 44100;

        [Test]
        public void Test_01_EncodeToOgg()
        {
            // Load 16-bit sine PCM sample
            var raw16BitPcmPath = AssetDatabase.GUIDToAssetPath("252623e548216914b8abb0b584ceee72");
            Assert.IsTrue(File.Exists(raw16BitPcmPath), "16-bit PCM sample file not found");

            // Read PCM bytes
            var pcm16BitBytes = File.ReadAllBytes(raw16BitPcmPath);
            Assert.IsNotNull(pcm16BitBytes, "Failed to read 16-bit PCM bytes");
            Assert.IsNotEmpty(pcm16BitBytes, "16-bit PCM bytes array is empty");

            // Decode PCM bytes to samples
            var samples = PCMEncoder.Decode(pcm16BitBytes);
            Assert.IsNotNull(samples, "Failed to decode PCM bytes");
            Assert.IsNotEmpty(samples, "Decoded samples array is empty");

            // Create AudioClip
            var audioClip = AudioClip.Create("16bit-sine", samples.Length, Channels, Frequency, false);
            Assert.IsNotNull(audioClip, "Failed to create AudioClip");
            audioClip.SetData(samples, 0);

            // Encode to OGG
            var encodedBytes = audioClip.EncodeToOggVorbis();

            // Validate the result
            Assert.IsNotNull(encodedBytes, "Failed to encode AudioClip to OGG");
            Assert.IsNotEmpty(encodedBytes, "Encoded OGG bytes array is empty");

            // Preliminary check OGG header
            Assert.AreEqual('O', encodedBytes[0], "Incorrect OGG header");
            Assert.AreEqual('g', encodedBytes[1], "Incorrect OGG header");
            Assert.AreEqual('g', encodedBytes[2], "Incorrect OGG header");
            Assert.AreEqual('S', encodedBytes[3], "Incorrect OGG header");
        }

        [Test]
        public async Task Test_02_ConvertWavToOgg()
        {
            // Load 16-bit sine WAV sample
            var wavPath = AssetDatabase.GUIDToAssetPath("dcafdc3acfadbcc4c81ead4b57e6d8d2");
            Assert.IsTrue(File.Exists(wavPath), "WAV sample file not found");

            // Load WAV as AudioClip
            var wavAudioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(wavPath);
            Assert.IsNotNull(wavAudioClip, "Failed to load AudioClip from WAV file");
            Assert.AreEqual(1, wavAudioClip.channels, "Only mono audio is supported");

            var oggFile = "test.ogg";
            var testOggPath = Path.Combine(Application.dataPath, oggFile).Replace("\\", "/");
            Assert.IsFalse(File.Exists(testOggPath));

            try
            {
                // Convert the audio clip to OGG and save it to disk
                var pcmData = new float[wavAudioClip.samples];
                wavAudioClip.GetData(pcmData, 0);
                var oggBytes = await OggEncoder.ConvertToBytesAsync(pcmData, wavAudioClip.frequency, 1);
                await File.WriteAllBytesAsync(testOggPath, oggBytes).ConfigureAwait(true);
                Assert.IsTrue(File.Exists(testOggPath));

                // Load the OGG file as AudioClip
                var webRequest = new UnityWebRequest($"file://{testOggPath}")
                {
                    downloadHandler = new DownloadHandlerAudioClip($"file://{testOggPath}", AudioType.OGGVORBIS)
                };
                await webRequest.SendWebRequest();
                var oggAudioClip = ((DownloadHandlerAudioClip)webRequest.downloadHandler).audioClip;
                Assert.IsNotNull(oggAudioClip, "Failed to load AudioClip from OGG file");

                // Validate the AudioClip
                Assert.AreEqual(wavAudioClip.samples, oggAudioClip.samples, $"Sample count mismatch: wav={wavAudioClip.samples}, ogg={oggAudioClip.samples}");
            }
            finally
            {
                if (File.Exists(testOggPath))
                {
                    File.Delete(testOggPath);
                }
            }
        }
    }
}

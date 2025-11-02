// Licensed under the MIT License. See LICENSE in the project root for license information.

using NUnit.Framework;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
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
            // Load 16-bit PCM sample
            var raw16BitPcmPath = AssetDatabase.GUIDToAssetPath("252623e548216914b8abb0b584ceee72");
            Assert.IsTrue(File.Exists(raw16BitPcmPath), "16-bit PCM sample file not found");

            // Read PCM bytes
            using var pcm16BitBytes = new NativeArray<byte>(File.ReadAllBytes(raw16BitPcmPath), Allocator.Temp);
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
    }
}

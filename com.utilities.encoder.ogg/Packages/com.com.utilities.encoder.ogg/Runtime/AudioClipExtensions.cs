using System;
using UnityEngine;

namespace Utilities.Encoding.OggVorbis
{
    public static class AudioClipExtensions
    {
        public static byte[] EncodeAudioClipToOggVorbis(this AudioClip audioClip)
        {
            var samples = new float[audioClip.samples * audioClip.channels];
            var modulatorData = new short[samples.Length * audioClip.channels];
            var pcmData = new byte[samples.Length * sizeof(float)];

            audioClip.GetData(samples, 0);

            int sampleIndex = 0;

            foreach (var pcm in samples)
            {
                var sample = (short)(pcm * Constants.RescaleFactor);
                modulatorData[sampleIndex++] = sample;
            }

            Buffer.BlockCopy(modulatorData, 0, pcmData, 0, modulatorData.Length);

            var rawSamples = Encoder.ConvertPcmData(audioClip.frequency, audioClip.channels, pcmData, audioClip.frequency, audioClip.channels);
            var rawOggBytes = Encoder.ConvertToBytes(rawSamples, audioClip.frequency, audioClip.channels);
            return rawOggBytes;
        }
    }
}

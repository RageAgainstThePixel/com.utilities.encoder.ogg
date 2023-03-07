using System;
using System.IO;
using UnityEngine;

namespace Utilities.Encoding.OggVorbis
{
    public static class AudioClipExtensions
    {
        /// <summary>
        /// Encodes the <see cref="AudioClip"/> to OggVorbis
        /// </summary>
        /// <param name="audioClip">The <see cref="AudioClip"/> to encode.</param>
        /// <param name="trim">Optional, trim silence at beginning and end of clip.</param>
        /// <returns><see cref="AudioClip"/> encoded to OggVorbis as byte array.</returns>
        public static byte[] EncodeAudioClipToOggVorbis(this AudioClip audioClip, bool trim = false)
        {
            var sampleCount = audioClip.samples;
            var samples = new float[audioClip.samples * audioClip.channels];
            audioClip.GetData(samples, 0);

            // trim data
            var start = 0;
            var end = sampleCount - 1;

            if (trim)
            {
                for (var i = 0; i < sampleCount; i++)
                {
                    if ((short)(samples[i] * Constants.RescaleFactor) == 0)
                    {
                        continue;
                    }

                    start = i;
                    break;
                }

                for (var i = sampleCount - 1; i >= 0; i--)
                {
                    if ((short)(samples[i] * Constants.RescaleFactor) == 0)
                    {
                        continue;
                    }

                    end = i;
                    break;
                }
            }

            using var stream = new MemoryStream();

            // convert and write data
            for (var i = start; i <= end; i++)
            {
                stream.Write(BitConverter.GetBytes((short)(samples[i] * Constants.RescaleFactor)));
            }

            var rawSamples = Encoder.ConvertPcmData(audioClip.frequency, audioClip.channels, stream.ToArray(), audioClip.frequency, audioClip.channels);
            var rawOggBytes = Encoder.ConvertToBytes(rawSamples, audioClip.frequency, audioClip.channels);
            return rawOggBytes;
        }
    }
}

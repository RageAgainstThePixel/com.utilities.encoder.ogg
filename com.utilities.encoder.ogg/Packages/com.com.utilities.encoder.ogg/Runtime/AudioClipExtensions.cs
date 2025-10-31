// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Utilities.Async;
using Utilities.Audio;

namespace Utilities.Encoding.OggVorbis
{
    public static class AudioClipExtensions
    {
        /// <summary>
        /// Encodes the <see cref="AudioClip"/> to OggVorbis
        /// </summary>
        /// <param name="audioClip">The <see cref="AudioClip"/> to encode.</param>
        /// <param name="bitDepth">Optional, Bit Depth to use for encoding. Defaults to <see cref="PCMFormatSize.SixteenBit"/>.</param>
        /// <param name="trim">Optional, trim silence at beginning and end of clip.</param>
        /// <returns><see cref="AudioClip"/> encoded to OggVorbis as byte array.</returns>
        public static byte[] EncodeToOggVorbis(this AudioClip audioClip, PCMFormatSize bitDepth = PCMFormatSize.SixteenBit, bool trim = false)
        {
            var pcmData = audioClip.EncodeToPCM(bitDepth, trim);
            var samples = PCMEncoder.Decode(pcmData, bitDepth);

            try
            {
                return OggEncoder.ConvertToBytes(samples.ToArray(), audioClip.frequency, audioClip.channels);
            }
            finally
            {
                samples.Dispose();
            }
        }

        /// <summary>
        /// Encodes the <see cref="AudioClip"/> to OggVorbis
        /// </summary>
        /// <param name="audioClip">The <see cref="AudioClip"/> to encode.</param>
        /// <param name="bitDepth">Optional, Bit Depth to use for encoding. Defaults to <see cref="PCMFormatSize.SixteenBit"/>.</param>
        /// <param name="trim">Optional, trim silence at beginning and end of clip.</param>
        /// <param name="cancellationToken">Optional, <see cref="CancellationToken"/>.</param>
        /// <returns><see cref="AudioClip"/> encoded to OggVorbis as byte array.</returns>
        public static async Task<byte[]> EncodeToOggVorbisAsync(this AudioClip audioClip, PCMFormatSize bitDepth = PCMFormatSize.SixteenBit, bool trim = false, CancellationToken cancellationToken = default)
        {
            await Awaiters.UnityMainThread;
            var pcmData = audioClip.EncodeToPCM(bitDepth, trim);
            var samples = PCMEncoder.Decode(pcmData, bitDepth);

            try
            {
                return await OggEncoder.ConvertToBytesAsync(samples.ToArray(), audioClip.frequency, audioClip.channels, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                samples.Dispose();
            }
        }
    }
}

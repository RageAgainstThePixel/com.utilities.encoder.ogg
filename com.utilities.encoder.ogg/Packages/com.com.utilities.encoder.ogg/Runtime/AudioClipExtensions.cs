// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Assertions;
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
        /// <param name="trim">Optional, trim silence at beginning and end of clip.</param>
        /// <returns><see cref="AudioClip"/> encoded to OggVorbis as byte array.</returns>
        public static byte[] EncodeToOggVorbis(this AudioClip audioClip, bool trim = false)
        {
            var pcmData = audioClip.EncodeToPCM(trim);
            var rawSamples = OggEncoder.ConvertPcmData(audioClip.frequency, audioClip.channels, pcmData, audioClip.frequency, audioClip.channels);
            var rawOggBytes = OggEncoder.ConvertToBytes(rawSamples, audioClip.frequency, audioClip.channels);
            return rawOggBytes;
        }

        /// <summary>
        /// Encodes the <see cref="AudioClip"/> to OggVorbis
        /// </summary>
        /// <param name="audioClip">The <see cref="AudioClip"/> to encode.</param>
        /// <param name="trim">Optional, trim silence at beginning and end of clip.</param>
        /// <param name="cancellationToken">Optional, <see cref="CancellationToken"/>.</param>
        /// <returns><see cref="AudioClip"/> encoded to OggVorbis as byte array.</returns>
        public static async Task<byte[]> EncodeToOggVorbisAsync(this AudioClip audioClip, bool trim = false, CancellationToken cancellationToken = default)
        {
            await Awaiters.UnityMainThread;
            var pcmData = audioClip.EncodeToPCM(trim);
            var rawSamples = OggEncoder.ConvertPcmData(audioClip.frequency, audioClip.channels, pcmData, audioClip.frequency, audioClip.channels);
            var rawOggBytes = await OggEncoder.ConvertToBytesAsync(rawSamples, audioClip.frequency, audioClip.channels, cancellationToken: cancellationToken).ConfigureAwait(false);
            return rawOggBytes;
        }
    }
}

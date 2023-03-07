using System;
using System.IO;
using OggVorbisEncoder;

namespace Utilities.Encoding.OggVorbis
{
    internal static class Encoder
    {
        internal static float[][] ConvertPcmData(int outputSampleRate, int outputChannels, byte[] pcmSamples, int pcmSampleRate, int pcmChannels)
        {
            const int pcmSampleSize = 2;

            var numPcmSamples = pcmSamples.Length / pcmSampleSize / pcmChannels;
            var pcmDuration = numPcmSamples / (float)pcmSampleRate;
            var numOutputSamples = (int)(pcmDuration * outputSampleRate) / pcmChannels;
            var outSamples = new float[outputChannels][];

            for (var ch = 0; ch < outputChannels; ch++)
            {
                outSamples[ch] = new float[numOutputSamples];
            }

            for (var i = 0; i < numOutputSamples; i++)
            {
                for (var channel = 0; channel < outputChannels; channel++)
                {
                    var sampleIndex = i * pcmChannels * pcmSampleSize;

                    if (channel < pcmChannels)
                    {
                        sampleIndex += channel * pcmSampleSize;
                    }

                    var rawSample = (short)(pcmSamples[sampleIndex + 1] << 8 | pcmSamples[sampleIndex]) / Constants.RescaleFactor;

                    outSamples[channel][i] = rawSample;
                }
            }

            return outSamples;
        }

        internal static byte[] ConvertToBytes(float[][] samples, int sampleRate, int channels, float quality = 1f)
        {
            const int writeBufferSize = 1;
            using MemoryStream outputData = new MemoryStream();

            // Stores all the static vorbis bit stream settings
            var info = VorbisInfo.InitVariableBitRate(channels, sampleRate, quality);

            // set up our packet->stream encoder
            var serial = new Random().Next();
            var oggStream = new OggStream(serial);

            // =========================================================
            // HEADER
            // =========================================================
            // Vorbis streams begin with three headers; the initial header
            // (with most of the codec setup parameters) which is mandated
            // by the Ogg bitstream spec.  The second header holds any
            // comment fields.  The third header holds the bitstream codebook.
            var comments = new Comments();

            var infoPacket = HeaderPacketBuilder.BuildInfoPacket(info);
            var commentsPacket = HeaderPacketBuilder.BuildCommentsPacket(comments);
            var booksPacket = HeaderPacketBuilder.BuildBooksPacket(info);

            oggStream.PacketIn(infoPacket);
            oggStream.PacketIn(commentsPacket);
            oggStream.PacketIn(booksPacket);

            // Flush to force audio data onto its own page per the spec
            oggStream.FlushPages(outputData, true);

            // =========================================================
            // BODY (Audio Data)
            // =========================================================
            var processingState = ProcessingState.Create(info);

            for (var readIndex = 0; readIndex <= samples[0].Length; readIndex += writeBufferSize)
            {
                if (readIndex == samples[0].Length)
                {
                    processingState.WriteEndOfStream();
                }
                else
                {
                    processingState.WriteData(samples, writeBufferSize, readIndex);
                }

                while (processingState.PacketOut(out var packet))
                {
                    oggStream.PacketIn(packet);
                    oggStream.FlushPages(outputData, false);
                }
            }

            oggStream.FlushPages(outputData, true);

            return outputData.ToArray();
        }

        private static void FlushPages(this OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out var page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }
    }
}

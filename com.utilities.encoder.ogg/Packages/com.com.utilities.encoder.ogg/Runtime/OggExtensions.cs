using System.IO;
using OggVorbisEncoder;

namespace Utilities.Encoding.OggVorbis
{
    internal static class OggExtensions
    {
        public static void FlushPages(this OggStream oggStream, Stream output, bool force)
        {
            while (oggStream.PageOut(out var page, force))
            {
                output.Write(page.Header, 0, page.Header.Length);
                output.Write(page.Body, 0, page.Body.Length);
            }
        }
    }
}

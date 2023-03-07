using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public static async Task FlushPagesAsync(this OggStream oggStream, Stream output, bool force, CancellationToken cancellationToken = default)
        {
            while (oggStream.PageOut(out var page, force))
            {
                await output.WriteAsync(page.Header, 0, page.Header.Length, cancellationToken);
                await output.WriteAsync(page.Body, 0, page.Body.Length, cancellationToken);
            }
        }
    }
}

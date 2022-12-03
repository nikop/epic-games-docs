using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace EpicDocSync
{
    public class LinkQueue
    {
        private ConcurrentQueue<Uri> _uri = new();
        private HashSet<Uri> _oldUris = new();

        public int Done { get; private set; }

        public int Total { get; private set; }

        public void Queue(Uri url)
        {
            if (_oldUris.Add(url))
            {
                Total++;
                _uri.Enqueue(url);
            }
        }
  
        public bool TryDequeue([NotNullWhen(true)] out Uri? uri)
        {
            var res = _uri.TryDequeue(out uri);

            if (res)
            {
                Done++;
            }

            return res;
        }
    }
}

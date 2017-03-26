using System;

namespace BestHTTP.Caching
{
    public interface HttpCacheUriBuilder
    {
        Uri BuildCacheUri (Uri uri);
    }
}


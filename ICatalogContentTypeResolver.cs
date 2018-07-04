using System.Collections.Generic;
using EPiServer.Core;

namespace DeltaX.Commerce.Catalog
{
    public interface ICatalogContentTypeResolver
    {
        IEnumerable<ResolvedContentType> ResolveContentTypes(IEnumerable<ContentReference> contentLinks);
    }
}
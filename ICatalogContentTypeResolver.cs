using System.Collections.Generic;
using EPiServer.Core;
using Mediachase.Commerce.Catalog;

namespace DeltaX.Commerce.Catalog
{
    public interface ICatalogContentTypeResolver
    {
        IEnumerable<ResolvedContentType> ResolveContentTypes(IEnumerable<ContentReference> contentLinks);
        IEnumerable<ResolvedContentType> ResolveContentTypesFromCodes(IEnumerable<string> codes);
        IEnumerable<ResolvedContentType> ResolveContentTypesFromCodes(IEnumerable<string> codes, CatalogContentType catalogContentType);
    }
}
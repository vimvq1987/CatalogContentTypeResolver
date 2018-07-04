using System.Collections.Generic;
using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace DeltaX.Commerce.Catalog
{
    public interface ICatalogContentTypeResolver
    {
        IDictionary<ContentReference, ContentType> ResolveContentTypes(IEnumerable<ContentReference> contentLinks);
    }
}
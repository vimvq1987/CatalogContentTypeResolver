using EPiServer.Core;
using EPiServer.DataAbstraction;

namespace DeltaX.Commerce.Catalog
{
    public class ResolvedContentType
    {
        public ResolvedContentType(ContentReference contentReference, ContentType contentType)
        {
            ContentReference = contentReference;
            ContentType = contentType;
        }
        public ContentReference ContentReference { get; set; }
        public ContentType ContentType { get; set; }
    }
}

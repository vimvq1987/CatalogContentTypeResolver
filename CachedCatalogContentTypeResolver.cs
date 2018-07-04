using System;
using System.Collections.Generic;
using System.Linq;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.Framework.Cache;

namespace DeltaX.Commerce.Catalog
{
    public class CachedCatalogContentTypeResolver : ICatalogContentTypeResolver
    {
        private readonly ICatalogContentTypeResolver _internalResolver;
        private readonly ISynchronizedObjectInstanceCache _cache;

        private const string CachePrefix = "EP:ECF:ContentType:";
        private readonly Func<ContentReference, CacheEvictionPolicy> _cacheEvictionPolicyFunc;

        public CachedCatalogContentTypeResolver(ICatalogContentTypeResolver internalResolver,
                                                ISynchronizedObjectInstanceCache cache,
                                                IContentCacheKeyCreator contentCacheKeyCreator)
        {
            _internalResolver = internalResolver;
            _cache = cache;

            var masterKey = CachePrefix + "*";
            _cacheEvictionPolicyFunc = (contentLink) =>
                new CacheEvictionPolicy(TimeSpan.FromMinutes(10),
                    CacheTimeoutType.Sliding,
                    new[] { contentCacheKeyCreator.CreateCommonCacheKey(contentLink) },
                    new[] { masterKey });
        }

        public IEnumerable<ResolvedContentType> ResolveContentTypes(
            IEnumerable<ContentReference> contentLinks)
        {
            var result = GetFromCache(contentLinks, out var notCachedLinks);
            var internalResult = _internalResolver.ResolveContentTypes(notCachedLinks).ToList();
            result.AddRange(internalResult);
            foreach (var resolved in internalResult)
            {
                _cache.Insert(GetCacheKey(resolved.ContentReference), resolved.ContentType, _cacheEvictionPolicyFunc(resolved.ContentReference));
            }

            return result;
        }

        private List<ResolvedContentType> GetFromCache(IEnumerable<ContentReference> contentLinks, out List<ContentReference> notCached)
        {
            var result = new List<ResolvedContentType>();

            var notCachedLinks = new List<ContentReference>();

            foreach (var contentLink in contentLinks)
            {
                var cached = _cache.Get(GetCacheKey(contentLink)) as ContentType;
                if (cached != null)
                {
                    result.Add(new ResolvedContentType(contentLink, cached));
                }
                else
                {
                    notCachedLinks.Add(contentLink);
                }
            }
            notCached = notCachedLinks;
            return result;
        }

        private string GetCacheKey(ContentReference contentLink)
        {
            return CachePrefix + contentLink.ID;
        }
    }
}

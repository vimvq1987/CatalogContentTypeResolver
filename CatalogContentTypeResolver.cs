using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Commerce.Catalog.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAbstraction.RuntimeModel;
using EPiServer.Framework.Cache;
using Mediachase.Commerce.Catalog;
using Mediachase.Data.Provider;
using Mediachase.MetaDataPlus;
using Mediachase.MetaDataPlus.Common;

namespace DeltaX.Commerce.Catalog
{
    public class CatalogContentTypeResolver
    {
        private readonly ReferenceConverter _referenceConverter;
        private readonly ContentTypeModelRepository _contentTypeModelRepository;
        private readonly ISynchronizedObjectInstanceCache _cache;
        private readonly IContentCacheKeyCreator _contentCacheKeyCreator;

        private const string CachePrefix = "EP:ECF:ContentType:";
        private readonly Func<ContentReference, CacheEvictionPolicy> _cacheEvictionPolicyFunc;

        private readonly ContentType _catalogContentType;
        private readonly Dictionary<string, ContentTypeModel> _metaClassContentTypeModelMap;

        public CatalogContentTypeResolver(ReferenceConverter referenceConverter,
            IContentTypeRepository contentTypeRepository,
            ContentTypeModelRepository contentTypeModelRepository, ISynchronizedObjectInstanceCache cache, IContentCacheKeyCreator contentCacheKeyCreator)
        {
            _referenceConverter = referenceConverter;
            _contentTypeModelRepository = contentTypeModelRepository;
            _cache = cache;
            _contentCacheKeyCreator = contentCacheKeyCreator;
            _metaClassContentTypeModelMap = PopulateMetadataMappings();
            _catalogContentType = contentTypeRepository.Load(typeof(CatalogContent));

            var masterKey = CachePrefix + "*";
            _cacheEvictionPolicyFunc = (contentLink) =>
                new CacheEvictionPolicy(TimeSpan.FromMinutes(10), 
                    CacheTimeoutType.Sliding, 
                    new [] { _contentCacheKeyCreator.CreateCommonCacheKey(contentLink)}, 
                    new [] { masterKey });
        }

        public IDictionary<ContentReference, ContentType> ResolveContentTypes(
            IEnumerable<ContentReference> contentLinks)
        {
            var result = new Dictionary<ContentReference, ContentType>();
            var catalogContentLinks =
                contentLinks.Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.Catalog);

            foreach (var catalogContentLink in catalogContentLinks)
            {
                result.Add(catalogContentLink, _catalogContentType);
            }

            var notCachedLinks = new List<ContentReference>();

            foreach (var contentLink in contentLinks)
            {
                var cached = _cache.Get(GetCacheKey(contentLink)) as ContentType;
                if (cached != null)
                {
                    result.Add(contentLink, cached);
                }
                else
                {
                    notCachedLinks.Add(contentLink);
                }
            }

            var nodeContentLinks =
                notCachedLinks.Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.CatalogNode);
            var nodeIds = nodeContentLinks.Select(l => _referenceConverter.GetObjectId(l));

            var entryLinks =
                notCachedLinks.Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.CatalogEntry);
            var entryIds = entryLinks.Select(l => _referenceConverter.GetObjectId(l));

            var ds = LoadMetaClassNames(entryIds, nodeIds);

            foreach (var keyPair in ResolveContentTypes(ds.Tables[0], CatalogContentType.CatalogEntry))
            {
                result.Add(keyPair.Key, keyPair.Value);
                _cache.Insert(GetCacheKey(keyPair.Key), keyPair.Value, _cacheEvictionPolicyFunc(keyPair.Key));
            }

            foreach (var keyPair in ResolveContentTypes(ds.Tables[1], CatalogContentType.CatalogNode))
            {
                result.Add(keyPair.Key, keyPair.Value);
                _cache.Insert(GetCacheKey(keyPair.Key), keyPair.Value, _cacheEvictionPolicyFunc(keyPair.Key));
            }

            return result;
        }

        private DataSet LoadMetaClassNames(IEnumerable<int> entryIds, IEnumerable<int> nodeIds)
        {
            var parameters = new[]
            {
                new DataParameter("EntryIds", CreateLinkTable(entryIds)),
                new DataParameter("NodeIds", CreateLinkTable(nodeIds))
            };
            DataSet ds = DBHelper.ExecuteDataSet(MetaDataContext.Instance, CommandType.StoredProcedure,
                "ecf_GetMetaClassNames", parameters).DataSet;
            return ds;
        }

        private IEnumerable<KeyValuePair<ContentReference, ContentType>> ResolveContentTypes(DataTable table, CatalogContentType contentType)
        {
            foreach (DataRow row in table.Rows)
            {
                var id = (int)row["Id"];
                var metaClassName = row["MetaClassName"].ToString();
                var contentLink = _referenceConverter.GetContentLink(id, contentType, 0);
                if (_metaClassContentTypeModelMap.TryGetValue(metaClassName, out var contentTypeModel))
                {
                    yield return new KeyValuePair<ContentReference, ContentType>(contentLink, contentTypeModel.ExistingContentType);
                }
            }
        }

        private DataTable CreateLinkTable(IEnumerable<int> ids)
        {
            var idTable = new DataTable();
            idTable.Columns.Add("ID", typeof(int));

            foreach (var id in ids)
            {
                idTable.Rows.Add(id);
            }

            return idTable;
        }

        private Dictionary<string, ContentTypeModel> PopulateMetadataMappings()
        {
            var mappings = new Dictionary<string, ContentTypeModel>(StringComparer.OrdinalIgnoreCase);

            foreach (var contentTypeModel in _contentTypeModelRepository.List())
            {
                var metaClassName = contentTypeModel.ModelType.Name;

                if (!typeof(CatalogContentBase).IsAssignableFrom(contentTypeModel.ModelType))
                {
                    continue;
                }

                if (contentTypeModel.Attributes.TryGetSingleAttribute(out CatalogContentTypeAttribute attribute)
                    && !String.IsNullOrWhiteSpace(attribute.MetaClassName))
                {
                    metaClassName = attribute.MetaClassName;
                    mappings.Add(metaClassName, contentTypeModel);
                }
            }

            return mappings;
        }

        private string GetCacheKey(ContentReference contentLink)
        {
            return CachePrefix + contentLink.ID;
        }
    }
}

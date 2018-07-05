﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using EPiServer.Commerce.Catalog.ContentTypes;
using EPiServer.Commerce.Catalog.DataAnnotations;
using EPiServer.Core;
using EPiServer.DataAbstraction;
using EPiServer.DataAbstraction.RuntimeModel;
using Mediachase.Commerce.Catalog;
using Mediachase.Data.Provider;
using Mediachase.MetaDataPlus;
using Mediachase.MetaDataPlus.Common;

namespace DeltaX.Commerce.Catalog
{
    public class CatalogContentTypeResolver : ICatalogContentTypeResolver
    {
        private readonly ReferenceConverter _referenceConverter;
        private readonly ContentTypeModelRepository _contentTypeModelRepository;
        private readonly ContentType _catalogContentType;
        private readonly Dictionary<string, ContentTypeModel> _metaClassContentTypeModelMap;

        public CatalogContentTypeResolver(ReferenceConverter referenceConverter,
            IContentTypeRepository contentTypeRepository,
            ContentTypeModelRepository contentTypeModelRepository)
        {
            _referenceConverter = referenceConverter;
            _contentTypeModelRepository = contentTypeModelRepository;
            _metaClassContentTypeModelMap = PopulateMetadataMappings();
            _catalogContentType = contentTypeRepository.Load(typeof(CatalogContent));
        }

        public IEnumerable<ResolvedContentType> ResolveContentTypesFromCodes(IEnumerable<string> codes)
        {
            var references = _referenceConverter.GetContentLinks(codes);
            return ResolveContentTypes(references.Values);
        }

        public IEnumerable<ResolvedContentType> ResolveContentTypesFromCodes(IEnumerable<string> codes,
            CatalogContentType catalogContentType)
        {
            var references = _referenceConverter.GetContentLinks(codes, catalogContentType);
            return ResolveContentTypes(references.Values);
        }

        public IEnumerable<ResolvedContentType> ResolveContentTypes(
            IEnumerable<ContentReference> contentLinks)
        {
            var contentReferences = contentLinks as ContentReference[] ?? contentLinks.ToArray();

            var result = new List<ResolvedContentType>();

            result.AddRange(contentReferences
                            .Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.Catalog)
                            .Select(l => new ResolvedContentType(l, _catalogContentType)));

            var nodeContentLinks =
                contentReferences.Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.CatalogNode);
            var nodeIds = nodeContentLinks.Select(l => _referenceConverter.GetObjectId(l));

            var entryLinks =
                contentReferences.Where(l => _referenceConverter.GetContentType(l) == CatalogContentType.CatalogEntry);
            var entryIds = entryLinks.Select(l => _referenceConverter.GetObjectId(l));

            var ds = LoadMetaClassNames(entryIds, nodeIds);

            result.AddRange(ResolveContentTypes(ds.Tables[0], CatalogContentType.CatalogEntry));
            result.AddRange(ResolveContentTypes(ds.Tables[1], CatalogContentType.CatalogNode));

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
                "GetMetaClassNames", parameters).DataSet;
            return ds;
        }

        private IEnumerable<ResolvedContentType> ResolveContentTypes(DataTable table, CatalogContentType contentType)
        {
            foreach (DataRow row in table.Rows)
            {
                var id = (int)row["Id"];
                var metaClassName = row["MetaClassName"].ToString();
                var contentLink = _referenceConverter.GetContentLink(id, contentType, 0);
                if (_metaClassContentTypeModelMap.TryGetValue(metaClassName, out var contentTypeModel))
                {
                    yield return new ResolvedContentType(contentLink, contentTypeModel.ExistingContentType);
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
                if (!typeof(CatalogContentBase).IsAssignableFrom(contentTypeModel.ModelType))
                {
                    continue;
                }

                if (contentTypeModel.Attributes.TryGetSingleAttribute(out CatalogContentTypeAttribute attribute)
                    && !string.IsNullOrWhiteSpace(attribute.MetaClassName))
                {
                    mappings.Add(attribute.MetaClassName, contentTypeModel);
                }
            }

            return mappings;
        }
    }
}
using System.Text.Json;
using GS1Resolver.Shared.Exceptions;
using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;
using Microsoft.Extensions.Logging;

namespace GS1Resolver.Shared.Services;

/// <summary>
/// Implements v3 data entry business logic for authoring, upserts, conversions, and v2 migration.
/// </summary>
public class DataEntryLogicService : IDataEntryLogicService
{
    private readonly IResolverRepository _repository;
    private readonly IGS1ToolkitService _gs1Toolkit;
    private readonly ILogger<DataEntryLogicService> _logger;

    private const string GS1_VOC_BASE = "https://gs1.org/voc/";
    private const string DEFAULT_LINK_KEY = "https://gs1.org/voc/defaultLink";
    private const string DEFAULT_LINK_MULTI_KEY = "https://gs1.org/voc/defaultLinkMulti";

    public DataEntryLogicService(
        IResolverRepository repository,
        IGS1ToolkitService gs1Toolkit,
        ILogger<DataEntryLogicService> logger)
    {
        _repository = repository;
        _gs1Toolkit = gs1Toolkit;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ConvertPathToDocumentId(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path cannot be null or empty", nameof(path));
        }

        // Split by '/', filter empty segments, join with '_'
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return string.Join('_', segments);
    }

    /// <inheritdoc/>
    public async Task<MongoLinksetDocument> AuthorDbLinksetDocumentAsync(DataEntryV3Document v3Doc)
    {
        if (v3Doc == null)
        {
            throw new ArgumentNullException(nameof(v3Doc));
        }

        if (string.IsNullOrWhiteSpace(v3Doc.Anchor))
        {
            throw new ArgumentException("Anchor is required", nameof(v3Doc));
        }

        if (v3Doc.Links == null || v3Doc.Links.Count == 0)
        {
            throw new ArgumentException("At least one link is required", nameof(v3Doc));
        }

        // Convert anchor to document ID
        var docId = ConvertPathToDocumentId(v3Doc.Anchor);

        // Group links by GS1 vocabulary URI
        var linksetDict = new Dictionary<string, List<LinksetEntry>>();

        foreach (var link in v3Doc.Links)
        {
            // Convert linktype to full GS1 URI
            var gs1Uri = link.Linktype.StartsWith("gs1:")
                ? GS1_VOC_BASE + link.Linktype.Substring(4)
                : link.Linktype.StartsWith("http")
                    ? link.Linktype
                    : GS1_VOC_BASE + link.Linktype;

            var entry = new LinksetEntry
            {
                Href = link.Href,
                Title = link.Title,
                Type = link.Type,
                Hreflang = link.Hreflang,
                Context = link.Context
            };

            if (!linksetDict.ContainsKey(gs1Uri))
            {
                linksetDict[gs1Uri] = new List<LinksetEntry>();
            }

            linksetDict[gs1Uri].Add(entry);
        }

        // Rearrange: defaultLink first, defaultLinkMulti second, others follow
        var orderedLinkset = new Dictionary<string, List<LinksetEntry>>();

        // Add defaultLink first if present (use only first entry for defaultLink)
        if (linksetDict.ContainsKey(DEFAULT_LINK_KEY))
        {
            orderedLinkset[DEFAULT_LINK_KEY] = new List<LinksetEntry> { linksetDict[DEFAULT_LINK_KEY][0] };
        }

        // Add defaultLinkMulti second if present (preserve all entries)
        if (linksetDict.ContainsKey(DEFAULT_LINK_MULTI_KEY))
        {
            orderedLinkset[DEFAULT_LINK_MULTI_KEY] = linksetDict[DEFAULT_LINK_MULTI_KEY];
        }

        // Add remaining links (excluding default keys)
        foreach (var kvp in linksetDict.Where(k => k.Key != DEFAULT_LINK_KEY && k.Key != DEFAULT_LINK_MULTI_KEY))
        {
            orderedLinkset[kvp.Key] = kvp.Value;
        }

        // Create linkset object with item description and link types
        var linksetObj = new LinksetObject
        {
            ItemDescription = v3Doc.ItemDescription,
            LinkTypes = orderedLinkset
        };

        // Create data item
        var dataItem = new LinksetDataItem
        {
            Qualifiers = v3Doc.Qualifiers ?? new List<Dictionary<string, string>>(),
            Linkset = linksetObj
        };

        // Create MongoLinksetDocument (no top-level anchor or itemDescription)
        var mongoDoc = new MongoLinksetDocument
        {
            Id = docId,
            DefaultLinktype = v3Doc.DefaultLinktype,
            Data = new List<LinksetDataItem> { dataItem }
        };

        return await Task.FromResult(mongoDoc);
    }

    /// <inheritdoc/>
    public async Task<List<MongoLinksetDocument>> AuthorDbLinksetListAsync(List<DataEntryV3Document> v3Docs)
    {
        if (v3Docs == null || v3Docs.Count == 0)
        {
            return new List<MongoLinksetDocument>();
        }

        // Author each document
        var authoredDocs = new List<MongoLinksetDocument>();
        foreach (var doc in v3Docs)
        {
            var authored = await AuthorDbLinksetDocumentAsync(doc);
            authoredDocs.Add(authored);
        }

        // Merge documents with same ID
        var mergedDict = new Dictionary<string, MongoLinksetDocument>();
        foreach (var doc in authoredDocs)
        {
            if (!mergedDict.ContainsKey(doc.Id))
            {
                mergedDict[doc.Id] = doc;
            }
            else
            {
                // Merge data arrays
                mergedDict[doc.Id].Data.AddRange(doc.Data);
                // Preserve DefaultLinktype from first document if not already set
                mergedDict[doc.Id].DefaultLinktype ??= doc.DefaultLinktype;
                // Keep first ItemDescription at document level (used as fallback)
                // Each data item has its own ItemDescription now
            }
        }

        return mergedDict.Values.ToList();
    }

    /// <inheritdoc/>
    public async Task<(ResolverDocument Document, int StatusCode)> ProcessDocumentUpsertAsync(
        MongoLinksetDocument newDoc,
        IResolverRepository repo)
    {
        if (newDoc == null)
        {
            throw new ArgumentNullException(nameof(newDoc));
        }

        try
        {
            // Always fetch the existing document to merge properly
            // Since CreateAsync now uses UpsertItemAsync, we need to always merge to avoid race conditions
            var existing = await repo.GetByIdAsync(newDoc.Id);
            var existingData = existing?.Data ?? new List<LinksetDataItem>();

            // Merge new data items with existing ones
            foreach (var newDataItem in newDoc.Data)
            {
                // Find matching data item by qualifiers
                var matchingItem = FindMatchingDataItem(existingData, newDataItem.Qualifiers);

                if (matchingItem != null)
                {
                    // Merge linksets for matching qualifier
                    MergeLinksetObjects(matchingItem.Linkset, newDataItem.Linkset);
                }
                else
                {
                    // Append new data item with different qualifiers
                    existingData.Add(newDataItem);
                }
            }

            // Create or update the document (no top-level anchor or itemDescription)
            var document = new ResolverDocument
            {
                Id = newDoc.Id,
                DefaultLinktype = newDoc.DefaultLinktype ?? existing?.DefaultLinktype,
                Data = existingData
            };

            // Use CreateAsync (which does Upsert) to handle both create and update
            var result = await repo.CreateAsync(document);
            var statusCode = existing == null ? 201 : 200;
            return (result, statusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during document upsert for ID: {Id}", newDoc.Id);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<DataEntryV3Document>> ConvertMongoLinksetToV3Async(ResolverDocument linksetDoc)
    {
        if (linksetDoc == null)
        {
            throw new ArgumentNullException(nameof(linksetDoc));
        }

        var v3Docs = new List<DataEntryV3Document>();

        foreach (var dataItem in linksetDoc.Data ?? new List<LinksetDataItem>())
        {
            // Derive anchor from document ID (convert 01_09506000134376 to /01/09506000134376)
            var anchor = $"/{linksetDoc.Id.Replace('_', '/')}";

            var v3Doc = new DataEntryV3Document
            {
                Anchor = anchor,
                ItemDescription = dataItem.Linkset.ItemDescription,
                DefaultLinktype = linksetDoc.DefaultLinktype,
                Qualifiers = dataItem.Qualifiers?.Count > 0 ? dataItem.Qualifiers : null,
                Links = new List<LinkV3>()
            };

            // Convert linkset entries to v3 links
            foreach (var kvp in dataItem.Linkset.LinkTypes)
            {
                var linktype = kvp.Key.StartsWith(GS1_VOC_BASE)
                    ? "gs1:" + kvp.Key.Substring(GS1_VOC_BASE.Length)
                    : kvp.Key;

                foreach (var entry in kvp.Value)
                {
                    v3Doc.Links.Add(new LinkV3
                    {
                        Linktype = linktype,
                        Href = entry.Href,
                        Title = entry.Title,
                        Type = entry.Type,
                        Hreflang = entry.Hreflang,
                        Context = entry.Context
                    });
                }
            }

            v3Docs.Add(v3Doc);
        }

        return await Task.FromResult(v3Docs);
    }

    /// <inheritdoc/>
    public async Task<List<CreateResult>> CreateDocumentAsync(List<DataEntryV3Document> docs)
    {
        if (docs == null || docs.Count == 0)
        {
            throw new ArgumentException("Documents list cannot be empty", nameof(docs));
        }

        var results = new List<CreateResult>();

        // Author documents
        var authoredDocs = await AuthorDbLinksetListAsync(docs);

        foreach (var doc in authoredDocs)
        {
            try
            {
                // Upsert (merge/append logic in ProcessDocumentUpsertAsync)
                var (createdDoc, statusCode) = await ProcessDocumentUpsertAsync(doc, _repository);

                results.Add(new CreateResult
                {
                    Id = createdDoc.Id,
                    Status = statusCode,
                    Message = statusCode == 201 ? "Created" : "Updated"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating document with ID: {Id}", doc.Id);
                results.Add(new CreateResult
                {
                    Id = doc.Id,
                    Status = 500,
                    Message = ex.Message
                });
            }
        }

        return results;
    }

    /// <inheritdoc/>
    public async Task<List<DataEntryV3Document>> ReadDocumentAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty", nameof(id));
        }

        var doc = await _repository.GetByIdAsync(id);
        if (doc == null)
        {
            throw new NotFoundException($"Document with ID '{id}' not found");
        }

        return await ConvertMongoLinksetToV3Async(doc);
    }

    /// <inheritdoc/>
    public async Task<List<string>> ReadIndexAsync()
    {
        var allDocumentIds = await _repository.GetAllDocumentIdsAsync();
        var formattedPaths = new List<string>();

        foreach (var id in allDocumentIds)
        {
            // Convert ID back to path format
            var path = "/" + id.Replace('_', '/');
            formattedPaths.Add(path);
        }

        return formattedPaths;
    }

    /// <inheritdoc/>
    public async Task<List<DataEntryV3Document>> MigrateV2Async(DataEntryV2Document v2Doc)
    {
        if (v2Doc == null)
        {
            throw new ArgumentNullException(nameof(v2Doc));
        }

        // Build anchor from v2 format
        var anchor = $"/{v2Doc.IdentificationKeyType}/{v2Doc.IdentificationKey}";
        if (!string.IsNullOrWhiteSpace(v2Doc.QualifierPath))
        {
            anchor += v2Doc.QualifierPath;
        }

        // Parse qualifiers from qualifier path
        List<Dictionary<string, string>>? qualifiers = null;
        if (!string.IsNullOrWhiteSpace(v2Doc.QualifierPath))
        {
            qualifiers = ParseQualifierPath(v2Doc.QualifierPath);
        }

        // Convert responses to v3 links
        var links = new List<LinkV3>();
        string? defaultLinktype = null;

        if (v2Doc.Responses != null)
        {
            foreach (var response in v2Doc.Responses.Where(r => r.Active))
            {
                var link = new LinkV3
                {
                    Linktype = response.LinkType.StartsWith("gs1:") ? response.LinkType : $"gs1:{response.LinkType}",
                    Href = response.TargetUrl,
                    Title = response.LinkTitle,
                    Type = response.MimeType
                };

                if (!string.IsNullOrWhiteSpace(response.IanaLanguage))
                {
                    link.Hreflang = new List<string> { response.IanaLanguage };
                }

                if (!string.IsNullOrWhiteSpace(response.Context))
                {
                    link.Context = new List<string> { response.Context };
                }

                links.Add(link);

                if (response.DefaultLinkType)
                {
                    defaultLinktype = link.Linktype;
                }
            }
        }

        var v3Doc = new DataEntryV3Document
        {
            Anchor = anchor,
            ItemDescription = v2Doc.ItemDescription,
            DefaultLinktype = defaultLinktype,
            Qualifiers = qualifiers,
            Links = links
        };

        return await Task.FromResult(new List<DataEntryV3Document> { v3Doc });
    }

    /// <inheritdoc/>
    public async Task DeleteDocumentAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("ID cannot be null or empty", nameof(id));
        }

        try
        {
            await _repository.DeleteAsync(id);
        }
        catch (NotFoundException)
        {
            // Repository threw NotFoundException - rethrow as service-level NotFoundException
            throw new NotFoundException($"Document with ID '{id}' not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document with ID: {Id}", id);
            throw;
        }
    }

    #region Private Helper Methods

    private LinksetDataItem? FindMatchingDataItem(
        List<LinksetDataItem> dataItems,
        List<Dictionary<string, string>> qualifiers)
    {
        foreach (var item in dataItems)
        {
            if (QualifiersMatch(item.Qualifiers, qualifiers))
            {
                return item;
            }
        }
        return null;
    }

    private bool QualifiersMatch(
        List<Dictionary<string, string>> qualifiers1,
        List<Dictionary<string, string>> qualifiers2)
    {
        if (qualifiers1.Count != qualifiers2.Count)
        {
            return false;
        }

        // Order-insensitive comparison: each dict in qualifiers1 must have a match in qualifiers2
        var matched = new HashSet<int>();

        foreach (var q1 in qualifiers1)
        {
            bool foundMatch = false;

            for (int i = 0; i < qualifiers2.Count; i++)
            {
                if (matched.Contains(i))
                {
                    continue; // Already matched
                }

                var q2 = qualifiers2[i];

                // Compare dictionaries (order-insensitive key comparison)
                if (DictionariesMatch(q1, q2))
                {
                    matched.Add(i);
                    foundMatch = true;
                    break;
                }
            }

            if (!foundMatch)
            {
                return false; // No match found for q1
            }
        }

        return true;
    }

    private bool DictionariesMatch(Dictionary<string, string> dict1, Dictionary<string, string> dict2)
    {
        if (dict1.Count != dict2.Count)
        {
            return false;
        }

        foreach (var kvp in dict1)
        {
            if (!dict2.ContainsKey(kvp.Key) || dict2[kvp.Key] != kvp.Value)
            {
                return false;
            }
        }

        return true;
    }

    private void MergeLinksetObjects(LinksetObject target, LinksetObject source)
    {
        // Update ItemDescription if provided
        target.ItemDescription = source.ItemDescription ?? target.ItemDescription;

        // Merge LinkTypes
        foreach (var kvp in source.LinkTypes)
        {
            if (!target.LinkTypes.ContainsKey(kvp.Key))
            {
                target.LinkTypes[kvp.Key] = new List<LinksetEntry>();
            }

            // Add entries that don't already exist (based on href)
            foreach (var entry in kvp.Value)
            {
                if (!target.LinkTypes[kvp.Key].Any(e => e.Href == entry.Href))
                {
                    target.LinkTypes[kvp.Key].Add(entry);
                }
            }
        }
    }

    private List<Dictionary<string, string>> ParseQualifierPath(string qualifierPath)
    {
        var qualifiers = new List<Dictionary<string, string>>();

        // Parse path like "/21/12345/10/ABC" into [{"21": "12345"}, {"10": "ABC"}]
        var segments = qualifierPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0; i < segments.Length; i += 2)
        {
            if (i + 1 < segments.Length)
            {
                qualifiers.Add(new Dictionary<string, string>
                {
                    { segments[i], segments[i + 1] }
                });
            }
        }

        return qualifiers;
    }

    #endregion
}

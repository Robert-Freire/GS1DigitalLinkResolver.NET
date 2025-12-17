using GS1Resolver.Shared.Models;
using GS1Resolver.Shared.Repositories;

namespace GS1Resolver.Shared.Services;

/// <summary>
/// Service interface for v3 data entry business logic.
/// Handles authoring, upserts, conversions, and v2 migration.
/// </summary>
public interface IDataEntryLogicService
{
    /// <summary>
    /// Authors a single v3 document into a MongoLinksetDocument ready for database storage.
    /// Converts anchor to document ID, groups links by GS1 vocabulary, and handles default links.
    /// </summary>
    /// <param name="v3Doc">The v3 document to author.</param>
    /// <returns>A MongoLinksetDocument ready for database operations.</returns>
    Task<MongoLinksetDocument> AuthorDbLinksetDocumentAsync(DataEntryV3Document v3Doc);

    /// <summary>
    /// Authors multiple v3 documents into MongoLinksetDocuments, merging documents with the same anchor.
    /// </summary>
    /// <param name="v3Docs">List of v3 documents to author.</param>
    /// <returns>List of MongoLinksetDocuments with merged data arrays for matching anchors.</returns>
    Task<List<MongoLinksetDocument>> AuthorDbLinksetListAsync(List<DataEntryV3Document> v3Docs);

    /// <summary>
    /// Processes document upsert logic: matches qualifiers, merges linksets, and updates or creates in database.
    /// </summary>
    /// <param name="newDoc">The new document to upsert.</param>
    /// <param name="repo">Repository for database operations.</param>
    /// <returns>Tuple of updated/created document and HTTP status code.</returns>
    Task<(ResolverDocument Document, int StatusCode)> ProcessDocumentUpsertAsync(
        MongoLinksetDocument newDoc,
        IResolverRepository repo);

    /// <summary>
    /// Converts a MongoLinksetDocument from database storage back to v3 format.
    /// </summary>
    /// <param name="linksetDoc">The linkset document from database.</param>
    /// <returns>List of v3 documents (one per data item).</returns>
    Task<List<DataEntryV3Document>> ConvertMongoLinksetToV3Async(ResolverDocument linksetDoc);

    /// <summary>
    /// Converts a GS1 Digital Link path to a document ID by splitting and filtering segments.
    /// Example: "/01/09506000134376/21/12345" -> "01_09506000134376_21_12345"
    /// </summary>
    /// <param name="path">The GS1 Digital Link path.</param>
    /// <returns>Document ID suitable for database storage.</returns>
    string ConvertPathToDocumentId(string path);

    /// <summary>
    /// Creates documents in the database. Handles delete-if-exists and upsert logic.
    /// </summary>
    /// <param name="docs">List of v3 documents to create.</param>
    /// <returns>List of creation results with IDs and status codes.</returns>
    Task<List<CreateResult>> CreateDocumentAsync(List<DataEntryV3Document> docs);

    /// <summary>
    /// Reads a document by ID and converts to v3 format.
    /// </summary>
    /// <param name="id">Document ID to read.</param>
    /// <returns>List of v3 documents.</returns>
    /// <exception cref="NotFoundException">Thrown if document not found.</exception>
    Task<List<DataEntryV3Document>> ReadDocumentAsync(string id);

    /// <summary>
    /// Reads the index of all document IDs and converts to formatted anchor paths.
    /// </summary>
    /// <returns>List of formatted anchor paths (e.g., "/01/09506000134376").</returns>
    Task<List<string>> ReadIndexAsync();

    /// <summary>
    /// Migrates a legacy v2 document to v3 format. One-time migration support.
    /// </summary>
    /// <param name="v2Doc">Legacy v2 document.</param>
    /// <returns>List of v3 documents.</returns>
    Task<List<DataEntryV3Document>> MigrateV2Async(DataEntryV2Document v2Doc);

    /// <summary>
    /// Deletes a document by ID.
    /// </summary>
    /// <param name="id">Document ID to delete.</param>
    /// <exception cref="NotFoundException">Thrown if document not found.</exception>
    Task DeleteDocumentAsync(string id);
}

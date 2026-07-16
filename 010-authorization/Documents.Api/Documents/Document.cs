using System.Collections.Concurrent;

namespace Documents.Api.Documents;

public sealed record Document(string Id, string Title, string Body, bool Published);

public sealed class DocumentStore
{
    private readonly ConcurrentDictionary<string, Document> _documents = new();

    public DocumentStore()
    {
        Save(new Document("doc-1", "Welcome", "Public welcome note.", Published: true));
    }

    public Document? Get(string id) => _documents.GetValueOrDefault(id);

    public IReadOnlyList<Document> List() => _documents.Values.OrderBy(d => d.Id, StringComparer.Ordinal).ToList();

    public Document Save(Document document)
    {
        _documents[document.Id] = document;
        return document;
    }
}

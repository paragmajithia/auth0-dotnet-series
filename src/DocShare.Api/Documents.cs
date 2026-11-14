namespace DocShare.Api;

// Why: a simple shape for a document, tagged with the org it belongs to.
public record Document(int Id, string Org, string Title);

// Why: an in-memory store keeps Episode 1 free of a database. Synthetic data only.
public class DocumentStore
{
    private readonly List<Document> _documents =
    [
        new(1, "Acme", "Acme Q3 Plan"),
        new(2, "Acme", "Acme Onboarding Guide"),
        new(3, "Globex", "Globex Roadmap"),
        new(4, "Globex", "Globex Holiday Schedule")
    ];

    public IReadOnlyList<Document> All() => _documents;

    public Document? Find(int id) => _documents.FirstOrDefault(d => d.Id == id);

    public Document Add(string org, string title)
    {
        var doc = new Document(_documents.Max(d => d.Id) + 1, org, title);
        _documents.Add(doc);
        return doc;
    }
}

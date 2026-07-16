using Nuvora.Nexus.Relay.Auth.Attributes;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Queries;
using Nuvora.Nexus.Relay.Http.Attributes;
using Nuvora.Nexus.Relay.Web.Exceptions;

namespace Documents.Api.Documents;

// Authorization is declared on the MESSAGE, not the endpoint. The authorization pipeline behaviors
// (priority 0, from AddRelayAuth) enforce these and throw UnauthorizedException (401) / ForbiddenException
// (403), which the Web layer maps to status codes. With AddRelayAuth, an UNattributed message would
// require an authenticated user (fail-closed) — so every message here is explicit.

[RelayHttpGet("/documents")]
[RelayHttpTag("Documents")]
[AllowAnonymous] // public listing
public sealed record ListDocumentsQuery : IQuery<IReadOnlyList<Document>>;

[RelayHttpGet("/documents/{id}")]
[RelayHttpTag("Documents")]
[RequireAuthentication] // any signed-in user
public sealed record GetDocumentQuery(string Id) : IQuery<Document?>;

[RelayHttpPost("/documents")]
[RelayHttpTag("Documents")]
[RequireRole("Editor")] // must hold the Editor role
[SkipTransaction]
public sealed record CreateDocumentCommand(string Title, string Body) : ICommand<Document>;

[RelayHttpPost("/documents/{id}/publish")]
[RelayHttpTag("Documents")]
[RequirePermission("documents:publish")] // must hold this permission
[SkipTransaction]
public sealed record PublishDocumentCommand(string Id) : ICommand<Document>;

public sealed class ListDocumentsQueryHandler(DocumentStore store) : IQueryHandler<ListDocumentsQuery, IReadOnlyList<Document>>
{
    public Task<IReadOnlyList<Document>> Handle(ListDocumentsQuery query, CancellationToken cancellationToken)
        => Task.FromResult(store.List());
}

public sealed class GetDocumentQueryHandler(DocumentStore store) : IQueryHandler<GetDocumentQuery, Document?>
{
    public Task<Document?> Handle(GetDocumentQuery query, CancellationToken cancellationToken)
        => Task.FromResult(store.Get(query.Id));
}

public sealed class CreateDocumentCommandHandler(DocumentStore store) : ICommandHandler<CreateDocumentCommand, Document>
{
    public Task<Document> Handle(CreateDocumentCommand command, CancellationToken cancellationToken)
        => Task.FromResult(store.Save(new Document($"doc-{Guid.NewGuid():N}", command.Title, command.Body, Published: false)));
}

public sealed class PublishDocumentCommandHandler(DocumentStore store) : ICommandHandler<PublishDocumentCommand, Document>
{
    public Task<Document> Handle(PublishDocumentCommand command, CancellationToken cancellationToken)
    {
        var document = store.Get(command.Id) ?? throw new NotFoundException($"Document '{command.Id}' was not found.");
        return Task.FromResult(store.Save(document with { Published = true }));
    }
}

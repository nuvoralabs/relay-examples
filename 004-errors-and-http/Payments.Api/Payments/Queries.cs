using Nuvora.Nexus.Relay.Auth.Attributes;
using Nuvora.Nexus.Relay.Core.Application.Queries;
using Nuvora.Nexus.Relay.Http.Attributes;
using Nuvora.Nexus.Relay.Web.Exceptions;

namespace Payments.Api.Payments;

// Article 001 returned Product? and let a null map to 404. Here we show the other idiom: a query that
// returns a non-nullable result and throws NotFoundException, which the Web mapper turns into a 404
// ProblemDetails whose detail message ("Payment 'x' was not found.") is safe to expose because
// NotFoundException is part of the RelayException hierarchy. [AllowAnonymous] keeps the endpoint
// public (Relay is fail-closed; see Commands.cs / article 010).

[RelayHttpGet("/payments/{id}")]
[RelayHttpTag("Payments")]
[AllowAnonymous]
public sealed record GetPaymentQuery(string Id) : IQuery<Payment>;

public sealed class GetPaymentQueryHandler(PaymentStore store) : IQueryHandler<GetPaymentQuery, Payment>
{
    public Task<Payment> Handle(GetPaymentQuery query, CancellationToken cancellationToken)
        => Task.FromResult(store.Get(query.Id)
            ?? throw new NotFoundException($"Payment '{query.Id}' was not found."));
}

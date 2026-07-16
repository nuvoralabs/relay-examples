using FluentValidation.Results;
using Nuvora.Nexus.Relay.Auth.Attributes;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Http.Attributes;
using Nuvora.Nexus.Relay.Web.Exceptions;

namespace Payments.Api.Payments;

// This sample's whole point is the error surface. Each path below produces a different, well-mapped
// HTTP status: pipeline validation → 400; a handler-thrown Web ValidationException → 400 with field
// errors; a BusinessRuleException → 422; a custom InsufficientFundsException → 402; a NotFound → 404;
// a ConflictException → 409. None of these status codes are written in the handlers — the Web layer
// maps the exception type to a status and a ProblemDetails body.
//
// [AllowAnonymous]: Relay is fail-closed and refuses to start unless every command/query declares an
// authorization posture. These endpoints are public; article 010 covers real authorization.

[RelayHttpPost("/payments")]
[RelayHttpTag("Payments")]
[SkipTransaction]
[AllowAnonymous]
public sealed record AuthorizePaymentCommand(decimal Amount, string Currency, string Card) : ICommand<Payment>;

[RelayHttpPost("/payments/{id}/capture")]
[RelayHttpTag("Payments")]
[SkipTransaction]
[AllowAnonymous]
public sealed record CapturePaymentCommand(string Id) : ICommand<Payment>;

/// <summary>Shape validation. Failures here become a pipeline ValidationException → HTTP 400.</summary>
public sealed class AuthorizePaymentCommandValidator : ICommandValidator<AuthorizePaymentCommand, Payment>
{
    public Task<ValidationResult> ValidateAsync(AuthorizePaymentCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (command.Amount <= 0m)
            failures.Add(new ValidationFailure(nameof(command.Amount), "Amount must be greater than zero."));
        if (string.IsNullOrWhiteSpace(command.Currency) || command.Currency.Trim().Length != 3)
            failures.Add(new ValidationFailure(nameof(command.Currency), "Currency must be a 3-letter ISO code."));
        if (string.IsNullOrWhiteSpace(command.Card))
            failures.Add(new ValidationFailure(nameof(command.Card), "Card is required."));
        return Task.FromResult(new ValidationResult(failures));
    }
}

public sealed class AuthorizePaymentCommandHandler(PaymentStore store) : ICommandHandler<AuthorizePaymentCommand, Payment>
{
    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase) { "USD", "EUR", "GBP" };

    public Task<Payment> Handle(AuthorizePaymentCommand command, CancellationToken cancellationToken)
    {
        var currency = command.Currency.Trim().ToUpperInvariant();

        // A domain validation the shape validator can't express → HTTP 400 *with field errors* (the
        // Web ValidationException carries an Errors dictionary that ends up in ProblemDetails.errors).
        if (!Supported.Contains(currency))
        {
            throw new ValidationException(
                "Unsupported currency",
                new Dictionary<string, string[]>
                {
                    ["currency"] = [$"'{currency}' is not supported. Use USD, EUR, or GBP."],
                });
        }

        // A business rule → HTTP 422 Unprocessable Entity.
        if (command.Amount > 10_000m)
            throw new BusinessRuleException($"Amount {command.Amount} {currency} exceeds the per-transaction limit of 10000.");

        // A declined card → our own exception, mapped to HTTP 402 Payment Required.
        if (IsDeclined(command.Card))
            throw new InsufficientFundsException("The card was declined for insufficient funds.");

        var payment = new Payment(
            Id: $"pay_{Guid.NewGuid():N}",
            Amount: command.Amount,
            Currency: currency,
            MaskedCard: Mask(command.Card),
            Status: "Authorized");

        return Task.FromResult(store.Save(payment));
    }

    private static bool IsDeclined(string card)
        => card.Replace(" ", string.Empty).EndsWith("0000", StringComparison.Ordinal);

    private static string Mask(string card)
    {
        var digits = new string(card.Where(char.IsDigit).ToArray());
        var last4 = digits.Length >= 4 ? digits[^4..] : digits;
        return $"**** **** **** {last4}";
    }
}

public sealed class CapturePaymentCommandHandler(PaymentStore store) : ICommandHandler<CapturePaymentCommand, Payment>
{
    public Task<Payment> Handle(CapturePaymentCommand command, CancellationToken cancellationToken)
    {
        var payment = store.Get(command.Id)
            ?? throw new NotFoundException($"Payment '{command.Id}' was not found."); // → 404

        if (payment.Status == "Captured")
            throw new ConflictException($"Payment '{command.Id}' has already been captured."); // → 409

        return Task.FromResult(store.Save(payment with { Status = "Captured" }));
    }
}

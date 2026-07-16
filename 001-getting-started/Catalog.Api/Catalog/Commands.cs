using FluentValidation.Results;
using Nuvora.Nexus.Relay.Auth.Attributes;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Http.Attributes;
using Nuvora.Nexus.Relay.Web.Exceptions;

namespace Catalog.Api.Catalog;

// ── Commands ─────────────────────────────────────────────────────────────────────────────────────
//
// Commands change state and are named with a verb. They are immutable records implementing
// ICommand<TResult>. The [RelayHttp*] attribute turns each command into an HTTP endpoint when
// MapRelayEndpoints() runs. [SkipTransaction] opts the command out of the automatic transactional
// pipeline — this sample keeps state in memory, so there is no unit of work to wrap.
//
// [AllowAnonymous] declares the endpoint public. Relay is fail-closed: it refuses to start if a
// command/query has no authorization posture (the default is "require authentication") and no
// authorization behavior is wired. This sample has no authentication story, so each public endpoint
// says so explicitly. Article 010 shows how to require roles/permissions with real authorization.

/// <summary>Create a new catalog product. Validated by <see cref="CreateProductCommandValidator"/>.</summary>
[RelayHttpPost("/products")]
[RelayHttpTag("Catalog")]
[SkipTransaction]
[AllowAnonymous]
public sealed record CreateProductCommand(string Name, string Category, decimal Price) : ICommand<Product>;

/// <summary>Change a product's price. The id comes from the route; the new price from the body.</summary>
[RelayHttpPut("/products/{id}/price")]
[RelayHttpTag("Catalog")]
[SkipTransaction]
[AllowAnonymous]
public sealed record UpdateProductPriceCommand : ICommand<Product>
{
    [RelayFromRoute]
    public string Id { get; init; } = string.Empty;

    public decimal NewPrice { get; init; }
}

/// <summary>Mark a product as discontinued. The id binds from the route token by name.</summary>
[RelayHttpPost("/products/{id}/discontinue")]
[RelayHttpTag("Catalog")]
[SkipTransaction]
[AllowAnonymous]
public sealed record DiscontinueProductCommand(string Id) : ICommand<Product>;

// ── Validators ───────────────────────────────────────────────────────────────────────────────────
//
// Validators implement ICommandValidator<TCommand, TResult> and are discovered + registered by
// AddRelay automatically. The ValidationBehavior (pipeline priority 1) runs every validator for a
// message and aggregates their failures into one ValidationException, which the Web exception
// mapper turns into an HTTP 400 ProblemDetails. Validators return a FluentValidation
// ValidationResult; you can build one by hand (as here) or with a FluentValidation AbstractValidator.

public sealed class CreateProductCommandValidator : ICommandValidator<CreateProductCommand, Product>
{
    public Task<ValidationResult> ValidateAsync(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            failures.Add(new ValidationFailure(nameof(command.Name), "Name is required."));
        }
        else if (command.Name.Length > 120)
        {
            failures.Add(new ValidationFailure(nameof(command.Name), "Name cannot exceed 120 characters."));
        }

        if (string.IsNullOrWhiteSpace(command.Category))
        {
            failures.Add(new ValidationFailure(nameof(command.Category), "Category is required."));
        }

        if (command.Price <= 0m)
        {
            failures.Add(new ValidationFailure(nameof(command.Price), "Price must be greater than zero."));
        }

        return Task.FromResult(new ValidationResult(failures));
    }
}

// ── Handlers ─────────────────────────────────────────────────────────────────────────────────────
//
// Handlers contain the behavior for one command. They are auto-discovered by AddRelay, so they are
// never registered by hand. Dependencies (here the in-memory ProductCatalog) are injected.

public sealed class CreateProductCommandHandler(ProductCatalog catalog)
    : ICommandHandler<CreateProductCommand, Product>
{
    public Task<Product> Handle(CreateProductCommand command, CancellationToken cancellationToken)
    {
        var product = new Product(
            Id: $"p-{Guid.NewGuid():N}",
            Name: command.Name.Trim(),
            Category: command.Category.Trim().ToLowerInvariant(),
            Price: command.Price,
            Discontinued: false);

        return Task.FromResult(catalog.Save(product));
    }
}

public sealed class UpdateProductPriceCommandHandler(ProductCatalog catalog)
    : ICommandHandler<UpdateProductPriceCommand, Product>
{
    public Task<Product> Handle(UpdateProductPriceCommand command, CancellationToken cancellationToken)
    {
        var existing = catalog.Get(command.Id)
            ?? throw new NotFoundException($"Product '{command.Id}' was not found.");

        if (command.NewPrice <= 0m)
        {
            // A handler may also reject invalid input directly; this maps to HTTP 400.
            throw new ValidationException(
                "Validation failed",
                new Dictionary<string, string[]> { [nameof(command.NewPrice)] = ["Price must be greater than zero."] });
        }

        return Task.FromResult(catalog.Save(existing with { Price = command.NewPrice }));
    }
}

public sealed class DiscontinueProductCommandHandler(ProductCatalog catalog)
    : ICommandHandler<DiscontinueProductCommand, Product>
{
    public Task<Product> Handle(DiscontinueProductCommand command, CancellationToken cancellationToken)
    {
        var existing = catalog.Get(command.Id)
            ?? throw new NotFoundException($"Product '{command.Id}' was not found.");

        return Task.FromResult(catalog.Save(existing with { Discontinued = true }));
    }
}

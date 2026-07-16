using System.Net;
using System.Net.Http.Json;
using Documents.Api;
using Documents.Api.Documents;
using FluentAssertions;
using Xunit;

namespace Documents.Api.Tests;

/// <summary>
/// One test per authorization attribute, driven through an in-process server with header-based dev auth
/// (<c>X-User-Id</c> / <c>X-Roles</c> / <c>X-Permissions</c>). The status codes — 401 unauthenticated,
/// 403 authenticated-but-not-allowed, 200 allowed — are produced by the authorization behaviors and the
/// Web exception mapper, never written in the handlers.
/// </summary>
public sealed class AuthorizationTests
{
    private static HttpRequestMessage Request(HttpMethod method, string url, string? userId = null, string? roles = null, string? permissions = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (userId is not null) request.Headers.Add(DevAuthentication.UserHeader, userId);
        if (roles is not null) request.Headers.Add(DevAuthentication.RolesHeader, roles);
        if (permissions is not null) request.Headers.Add(DevAuthentication.PermissionsHeader, permissions);
        if (body is not null) request.Content = JsonContent.Create(body);
        return request;
    }

    [Fact]
    public async Task Listing_documents_is_public()
    {
        using var server = AuthTestHost.Create();
        using var client = server.CreateClient();

        var response = await client.GetAsync("/documents"); // no auth headers
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Getting_a_document_requires_authentication()
    {
        using var server = AuthTestHost.Create();
        using var client = server.CreateClient();

        (await client.SendAsync(Request(HttpMethod.Get, "/documents/doc-1")))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized); // anonymous → 401

        (await client.SendAsync(Request(HttpMethod.Get, "/documents/doc-1", userId: Guid.NewGuid().ToString())))
            .StatusCode.Should().Be(HttpStatusCode.OK); // any signed-in user
    }

    [Fact]
    public async Task Creating_a_document_requires_the_Editor_role()
    {
        using var server = AuthTestHost.Create();
        using var client = server.CreateClient();
        var body = new { title = "Draft", body = "..." };

        (await client.SendAsync(Request(HttpMethod.Post, "/documents", userId: Guid.NewGuid().ToString(), body: body)))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden); // authenticated, wrong role → 403

        (await client.SendAsync(Request(HttpMethod.Post, "/documents", userId: Guid.NewGuid().ToString(), roles: "Editor", body: body)))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Publishing_a_document_requires_the_permission()
    {
        using var server = AuthTestHost.Create();
        using var client = server.CreateClient();

        // Even an Editor without the specific permission is forbidden.
        (await client.SendAsync(Request(HttpMethod.Post, "/documents/doc-1/publish", userId: Guid.NewGuid().ToString(), roles: "Editor")))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await client.SendAsync(Request(HttpMethod.Post, "/documents/doc-1/publish", userId: Guid.NewGuid().ToString(), permissions: "documents:publish")))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

using System.Security.Claims;
using Cratebase.Api.Auth;
using Cratebase.Application.Security;
using Cratebase.Domain.SharedKernel.Ids;
using Microsoft.AspNetCore.Http;

namespace Cratebase.Api.Tests;

public sealed class CurrentRequestContextTests
{
    [Fact(DisplayName = "Current user reads the authenticated user id claim")]
    public void Current_user_reads_the_authenticated_user_id_claim()
    {
        var userId = UserId.New();
        HttpCurrentUser currentUser = new(CreateAccessor(new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())));

        Assert.Equal(userId, currentUser.UserId);
    }

    [Fact(DisplayName = "Current user rejects missing user id claim")]
    public void Current_user_rejects_missing_user_id_claim()
    {
        HttpCurrentUser currentUser = new(CreateAccessor());

        _ = Assert.Throws<InvalidOperationException>(() => currentUser.UserId);
    }

    [Fact(DisplayName = "Current collection reads the default collection claim")]
    public void Current_collection_reads_the_default_collection_claim()
    {
        var collectionId = CollectionId.New();
        HttpCurrentCollection currentCollection = new(CreateAccessor(new Claim(CratebaseClaimTypes.DefaultCollectionId, collectionId.Value.ToString())));

        Assert.Equal(collectionId, currentCollection.CollectionId);
    }

    [Fact(DisplayName = "Current collection rejects missing collection claim")]
    public void Current_collection_rejects_missing_collection_claim()
    {
        HttpCurrentCollection currentCollection = new(CreateAccessor());

        _ = Assert.Throws<InvalidOperationException>(() => currentCollection.CollectionId);
    }

    private static HttpContextAccessor CreateAccessor(params Claim[] claims)
    {
        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            }
        };
    }
}

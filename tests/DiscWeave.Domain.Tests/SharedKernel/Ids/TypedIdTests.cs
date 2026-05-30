using System.Reflection;
using DiscWeave.Domain.SharedKernel.Ids;

namespace DiscWeave.Domain.Tests.SharedKernel.Ids;

public sealed class TypedIdTests
{
    public static TheoryData<string> NewIds()
    {
        var data = new TheoryData<string>();
        IEnumerable<Type> idTypes = typeof(ArtistId).Assembly
            .GetTypes()
            .Where(type => type is { IsPublic: true, IsValueType: true } && type.Name.EndsWith("Id", StringComparison.Ordinal))
            .OrderBy(type => type.Name);

        foreach (Type idType in idTypes)
        {
            MethodInfo? newMethod = idType.GetMethod(nameof(ArtistId.New), BindingFlags.Public | BindingFlags.Static, []);
            PropertyInfo? valueProperty = idType.GetProperty(nameof(ArtistId.Value), BindingFlags.Public | BindingFlags.Instance);

            if (newMethod is null || valueProperty?.PropertyType != typeof(Guid))
            {
                continue;
            }

            object id = newMethod.Invoke(null, null) ?? throw new InvalidOperationException($"Typed ID {idType.Name}.New returned null.");
            data.Add(((Guid)valueProperty.GetValue(id)!).ToString());
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NewIds))]
    public void New_typed_ids_use_version_seven_guids(string value)
    {
        Assert.Equal(7, Guid.Parse(value).Version);
    }

    [Fact]
    public void User_and_collection_ids_reject_empty_guids()
    {
        _ = Assert.Throws<ArgumentException>(() => new CollectionId(Guid.Empty));
        _ = Assert.Throws<ArgumentException>(() => new UserId(Guid.Empty));
    }
}

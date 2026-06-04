using System.IO;
using NUnit.Framework;
using Migration.Engine.Profiles;

namespace Migration.Engine.Tests.Profiles;

[TestFixture]
public class ProfileLoaderTests
{
    [Test]
    public void Load_RoundTripsTypeMapAndValueTable()
    {
        var json = @"{
          ""level"": ""StoriesEtc"",
          ""typeMap"": { ""Story"": ""User Story"" },
          ""valueTables"": { ""System.State"": { ""Done"": ""Closed"" } },
          ""linkMap"": { ""relates to"": ""System.LinkTypes.Related"" },
          ""fields"": [ { ""sourceName"": ""summary"", ""target"": ""System.Title"", ""mapper"": ""MapTitle"" } ],
          ""users"": { ""fallback"": ""svc@org"", ""emailToAdo"": {} }
        }";
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);

        var profile = ProfileLoader.Load(path);

        Assert.That(profile.TryMapType("Story", out var t), Is.True);
        Assert.That(t, Is.EqualTo("User Story"));
        Assert.That(profile.MapValue("System.State", "Done"), Is.EqualTo("Closed"));
        Assert.That(profile.LinkMap["relates to"], Is.EqualTo("System.LinkTypes.Related"));
        Assert.That(profile.Users.Fallback, Is.EqualTo("svc@org"));
        File.Delete(path);
    }

    [Test]
    public void Load_Throws_WhenTypeMapEmpty()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, @"{ ""level"": ""Epics"", ""typeMap"": {} }");
        var ex = Assert.Throws<ProfileValidationException>(() => ProfileLoader.Load(path));
        Assert.That(ex!.Message, Does.Contain("typeMap"));
        File.Delete(path);
    }
}

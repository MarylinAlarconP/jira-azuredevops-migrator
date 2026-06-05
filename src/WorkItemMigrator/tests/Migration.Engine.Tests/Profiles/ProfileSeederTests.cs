using System.IO;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.WIContract;

namespace Migration.Engine.Tests.Profiles;

[TestFixture]
public class ProfileSeederTests
{
    // Includes the Required.Always keys of the real ConfigJson model and the
    // singular wire names (type/field/link) the legacy ConfigReaderJson expects.
    private const string ConfigJson = @"{
      ""source-project"": ""JIRA"",
      ""target-project"": ""ADO"",
      ""query"": ""project = JIRA"",
      ""workspace"": ""C:/tmp"",
      ""attachment-folder"": ""attachments"",
      ""type-map"": { ""type"": [ { ""source"": ""Story"", ""target"": ""User Story"" } ] },
      ""field-map"": { ""field"": [
        { ""source"": ""summary"", ""target"": ""System.Title"", ""mapper"": ""MapTitle"" },
        { ""source"": ""status"", ""target"": ""System.State"",
          ""mapping"": { ""values"": [ { ""source"": ""Done"", ""target"": ""Closed"" } ] } }
      ] },
      ""link-map"": { ""link"": [ { ""source"": ""relates to"", ""target"": ""System.LinkTypes.Related"" } ] },
      ""user-mapping-file"": ""users.txt""
    }";

    [Test]
    public void Seed_BuildsTypeMapFieldsValueTablesAndLinkMap()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "config.json");
        File.WriteAllText(path, ConfigJson);
        File.WriteAllText(Path.Combine(dir, "users.txt"), "jdoe@jira = John Doe <jdoe@ado>\n");

        var profile = ProfileSeeder.Seed(path, MigrationLevel.StoriesEtc);

        Assert.That(profile.Level, Is.EqualTo("StoriesEtc"));
        Assert.That(profile.TryMapType("Story", out var t), Is.True);
        Assert.That(t, Is.EqualTo("User Story"));
        Assert.That(profile.MapValue(WiFieldReference.State, "Done"), Is.EqualTo("Closed"));
        Assert.That(profile.LinkMap["relates to"], Is.EqualTo("System.LinkTypes.Related"));
        Assert.That(profile.Fields.Exists(f => f.Mapper == "MapTitle"), Is.True);
        Assert.That(profile.Users.EmailToAdo["jdoe@jira"], Is.EqualTo("John Doe <jdoe@ado>"));
        Directory.Delete(dir, recursive: true);
    }
}

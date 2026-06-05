using System.Linq;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Transforming;
using Migration.WIContract;

namespace Migration.Engine.Tests.Transforming;

[TestFixture]
public class TransformerTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        p.Fields.Add(new FieldRule { SourceName = "summary", Target = WiFieldReference.Title, Mapper = "MapTitle" });
        return p;
    }

    private static JobRequest Request() => new()
    {
        BaseAreaPath = "Proj\\Area", BaseIterationPath = "Proj\\Iter"
    };

    [Test]
    public void Transform_UnmappedType_IsSkippedWithReason()
    {
        var issue = new RawIssue { Key = "PROJ-9", IssueType = "Epic" };
        var result = new Transformer().Transform(issue, Profile(), Request());
        Assert.That(result.Skipped, Is.True);
        Assert.That(result.SkipReason, Is.EqualTo("unmapped-type"));
    }

    [Test]
    public void Transform_MapsTypeLegacyIdAndTitle()
    {
        var issue = new RawIssue { Key = "PROJ-1", IssueType = "Story", Fields = { ["summary"] = "Login bug" } };
        var result = new Transformer().Transform(issue, Profile(), Request());

        Assert.That(result.Skipped, Is.False);
        var item = result.Item!;
        Assert.That(item.Type, Is.EqualTo("User Story"));
        Assert.That(item.OriginId, Is.EqualTo("PROJ-1"));
        var rev = item.Revisions.Single();
        Assert.That(rev.Fields.Single(f => f.ReferenceName == "Custom.LegacyID").Value, Is.EqualTo("PROJ-1"));
        Assert.That(rev.Fields.Single(f => f.ReferenceName == WiFieldReference.Title).Value, Is.EqualTo("Login bug"));
        Assert.That(rev.Fields.Single(f => f.ReferenceName == WiFieldReference.AreaPath).Value, Is.EqualTo("Proj\\Area"));
        Assert.That(rev.Fields.Single(f => f.ReferenceName == WiFieldReference.IterationPath).Value, Is.EqualTo("Proj\\Iter"));
    }
}

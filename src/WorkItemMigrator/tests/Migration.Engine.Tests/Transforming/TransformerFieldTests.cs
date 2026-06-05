using System.Linq;
using NUnit.Framework;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Transforming;
using Migration.WIContract;

namespace Migration.Engine.Tests.Transforming;

[TestFixture]
public class TransformerFieldTests
{
    [Test]
    public void Transform_MapsStatusThroughValueTable_ToState()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        p.Fields.Add(new FieldRule { SourceName = "status", Target = WiFieldReference.State });
        p.ValueTables[WiFieldReference.State] = new() { ["Done"] = "Closed" };

        var issue = new RawIssue
        {
            Key = "PROJ-1", IssueType = "Story",
            ReporterEmail = "rep@org",
            Fields = { ["status"] = "Done" }
        };
        var result = new Transformer().Transform(issue, p, new JobRequest());

        var rev = result.Item!.Revisions.Single();
        Assert.That(rev.Fields.Single(f => f.ReferenceName == WiFieldReference.State).Value, Is.EqualTo("Closed"));
    }

    [Test]
    public void Transform_SetsCreatedByAndDateFromSource()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        p.Users.EmailToAdo["rep@org"] = "Reporter <rep@ado>";

        var created = new System.DateTime(2023, 1, 2, 3, 4, 5);
        var issue = new RawIssue { Key = "PROJ-1", IssueType = "Story", ReporterEmail = "rep@org", Created = created };
        var result = new Transformer().Transform(issue, p, new JobRequest());

        var rev = result.Item!.Revisions.Single();
        Assert.That(rev.Author, Is.EqualTo("Reporter <rep@ado>"));
        Assert.That(rev.Fields.Single(f => f.ReferenceName == WiFieldReference.CreatedDate).Value, Is.EqualTo(created));
    }
}

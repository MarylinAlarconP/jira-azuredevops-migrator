using System.Linq;
using NUnit.Framework;
using Migration.Engine.Profiles;
using Migration.Engine.Reading;
using Migration.Engine.Contracts;
using Migration.Engine.Transforming;
using Migration.WIContract;

namespace Migration.Engine.Tests.Transforming;

[TestFixture]
public class TransformerCommentTests
{
    private static MappingProfile Profile()
    {
        var p = new MappingProfile();
        p.TypeMap["Story"] = "User Story";
        return p;
    }

    [Test]
    public void Transform_AddsHistoryFieldPerComment()
    {
        var issue = new RawIssue
        {
            Key = "PROJ-1", IssueType = "Story",
            Comments = { new RawComment { Author = "a@org", Body = "first" },
                         new RawComment { Author = "b@org", Body = "second" } }
        };
        var item = new Transformer().Transform(issue, Profile(), new JobRequest()).Item!;
        var historyValues = item.Revisions.Single().Fields
            .Where(f => f.ReferenceName == WiFieldReference.History)
            .Select(f => f.Value!.ToString()).ToList();
        Assert.That(historyValues.Count, Is.EqualTo(2));
        Assert.That(historyValues[0], Does.Contain("first"));
    }

    [Test]
    public void Transform_CarriesAttachmentReferences()
    {
        var issue = new RawIssue
        {
            Key = "PROJ-1", IssueType = "Story",
            Attachments = { new RawAttachment { Id = "55", FileName = "a.png" } }
        };
        var att = new Transformer().Transform(issue, Profile(), new JobRequest())
            .Item!.Revisions.Single().Attachments.Single();
        Assert.That(att.AttOriginId, Is.EqualTo("55"));
        Assert.That(att.FileName, Is.EqualTo("a.png"));
    }
}

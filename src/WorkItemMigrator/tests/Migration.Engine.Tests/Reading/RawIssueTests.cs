using NUnit.Framework;
using Migration.Engine.Reading;

namespace Migration.Engine.Tests.Reading;

[TestFixture]
public class RawIssueTests
{
    [Test]
    public void RawIssue_HoldsFieldsCommentsAttachmentsLinks()
    {
        var issue = new RawIssue
        {
            Key = "PROJ-1",
            IssueType = "Story",
            Fields = { ["summary"] = "Hello" },
            Comments = { new RawComment { Author = "a@org", Body = "hi" } },
            Attachments = { new RawAttachment { Id = "10", FileName = "f.png" } },
            Links = { new RawLink { TargetKey = "PROJ-2", JiraLinkType = "relates to" } }
        };
        Assert.That(issue.Fields["summary"], Is.EqualTo("Hello"));
        Assert.That(issue.Comments[0].Author, Is.EqualTo("a@org"));
        Assert.That(issue.Attachments[0].FileName, Is.EqualTo("f.png"));
        Assert.That(issue.Links[0].JiraLinkType, Is.EqualTo("relates to"));
    }
}

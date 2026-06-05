using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Migration.Engine.Storage;
using Migration.Engine.Transforming;
using Migration.Engine.Writing;
using Migration.Engine.Tests.Fakes;
using Migration.WIContract;

namespace Migration.Engine.Tests.Writing;

[TestFixture]
public class AdoWriterTests
{
    private static WiItem Item(string key)
    {
        var item = new WiItem { Type = "User Story", OriginId = key };
        var rev = new WiRevision { Index = 0 };
        rev.Fields.Add(new WiField { ReferenceName = Transformer.LegacyIdField, Value = key });
        rev.Fields.Add(new WiField { ReferenceName = WiFieldReference.Title, Value = "t" });
        item.Revisions = new List<WiRevision> { rev };
        return item;
    }

    private static (IStageStore store, string root) NewStore()
    {
        var root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        return (new FileSystemStageStore(root, "job1"), root);
    }

    [Test]
    public void Write_SkipsWhenLegacyIdPresent()
    {
        var (store, root) = NewStore();
        var ado = new FakeAdoClient();
        ado.Existing["PROJ-1"] = new() { 7 };
        var outcome = new WriteOutcome();

        new AdoWriter(ado).Write(Item("PROJ-1"), store, store.LoadState(), outcome, CancellationToken.None);

        Assert.That(outcome.Skipped, Is.EqualTo(1));
        Assert.That(outcome.Created, Is.EqualTo(0));
        Assert.That(ado.Created, Is.Empty);
        Directory.Delete(root, true);
    }

    [Test]
    public void Write_CreatesWhenAbsent_AndRecordsState()
    {
        var (store, root) = NewStore();
        var ado = new FakeAdoClient();
        var state = store.LoadState();
        var outcome = new WriteOutcome();

        new AdoWriter(ado).Write(Item("PROJ-2"), store, state, outcome, CancellationToken.None);

        Assert.That(outcome.Created, Is.EqualTo(1));
        Assert.That(ado.Created.Single().LegacyId, Is.EqualTo("PROJ-2"));
        Assert.That(state.IsWritten("PROJ-2"), Is.True);
        Directory.Delete(root, true);
    }

    [Test]
    public void Write_RecordsFailure_WhenAdoRejects()
    {
        var (store, root) = NewStore();
        var ado = new FakeAdoClient();
        ado.RejectLegacyIds.Add("PROJ-3");
        var outcome = new WriteOutcome();

        new AdoWriter(ado).Write(Item("PROJ-3"), store, store.LoadState(), outcome, CancellationToken.None);

        Assert.That(outcome.Failed, Is.EqualTo(1));
        Assert.That(outcome.Failures.Single().ItemKey, Is.EqualTo("PROJ-3"));
        Directory.Delete(root, true);
    }

    [Test]
    public void Write_DoesNotDuplicateAuthorAndDateIntoFields()
    {
        // Transformer puts CreatedBy/CreatedDate into rev.Fields; the writer carries them
        // via the dedicated request properties, so they must NOT also appear in request.Fields
        // (a duplicate /fields/System.CreatedBy patch path makes ADO reject the create).
        var (store, root) = NewStore();
        var ado = new FakeAdoClient();
        var created = new System.DateTime(2023, 1, 2, 3, 4, 5);
        var item = Item("PROJ-4");
        item.Revisions[0].Fields.Add(new WiField { ReferenceName = WiFieldReference.CreatedBy, Value = "Rep <rep@ado>" });
        item.Revisions[0].Fields.Add(new WiField { ReferenceName = WiFieldReference.CreatedDate, Value = created });

        new AdoWriter(ado).Write(item, store, store.LoadState(), new WriteOutcome(), CancellationToken.None);

        var req = ado.Created.Single();
        Assert.That(req.CreatedBy, Is.EqualTo("Rep <rep@ado>"));
        Assert.That(req.CreatedDate, Is.EqualTo(created));
        Assert.That(req.Fields.ContainsKey(WiFieldReference.CreatedBy), Is.False);
        Assert.That(req.Fields.ContainsKey(WiFieldReference.CreatedDate), Is.False);
        Directory.Delete(root, true);
    }

    [Test]
    public void Write_SkipsWhenStateAlreadyWritten()
    {
        var (store, root) = NewStore();
        var ado = new FakeAdoClient();
        var state = store.LoadState();
        state.RecordWritten("PROJ-5", 42);
        var outcome = new WriteOutcome();

        new AdoWriter(ado).Write(Item("PROJ-5"), store, state, outcome, CancellationToken.None);

        Assert.That(outcome.Skipped, Is.EqualTo(1));
        Assert.That(ado.Created, Is.Empty);
        Directory.Delete(root, true);
    }
}

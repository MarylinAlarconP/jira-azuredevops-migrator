using System.Collections.Generic;

namespace Migration.Engine.Writing;

public sealed class AdoCreateRequest
{
    public string Type { get; set; } = "";
    public string LegacyId { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public System.DateTime CreatedDate { get; set; }
    public Dictionary<string, object> Fields { get; set; } = new();   // reference name -> value
    public List<string> CommentHtml { get; set; } = new();
    public List<string> AttachmentPaths { get; set; } = new();
}

public interface IAdoClient
{
    // returns all ADO ids in the project carrying this LegacyID (whole-project scope)
    IReadOnlyList<int> FindByLegacyId(string legacyId);

    // creates a work item in its final state via bypass rules; throws AdoRejectedException on rejection
    int CreateWorkItem(AdoCreateRequest request);

    int CountByLegacyIds(IEnumerable<string> legacyIds);

    // creates an ADO relation from source to target; returns true if created, false if it already existed
    bool AddRelation(int sourceId, int targetId, string adoRelationReferenceName);

    // target metadata checks used by the readiness pre-flight
    bool FieldExists(string referenceName);
    bool TypeExists(string workItemType);
    bool PathExists(string classification, string path); // classification: "area" or "iteration"
}

public sealed class AdoRejectedException : System.Exception
{
    public AdoRejectedException(string message) : base(message) { }
}

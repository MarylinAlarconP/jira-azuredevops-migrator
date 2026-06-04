using System.Collections.Generic;
using Migration.Engine.Contracts;
using Migration.Engine.Profiles;
using Migration.Engine.Writing;

namespace Migration.Engine.Preflight;

public sealed class PreflightResult
{
    public bool Ok => Problems.Count == 0;
    public List<string> Problems { get; } = new();
}

public sealed class ReadinessPreflight
{
    private readonly IAdoClient _ado;
    public ReadinessPreflight(IAdoClient ado) => _ado = ado;

    public PreflightResult Check(JobRequest request, MappingProfile profile)
    {
        var result = new PreflightResult();

        if (!_ado.FieldExists("Custom.LegacyID"))
            result.Problems.Add("Required field 'Custom.LegacyID' is missing in the target project.");

        foreach (var adoType in new HashSet<string>(profile.TypeMap.Values))
            if (!_ado.TypeExists(adoType))
                result.Problems.Add($"Work item type '{adoType}' is not available in the target process.");

        if (!_ado.PathExists("area", request.BaseAreaPath))
            result.Problems.Add($"Base area path '{request.BaseAreaPath}' does not exist.");
        if (!_ado.PathExists("iteration", request.BaseIterationPath))
            result.Problems.Add($"Base iteration path '{request.BaseIterationPath}' does not exist.");

        return result;
    }
}

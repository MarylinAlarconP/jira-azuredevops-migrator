using System;
using System.Collections.Generic;

namespace Migration.Engine.Profiles;

public sealed class FieldRule
{
    public string SourceName { get; set; } = "";   // Jira field id/name
    public string Target { get; set; } = "";        // ADO reference name
    public string Mapper { get; set; } = "";        // MapTitle, MapRendered, MapTags, MapDateTime, MapUser, MapToComments, ""
    public string For { get; set; } = "All";        // work-item-type filter
    public string NotFor { get; set; } = "";
}

public sealed class UserMappingRule
{
    public Dictionary<string, string> EmailToAdo { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public string Fallback { get; set; } = "";       // ADO identity used when no email match
}

public sealed class MappingProfile
{
    public string Level { get; set; } = "";

    public Dictionary<string, string> TypeMap { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public List<FieldRule> Fields { get; set; } = new();

    // target ADO field -> (source value -> target value)
    public Dictionary<string, Dictionary<string, string>> ValueTables { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    // Jira link type name -> ADO relation reference name
    public Dictionary<string, string> LinkMap { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public UserMappingRule Users { get; set; } = new();

    public bool TryMapType(string jiraType, out string adoType)
    {
        if (jiraType != null && TypeMap.TryGetValue(jiraType, out var t))
        {
            adoType = t;
            return true;
        }
        adoType = "";
        return false;
    }

    public string MapValue(string targetField, string sourceValue)
    {
        if (ValueTables.TryGetValue(targetField, out var table)
            && table.TryGetValue(sourceValue, out var mapped))
        {
            return mapped;
        }
        return sourceValue;
    }
}

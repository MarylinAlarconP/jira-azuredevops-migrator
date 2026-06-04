using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Migration.Common;
using Migration.Common.Config;
using Migration.Engine.Contracts;

namespace Migration.Engine.Profiles;

public static class ProfileSeeder
{
    public static MappingProfile Seed(string configPath, MigrationLevel level)
    {
        // ConfigReaderJson populates Types/Fields/Links from the singular wire
        // names (type/field/link); plain JsonConvert leaves those collections null.
        var config = new ConfigReaderJson(configPath).Deserialize()
            ?? throw new InvalidOperationException($"Could not read config: {configPath}");

        var profile = new MappingProfile { Level = level.ToString() };

        if (config.TypeMap?.Types != null)
            foreach (var type in config.TypeMap.Types)
                profile.TypeMap[type.Source] = type.Target;

        if (config.FieldMap?.Fields != null)
            foreach (var field in config.FieldMap.Fields)
            {
                profile.Fields.Add(new FieldRule
                {
                    SourceName = field.Source,
                    Target = field.Target,
                    Mapper = field.Mapper ?? "",
                    For = field.For ?? "All",
                    NotFor = field.NotFor ?? ""
                });

                if (field.Mapping?.Values != null && !string.IsNullOrEmpty(field.Target))
                {
                    if (!profile.ValueTables.TryGetValue(field.Target, out var table))
                        profile.ValueTables[field.Target] = table =
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var v in field.Mapping.Values)
                        table[v.Source] = v.Target;
                }
            }

        if (config.LinkMap?.Links != null)
            foreach (var link in config.LinkMap.Links)
                profile.LinkMap[link.Source] = link.Target;

        SeedUsers(profile, config.UserMappingFile, configPath);

        return profile;
    }

    // Reuse the legacy parser (jiraUser = wiUser lines). Re-base relative to the
    // config's own directory when the literal path does not exist, since generated
    // configs may carry absolute paths from another machine. Missing file -> empty
    // map (UserMapper logs a warning); seeded Users can then be filled in manually.
    private static void SeedUsers(MappingProfile profile, string userMappingFile, string configPath)
    {
        if (string.IsNullOrWhiteSpace(userMappingFile)) return;

        var path = userMappingFile;
        if (!File.Exists(path))
        {
            var configDir = Path.GetDirectoryName(Path.GetFullPath(configPath)) ?? ".";
            path = Path.Combine(configDir, Path.GetFileName(userMappingFile));
        }

        foreach (var pair in UserMapper.ParseUserMappings(path))
            profile.Users.EmailToAdo[pair.Key] = pair.Value;
    }

    public static void SeedToFile(string configPath, MigrationLevel level, string outPath)
    {
        var profile = Seed(configPath, level);
        File.WriteAllText(outPath, JsonConvert.SerializeObject(profile, Formatting.Indented));
    }
}

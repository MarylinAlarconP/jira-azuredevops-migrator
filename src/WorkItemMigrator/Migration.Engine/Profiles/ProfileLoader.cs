using System;
using System.IO;
using Newtonsoft.Json;

namespace Migration.Engine.Profiles;

public sealed class ProfileValidationException : Exception
{
    public ProfileValidationException(string message) : base(message) { }
}

public static class ProfileLoader
{
    public static MappingProfile Load(string path)
    {
        if (!File.Exists(path))
            throw new ProfileValidationException($"Profile file not found: {path}");

        var json = File.ReadAllText(path);
        MappingProfile? profile;
        try
        {
            profile = JsonConvert.DeserializeObject<MappingProfile>(json);
        }
        catch (JsonException ex)
        {
            throw new ProfileValidationException($"Profile JSON is invalid: {ex.Message}");
        }

        if (profile == null)
            throw new ProfileValidationException("Profile JSON deserialized to null.");
        if (profile.TypeMap.Count == 0)
            throw new ProfileValidationException("Profile typeMap must contain at least one mapping.");

        return profile;
    }
}

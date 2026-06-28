using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.UserInterface.Systems.Language;

public sealed class LanguageFavorites
{
    private static readonly ResPath Path = new("/language_favorites.txt");
    private readonly HashSet<string> _favorites = new();
    private readonly IResourceManager _resource;

    public LanguageFavorites(IResourceManager resource)
    {
        _resource = resource;
        Load();
    }

    public bool Contains(string languageId) => _favorites.Contains(languageId);

    public void Toggle(string languageId)
    {
        if (!_favorites.Remove(languageId))
            _favorites.Add(languageId);
        Save();
    }

    private void Load()
    {
        if (!_resource.UserData.TryReadAllText(Path, out var text))
            return;
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0)
                _favorites.Add(trimmed);
        }
    }

    private void Save()
    {
        _resource.UserData.WriteAllText(Path, string.Join("\n", _favorites));
    }
}
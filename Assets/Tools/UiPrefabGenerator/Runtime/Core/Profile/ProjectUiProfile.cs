using System.Collections.Generic;

namespace UiPrefabGenerator.Core.Profile
{
    public interface IProjectUiProfile
    {
        string ProfileId { get; }
        string DraftPrefabRoot { get; }
        string RuntimeBindingNamespace { get; }
        IReadOnlyCollection<string> AllowedComponentTypes { get; }
    }
}

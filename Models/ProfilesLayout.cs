using System.Collections.Generic;

namespace HoldSpace.Models
{
    /// <summary>
    /// Top-level layout file model supporting multiple profiles/modes.
    /// Replaces the flat CanvasLayout when profiles are present.
    /// </summary>
    public class ProfilesLayout
    {
        public string ActiveProfileId { get; set; } = "default";
        public List<ShortcutProfile> Profiles { get; set; } = new List<ShortcutProfile>();
    }
}

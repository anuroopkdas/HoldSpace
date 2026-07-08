namespace HoldSpace.Models
{
    public class ShortcutAction
    {
        public string Type { get; set; } = "website"; // app, folder, file, website, systemAction
        public string Target { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }
}

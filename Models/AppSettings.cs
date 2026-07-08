namespace HoldSpace.Models
{
    public class AppSettings
    {
        public bool StartWithWindows { get; set; } = false;
        public string Theme { get; set; } = "Dark";
        public int HoldDelayMs { get; set; } = 120;
        public TriggerKey Trigger { get; set; } = new TriggerKey();
        public bool IsOnboarded { get; set; } = false;

        // Onboarding configurations
        public bool HasCompletedOnboarding { get; set; } = false;
        public int OnboardingVersion { get; set; } = 1;

        // Appearance Settings
        public double OverlayOpacity { get; set; } = 0.8;
        public bool BackgroundDim { get; set; } = true;
        public int AnimationDurationMs { get; set; } = 150;
        public int HoverDelayMs { get; set; } = 80;

        // General Behavior
        public bool MinimizeToTray { get; set; } = true;
        public bool StartMinimizedToTray { get; set; } = false;
    }
}

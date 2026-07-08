namespace HoldSpace.Models
{
    public class TriggerKey
    {
        public string KeyName { get; set; } = "CapsLock";
        public int VirtualKeyCode { get; set; } = 0x14; // VK_CAPITAL
        public bool IsModifier { get; set; } = false;
    }
}

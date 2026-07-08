using System;
using System.Collections.Generic;

namespace HoldSpace.Models
{
    public class ShortcutProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Default";
        public string LayoutType { get; set; } = "freeCanvas";
        public List<CanvasItem> Items { get; set; } = new List<CanvasItem>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

using System.Collections.Generic;

namespace HoldSpace.Models
{
    public class CanvasLayout
    {
        public string Name { get; set; } = "Default";
        public List<CanvasItem> Items { get; set; } = new List<CanvasItem>();
    }
}

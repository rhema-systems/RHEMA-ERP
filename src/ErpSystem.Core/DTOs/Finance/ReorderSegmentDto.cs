using System;

namespace ErpSystem.Core.DTOs.Finance
{
    public class ReorderSegmentDto
    {
        public Guid SegmentId { get; set; }
        public int NewPosition { get; set; }
    }
}

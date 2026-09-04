using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    public class ReorderSegmentDto
    {
        public Guid SegmentId { get; set; }
        public int NewPosition { get; set; }

        [Required]
        public string RowVersion { get; set; } = string.Empty;
    }
}

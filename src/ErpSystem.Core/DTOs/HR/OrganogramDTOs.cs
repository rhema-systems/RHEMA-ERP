using System;
using System.Collections.Generic;

namespace ErpSystem.Core.DTOs.HR
{
    /// <summary>
    /// A single, dimension-agnostic node in an organogram. Returned as a flat list
    /// (each node carries its <see cref="ParentId"/>); the client (d3-org-chart) builds the tree.
    /// The same shape is used for every dimension (units, positions, people, locations, teams)
    /// so the renderer stays uniform.
    /// </summary>
    public class OrganogramNodeDto
    {
        /// <summary>Stable node id (string — d3-org-chart requires string ids).</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>Parent node id. Null/empty marks a root node.</summary>
        public string? ParentId { get; set; }

        /// <summary>Primary label (unit name / position title / employee full name / location / team).</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Secondary label (level name / job title / role).</summary>
        public string? Title { get; set; }

        /// <summary>Short code, where the entity has one.</summary>
        public string? Code { get; set; }

        /// <summary>Name of the unit head / team lead (units &amp; teams dimensions).</summary>
        public string? HeadName { get; set; }

        /// <summary>Avatar / photo URL (people dimension).</summary>
        public string? ImageUrl { get; set; }

        /// <summary>Optional status chip text, e.g. "Vacant", "Inactive".</summary>
        public string? Badge { get; set; }

        /// <summary>True when the node represents an unfilled post / leaderless unit.</summary>
        public bool IsVacant { get; set; }

        /// <summary>Whether the underlying record is active.</summary>
        public bool IsActive { get; set; } = true;

        /// <summary>Current headcount under / within this node, when meaningful.</summary>
        public int? EmployeeCount { get; set; }

        /// <summary>Planned headcount (positions dimension capacity planning).</summary>
        public int? ExpectedHeadcount { get; set; }

        /// <summary>"solid" (default) or "dotted" for advisory / functional reporting lines.</summary>
        public string LineType { get; set; } = "solid";

        /// <summary>Extra key/value detail surfaced in the side panel on selection.</summary>
        public Dictionary<string, string> Meta { get; set; } = new();
    }

    /// <summary>
    /// Envelope returned by every organogram endpoint: the dimension, the flat node list,
    /// and lightweight metadata for the client.
    /// </summary>
    public class OrganogramResponseDto
    {
        /// <summary>units | positions | people | locations | teams</summary>
        public string Dimension { get; set; } = string.Empty;

        public List<OrganogramNodeDto> Nodes { get; set; } = new();

        public int NodeCount { get; set; }

        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    }
}

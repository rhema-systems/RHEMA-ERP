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

        /// <summary>
        /// Headcount attached directly to this node, when meaningful: employees in this unit,
        /// holders of this post, direct reports of this person. Null on the dimensions where a
        /// headcount means nothing (locations, teams).
        /// </summary>
        /// <remarks>
        /// Counts people <b>on strength</b> only — <c>IsActive &amp;&amp; StaffStatus != Terminated</c>,
        /// the same predicate <c>EmployeeService</c> uses. Before slice 4 this counted leavers too,
        /// so a department that had lost someone still reported them as staff.
        /// </remarks>
        public int? EmployeeCount { get; set; }

        /// <summary>
        /// Headcount at and below this node — the subtree total. Null wherever
        /// <see cref="EmployeeCount"/> is null for the whole dimension.
        /// </summary>
        /// <remarks>
        /// The number an org chart is actually read for: a Directorate whose employees all sit in
        /// its departments has a direct headcount of zero and a real one of several hundred.
        /// Measured on DEFAULT 2026-08-22, nine of TDC's 41 units were in exactly that position.
        /// </remarks>
        public int? TotalEmployeeCount { get; set; }

        /// <summary>Planned headcount (positions dimension capacity planning).</summary>
        public int? ExpectedHeadcount { get; set; }

        /// <summary>
        /// Units dimension: established posts attached to this unit. Null on every other dimension.
        /// </summary>
        /// <remarks>
        /// Added for the organogram redesign (2026-09-08) so the detail drawer can say what a unit
        /// is made of, not only how many people sit in it. Counts posts, not establishment: a post
        /// with an expected headcount of three is one post here.
        /// </remarks>
        public int? PositionCount { get; set; }

        /// <summary>
        /// Units dimension: posts in this unit that nobody on strength holds. Null elsewhere.
        /// The same predicate as the positions dimension's "Vacant" badge, so the two views agree.
        /// </summary>
        public int? VacantPositionCount { get; set; }

        /// <summary>
        /// Positions dimension: the people on strength who hold this post, by full name, capped at
        /// <see cref="HoldersCap"/>. Null on every other dimension; empty when the post is vacant.
        /// </summary>
        /// <remarks>
        /// The positions view is open to every authenticated user, as the units view is; a name
        /// against a post is what a printed org chart on a noticeboard already shows, and it
        /// carries no contact detail. The cap keeps a post with a large establishment (a pool of
        /// drivers, a typing pool) from turning the payload into a register.
        /// </remarks>
        public List<string>? Holders { get; set; }

        /// <summary>Positions dimension: true when more holders exist than <see cref="Holders"/> lists.</summary>
        public bool HoldersTruncated { get; set; }

        public const int HoldersCap = 25;

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

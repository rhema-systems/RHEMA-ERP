using ErpSystem.Core.Entities.HR.Recruitment;

namespace ErpSystem.Core.Interfaces.HR;

/// <summary>
/// The test paper, with the two reads that do not belong in a service.
/// </summary>
/// <remarks>
/// <para>⚠ <b>Only ONE repository for seven entities</b>, and that is deliberate. Everything else in
/// the engine — sections, questions, options, assignments, sittings, answers — is reached through
/// <c>IUnitOfWork.Repository&lt;T&gt;()</c>, exactly as <c>RecruitmentLifecycleSweepService</c>
/// reaches offers and postings. Seven interfaces and seven implementations that each added nothing
/// to the generic one would be seven more places to forget a tenant predicate.</para>
///
/// <para>This one exists because <b>issuing a reference number needs the Data layer</b>:
/// <c>NumberSequenceExtensions.NextUnusedAsync</c> lives there, and the alternative was to
/// re-implement its collision repair in Core — the helper that exists precisely so nobody does
/// that.</para>
/// </remarks>
public interface IRecruitmentTestRepository : IGenericRepository<RecruitmentTest>
{
    /// <summary>
    /// The next test code, probed against the table before it is used.
    /// </summary>
    /// <remarks>
    /// ⚠ Soft-deleted papers still hold their code against <c>IX_RecruitmentTest_Tenant_Code</c>, so
    /// the probe reads through the filter. This is the lesson three company-schedule generators had
    /// to be retrofitted with in lane D-2 (defect C-6): a <c>COUNT(*) + 1</c> over live rows hands
    /// out a number a deleted row already owns, and the create dies on the unique index as a 500.
    /// </remarks>
    Task<string> GetNextTestCodeAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// A paper with its sections, its questions and every question's options.
    /// </summary>
    /// <remarks>
    /// ⚠ One include chain, in one place. Every read of a paper goes through here — the authoring
    /// screen, the candidate's sitting, the marker and the printed paper — because a read that
    /// forgets <c>Questions.Options</c> renders a multiple-choice test with no choices, and it does
    /// so with a 200. That shape (declared, populated by nothing) is the one this round keeps
    /// finding; it is not going to be reintroduced by a fourth hand-written include list.
    /// </remarks>
    Task<RecruitmentTest?> GetWithFullPaperAsync(Guid id, CancellationToken cancellationToken = default);
}

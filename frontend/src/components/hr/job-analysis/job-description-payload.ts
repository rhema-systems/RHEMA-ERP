import type { CreateJobDescription, JobDescription } from '@/types/hr/job-architecture';

/**
 * The wire shape for `POST descriptions` and `PUT descriptions/{id}`.
 *
 * ⚠ **Undefined optionals are DROPPED by `JSON.stringify`, which is what the API wants:** an empty
 * string on a `Guid?` or a `DateTime` is a 400, not a null. Never send '' for an unset picker.
 */
export function toWritePayload(form: CreateJobDescription): CreateJobDescription {
  return {
    ...form,
    jobFamilyId: form.jobFamilyId || undefined,
    jobSubFamilyId: form.jobSubFamilyId || undefined,
    jobLevelId: form.jobLevelId || undefined,
    staffLevelId: form.staffLevelId || undefined,
    suggestedSalaryGradeId: form.suggestedSalaryGradeId || undefined,
    unionId: form.unionId || undefined,
    occupationCode: form.occupationCode || undefined,
    essentialFunctionsSummary: form.essentialFunctionsSummary || undefined,
    revisionReason: form.revisionReason || undefined,
    roleCriticality: form.roleCriticality || undefined,
    autonomyLevel: form.autonomyLevel || undefined,
    decisionMakingScope: form.decisionMakingScope || undefined,
    valuationNotes: form.valuationNotes || undefined,
    approvalAuthorityNotes: form.approvalAuthorityNotes || undefined,
    intendedEmploymentType: form.intendedEmploymentType || undefined,
  };
}

/**
 * The saved record, as form values.
 *
 * ⚠ **Every field the form does not show still has to travel.** `UpdateEntity` assigns all
 * twenty-nine columns from the DTO unconditionally — it is a replace, not a merge — so a field left
 * out of the payload is not "unchanged", it is set to null. That is how an edit screen silently
 * wipes an expiry date or a benchmark salary it never displayed.
 */
export function toFormValues(jd: JobDescription): CreateJobDescription {
  return {
    positionId: jd.positionId,
    jobTitle: jd.jobTitle,
    jobSummary: jd.jobSummary,
    effectiveDate: jd.effectiveDate,
    expiryDate: jd.expiryDate ?? null,
    revisionReason: jd.revisionReason ?? null,
    reviewCycleMonths: jd.reviewCycleMonths,
    jobFamilyId: jd.jobFamilyId ?? null,
    jobSubFamilyId: jd.jobSubFamilyId ?? null,
    jobLevelId: jd.jobLevelId ?? null,
    staffLevelId: jd.staffLevelId ?? null,
    suggestedSalaryGradeId: jd.suggestedSalaryGradeId ?? null,
    unionId: jd.unionId ?? null,
    isBargainingUnitRole: jd.isBargainingUnitRole,
    occupationCode: jd.occupationCode ?? null,
    essentialFunctionsSummary: jd.essentialFunctionsSummary ?? null,
    roleCriticality: jd.roleCriticality ?? null,
    roleIntrinsicValue: jd.roleIntrinsicValue ?? null,
    industryBenchmarkSalary: jd.industryBenchmarkSalary ?? null,
    valuationNotes: jd.valuationNotes ?? null,
    autonomyLevel: jd.autonomyLevel ?? null,
    decisionMakingScope: jd.decisionMakingScope ?? null,
    financialAuthorityLimit: jd.financialAuthorityLimit ?? null,
    approvalAuthorityNotes: jd.approvalAuthorityNotes ?? null,
    // Read as the enum's NAME and written back as the same name — JsonStringEnumConverter matches
    // the C# member. Nothing on the form sets it; carrying it here is what stops the PUT clearing it.
    intendedEmploymentType: jd.intendedEmploymentType ?? null,
  };
}

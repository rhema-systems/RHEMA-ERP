import { apiService } from '../api.service';
import type {
  BenefitGroup,
  BenefitGroupMember,
  BenefitGroupMemberInput,
  CertificationSet,
  CertificationSetMember,
  CertificationSetMemberInput,
  EffectiveBenefit,
  EffectiveCertification,
  EffectiveSkill,
  NamedSetRequest,
  SkillSet,
  SkillSetMember,
  SkillSetMemberInput,
} from '@/types/hr/named-sets';

/**
 * Named sets (demo feedback round 2, lane C3, plan § 6.4).
 *
 * - The three masters under `api/hr/reference/{benefit-groups|skill-sets|certification-sets}`,
 *   beside the other HR reference data, each with a nested `/{id}/members` collection.
 * - A position's ATTACHED sets are written with the position save, as ids on the whole-set
 *   payload (`benefitGroupIds`, `skillSetIds`, `certificationSetIds`) — the same replace-set
 *   convention the position's skills, benefits and certifications already follow. Omitting a
 *   list leaves that kind alone; sending an empty one detaches everything of that kind.
 * - The EFFECTIVE reads say what the post actually requires — attached sets unioned with
 *   individual rows, each line naming its source. Use them to grey out an item a set already
 *   provides, and to mark an individual row that has become redundant.
 */
class NamedSetService {
  private readonly benefitGroups = '/hr/reference/benefit-groups';
  private readonly skillSets = '/hr/reference/skill-sets';
  private readonly certificationSets = '/hr/reference/certification-sets';

  private query(params: { activeOnly?: boolean; search?: string }) {
    return {
      ...(params.activeOnly ? { activeOnly: true } : {}),
      ...(params.search ? { search: params.search } : {}),
    };
  }

  // ── Benefit groups ─────────────────────────────────────────────────────────

  getBenefitGroups(params: { activeOnly?: boolean; search?: string } = {}): Promise<BenefitGroup[]> {
    return apiService.get<BenefitGroup[]>(this.benefitGroups, this.query(params));
  }

  getBenefitGroup(id: string): Promise<BenefitGroup> {
    return apiService.get<BenefitGroup>(`${this.benefitGroups}/${id}`);
  }

  createBenefitGroup(body: NamedSetRequest): Promise<BenefitGroup> {
    return apiService.post<BenefitGroup>(this.benefitGroups, body);
  }

  updateBenefitGroup(id: string, body: NamedSetRequest): Promise<BenefitGroup> {
    return apiService.put<BenefitGroup>(`${this.benefitGroups}/${id}`, { ...body, id });
  }

  removeBenefitGroup(id: string): Promise<void> {
    return apiService.delete<void>(`${this.benefitGroups}/${id}`);
  }

  getBenefitGroupMembers(id: string): Promise<BenefitGroupMember[]> {
    return apiService.get<BenefitGroupMember[]>(`${this.benefitGroups}/${id}/members`);
  }

  addBenefitGroupMember(id: string, body: BenefitGroupMemberInput): Promise<BenefitGroupMember> {
    return apiService.post<BenefitGroupMember>(`${this.benefitGroups}/${id}/members`, body);
  }

  updateBenefitGroupMember(id: string, memberId: string, body: BenefitGroupMemberInput): Promise<BenefitGroupMember> {
    return apiService.put<BenefitGroupMember>(`${this.benefitGroups}/${id}/members/${memberId}`, body);
  }

  removeBenefitGroupMember(id: string, memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.benefitGroups}/${id}/members/${memberId}`);
  }

  // ── Skill sets ─────────────────────────────────────────────────────────────

  getSkillSets(params: { activeOnly?: boolean; search?: string } = {}): Promise<SkillSet[]> {
    return apiService.get<SkillSet[]>(this.skillSets, this.query(params));
  }

  getSkillSet(id: string): Promise<SkillSet> {
    return apiService.get<SkillSet>(`${this.skillSets}/${id}`);
  }

  createSkillSet(body: NamedSetRequest): Promise<SkillSet> {
    return apiService.post<SkillSet>(this.skillSets, body);
  }

  updateSkillSet(id: string, body: NamedSetRequest): Promise<SkillSet> {
    return apiService.put<SkillSet>(`${this.skillSets}/${id}`, { ...body, id });
  }

  removeSkillSet(id: string): Promise<void> {
    return apiService.delete<void>(`${this.skillSets}/${id}`);
  }

  getSkillSetMembers(id: string): Promise<SkillSetMember[]> {
    return apiService.get<SkillSetMember[]>(`${this.skillSets}/${id}/members`);
  }

  addSkillSetMember(id: string, body: SkillSetMemberInput): Promise<SkillSetMember> {
    return apiService.post<SkillSetMember>(`${this.skillSets}/${id}/members`, body);
  }

  updateSkillSetMember(id: string, memberId: string, body: SkillSetMemberInput): Promise<SkillSetMember> {
    return apiService.put<SkillSetMember>(`${this.skillSets}/${id}/members/${memberId}`, body);
  }

  removeSkillSetMember(id: string, memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.skillSets}/${id}/members/${memberId}`);
  }

  // ── Certification sets ─────────────────────────────────────────────────────

  getCertificationSets(params: { activeOnly?: boolean; search?: string } = {}): Promise<CertificationSet[]> {
    return apiService.get<CertificationSet[]>(this.certificationSets, this.query(params));
  }

  getCertificationSet(id: string): Promise<CertificationSet> {
    return apiService.get<CertificationSet>(`${this.certificationSets}/${id}`);
  }

  createCertificationSet(body: NamedSetRequest): Promise<CertificationSet> {
    return apiService.post<CertificationSet>(this.certificationSets, body);
  }

  updateCertificationSet(id: string, body: NamedSetRequest): Promise<CertificationSet> {
    return apiService.put<CertificationSet>(`${this.certificationSets}/${id}`, { ...body, id });
  }

  removeCertificationSet(id: string): Promise<void> {
    return apiService.delete<void>(`${this.certificationSets}/${id}`);
  }

  getCertificationSetMembers(id: string): Promise<CertificationSetMember[]> {
    return apiService.get<CertificationSetMember[]>(`${this.certificationSets}/${id}/members`);
  }

  addCertificationSetMember(id: string, body: CertificationSetMemberInput): Promise<CertificationSetMember> {
    return apiService.post<CertificationSetMember>(`${this.certificationSets}/${id}/members`, body);
  }

  updateCertificationSetMember(
    id: string,
    memberId: string,
    body: CertificationSetMemberInput,
  ): Promise<CertificationSetMember> {
    return apiService.put<CertificationSetMember>(`${this.certificationSets}/${id}/members/${memberId}`, body);
  }

  removeCertificationSetMember(id: string, memberId: string): Promise<void> {
    return apiService.delete<void>(`${this.certificationSets}/${id}/members/${memberId}`);
  }

  // ── The effective reads, on the position ───────────────────────────────────

  getEffectiveBenefits(positionId: string): Promise<EffectiveBenefit[]> {
    return apiService.get<EffectiveBenefit[]>(`/EmployeePositions/${positionId}/effective-benefits`);
  }

  getEffectiveSkills(positionId: string): Promise<EffectiveSkill[]> {
    return apiService.get<EffectiveSkill[]>(`/EmployeePositions/${positionId}/effective-skills`);
  }

  getEffectiveCertifications(positionId: string): Promise<EffectiveCertification[]> {
    return apiService.get<EffectiveCertification[]>(`/EmployeePositions/${positionId}/effective-certifications`);
  }
}

export const namedSetService = new NamedSetService();

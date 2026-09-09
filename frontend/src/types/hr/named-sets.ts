// Demo feedback round 2, lane C3 — named sets (plan § 1.5, § 6.4).
// Mirrors NamedSetDTOs.cs.
//
// Three masters of one shape: a benefit group, a skill set, a certification set. Each is a header
// plus members, attachable to a position beside that position's individual rows. What the post
// actually requires is the EFFECTIVE union of the two, which the server computes and every
// consumer reads — see the Effective* types at the bottom.

import type { AuditFields } from './common';
import type { SkillLevel } from './position';

// ── The shared header shape ──────────────────────────────────────────────────

interface NamedSetBase extends AuditFields {
  tenantId: string;
  name: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
  memberCount: number;
  /** How many posts hold it. The server refuses a delete while this is non-zero. */
  positionCount: number;
}

export interface NamedSetRequest {
  name: string;
  code?: string | null;
  description?: string | null;
  isActive: boolean;
}

// ── Benefit groups ───────────────────────────────────────────────────────────

export interface BenefitGroup extends NamedSetBase {
  members: BenefitGroupMember[];
}

export interface BenefitGroupMember extends AuditFields {
  benefitGroupId: string;
  policyId: string;
  policyName: string;
  policyCode?: string | null;
  policyIsActive: boolean;
}

/**
 * ⚠ The policy and nothing else. A post needing its own amount or expiry for a benefit takes that
 * benefit individually rather than from a group (plan Q-5).
 */
export interface BenefitGroupMemberInput {
  policyId: string;
}

// ── Skill sets ───────────────────────────────────────────────────────────────

export interface SkillSet extends NamedSetBase {
  members: SkillSetMember[];
}

export interface SkillSetMember extends AuditFields {
  skillSetId: string;
  skillId: string;
  skillName: string;
  skillCategory?: string | null;
  skillIsActive: boolean;
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
}

export interface SkillSetMemberInput {
  skillId: string;
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
}

// ── Certification sets ───────────────────────────────────────────────────────

export interface CertificationSet extends NamedSetBase {
  members: CertificationSetMember[];
}

export interface CertificationSetMember extends AuditFields {
  certificationSetId: string;
  certificationId: string;
  certificationName: string;
  certificationCode?: string | null;
  certifyingBodyName: string;
  certificationIsActive: boolean;
  isMandatory: boolean;
}

export interface CertificationSetMemberInput {
  certificationId: string;
  isMandatory: boolean;
}

// ── Attached to a position ───────────────────────────────────────────────────

/** A set attached to a post: enough to name it and detach it, no members. */
export interface AttachedSet {
  /** The attachment row's own id. */
  id: string;
  setId: string;
  name: string;
  code?: string | null;
  isActive: boolean;
  memberCount: number;
}

// ── The effective reads ──────────────────────────────────────────────────────

/**
 * Where an effective line came from. A line provided by two attached sets carries both: overlap
 * between sets is allowed and the read says so rather than hiding it.
 */
export interface EffectiveSource {
  kind: 'Individual' | 'Set';
  setId?: string | null;
  setName?: string | null;
  setCode?: string | null;
  /** What to print: "Individual" or "Set: SAFETY-CORE". */
  label: string;
}

export interface EffectiveBenefit {
  policyId: string;
  policyName: string;
  policyCode?: string | null;
  policyIsActive: boolean;
  /** Only ever set on an individual line — group members carry no amount. */
  positionAmount?: number | null;
  expiryDate?: string | null;
  /** The individual row's id where this line is one; null when a group provides it. */
  positionBenefitId?: string | null;
  sources: EffectiveSource[];
}

export interface EffectiveSkill {
  skillId: string;
  skillName: string;
  skillCategory?: string | null;
  requiresCertification: boolean;
  /** Where sources disagree the strongest wins: highest level, required beats preferred. */
  requiredLevel: SkillLevel;
  isRequired: boolean;
  priority: number;
  positionSkillRequirementId?: string | null;
  sources: EffectiveSource[];
}

export interface EffectiveCertification {
  certificationId: string;
  certificationName: string;
  certificationCode?: string | null;
  certifyingBodyName: string;
  certificationIsActive: boolean;
  isMandatory: boolean;
  positionCertificationRequirementId?: string | null;
  sources: EffectiveSource[];
}

/** True when a line is provided by at least one set — what the form marks as covered. */
export const isFromSet = (sources: EffectiveSource[]): boolean => sources.some((s) => s.kind === 'Set');

/** True when a line is BOTH an individual row and set-provided — the redundancy the rule refuses. */
export const isRedundantIndividual = (sources: EffectiveSource[]): boolean =>
  sources.some((s) => s.kind === 'Individual') && sources.some((s) => s.kind === 'Set');

/** The set names covering a line, for the "covered by X — remove" hint. */
export const coveringSetNames = (sources: EffectiveSource[]): string[] =>
  sources.filter((s) => s.kind === 'Set').map((s) => s.setName ?? s.setCode ?? 'a set');

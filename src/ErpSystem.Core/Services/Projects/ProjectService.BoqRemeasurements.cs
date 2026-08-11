using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ErpSystem.Core.DTOs.Projects;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.QuantitySurvey;
using ErpSystem.Core.Enums;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Services.Projects;

public partial class ProjectService
{
    public async Task<ProjectBoqRemeasurementWorkspaceDto> GetProjectBoqRemeasurementWorkspaceAsync(Guid projectId)
    {
        await RequireProjectAsync(projectId, ProjectAccessOperation.View);
        var versions = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectId == projectId))
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.VersionNumber)
            .ToList();
        var publication = versions.FirstOrDefault(IsCurrentBoqPublication)
            ?? throw new InvalidOperationException("Approve and publish a BoQ before preparing a remeasurement revision.");
        var openCandidate = versions.FirstOrDefault(value =>
            value.VersionType != QuantitySurveyBoqVersionType.Approved
            && value.Status is ProjectBoqVersionStatuses.Draft or ProjectBoqVersionStatuses.PendingApproval or ProjectBoqVersionStatuses.Rejected);

        var sourceLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && value.ProjectBoqVersionId == publication.Id))
            .ToDictionary(value => value.Id);
        var measurements = (await _unitOfWork.Repository<QuantitySurveyMeasurementSheet>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == projectId
                && value.ProjectBoqVersionId == publication.Id
                && value.Status == "Recorded"))
            .Where(value => sourceLines.ContainsKey(value.ProjectBoqVersionLineId))
            .ToList();
        var usedMeasurementIds = (await _unitOfWork.Repository<ProjectBoqRemeasurementSource>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectId == projectId))
            .Select(value => value.MeasurementSheetId)
            .ToHashSet();

        return new ProjectBoqRemeasurementWorkspaceDto
        {
            ProjectId = projectId,
            SourceApprovedBoqVersionId = publication.Id,
            SourceApprovedBoqVersionNumber = publication.VersionNumber,
            OpenCandidateVersionId = openCandidate?.Id,
            EligibleMeasurements = measurements
                .Where(value => !usedMeasurementIds.Contains(value.Id))
                .OrderBy(value => value.BoqLineNumberSnapshot)
                .ThenBy(value => value.MeasurementDate)
                .ThenBy(value => value.SheetReference)
                .Select(value =>
                {
                    var line = sourceLines[value.ProjectBoqVersionLineId];
                    return new ProjectBoqRemeasurementMeasurementDto
                    {
                        MeasurementSheetId = value.Id,
                        SheetReference = value.SheetReference,
                        ProjectBoqVersionLineId = line.Id,
                        BoqLineKey = line.LineKey,
                        BoqLineLabel = $"{line.LineNumber ?? line.ItemCode ?? "Line"} · {line.Description}",
                        UnitOfMeasure = line.UnitOfMeasure,
                        PreviousQuantity = line.Quantity,
                        MeasuredQuantity = value.TotalMeasuredQuantity,
                        MeasurementDate = value.MeasurementDate,
                        RecordedAt = value.RecordedAt ?? value.UpdatedAt ?? value.CreatedAt,
                        RecordedByName = value.RecordedByName
                    };
                })
                .ToList()
        };
    }

    public async Task<ProjectBoqVersionDetailDto> CreateProjectBoqRemeasurementAsync(
        Guid projectId,
        CreateProjectBoqRemeasurementDto dto,
        string correlationId)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (dto.ClientRequestId == Guid.Empty) throw new InvalidOperationException("Reload the remeasurement workspace before creating the revision.");
        var selectedIds = dto.MeasurementSheetIds.Where(value => value != Guid.Empty).Distinct().OrderBy(value => value).ToList();
        if (selectedIds.Count == 0) throw new InvalidOperationException("Select at least one Recorded measurement sheet.");
        if (selectedIds.Count != dto.MeasurementSheetIds.Count) throw new InvalidOperationException("Measurement selections must be unique and valid.");
        var summary = dto.ChangeSummary?.Trim();
        if (string.IsNullOrWhiteSpace(summary) || summary.Length < 5) throw new InvalidOperationException("Enter a meaningful change summary of at least 5 characters.");
        summary = summary[..Math.Min(summary.Length, 2000)];
        await RequireProjectAsync(projectId, ProjectAccessOperation.ManagePlan);

        var versionId = await _unitOfWork.ExecuteInStrategyAsync(async () =>
        {
            var transactionStarted = false;
            try
            {
                await _unitOfWork.BeginTransactionAsync(IsolationLevel.Serializable);
                transactionStarted = true;
                await _unitOfWork.AcquireTransactionLockAsync($"qs-boq-remeasurement:{_currentUserProvider.TenantId:N}:{projectId:N}");

                var versions = (await _unitOfWork.Repository<ProjectBoqVersion>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId && value.ProjectId == projectId))
                    .OrderBy(value => value.VersionNumber).ToList();
                var publication = versions.Where(IsCurrentBoqPublication)
                    .OrderByDescending(value => value.PublishedAt).ThenByDescending(value => value.VersionNumber).FirstOrDefault()
                    ?? throw new InvalidOperationException("Approve and publish a BoQ before preparing a remeasurement revision.");
                var requestHash = ComputeRemeasurementRequestHash(projectId, publication.Id, selectedIds, summary);
                var existing = (await _unitOfWork.Repository<ProjectBoqRemeasurementRevision>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.ProjectId == projectId
                        && value.ClientRequestId == dto.ClientRequestId))
                    .SingleOrDefault();
                if (existing != null)
                {
                    if (!string.Equals(existing.RequestHash, requestHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("This remeasurement request identifier was already used for different measurements.");
                    await _unitOfWork.RollbackAsync();
                    transactionStarted = false;
                    return existing.ProjectBoqVersionId;
                }

                ValidateBoqVersionCreation(QuantitySurveyBoqVersionType.Remeasurement, publication.Id, versions, await GetEffectiveBoqVersionPolicyAsync());
                var sourceLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.ProjectId == projectId
                        && value.ProjectBoqVersionId == publication.Id))
                    .OrderBy(value => value.SortOrder).ThenBy(value => value.LineNumber).ToList();
                var sourceLinesById = sourceLines.ToDictionary(value => value.Id);
                var measurements = (await _unitOfWork.Repository<QuantitySurveyMeasurementSheet>().FindAsync(value =>
                        value.TenantId == _currentUserProvider.TenantId
                        && value.ProjectId == projectId
                        && selectedIds.Contains(value.Id)))
                    .ToList();
                if (measurements.Count != selectedIds.Count
                    || measurements.Any(value => value.Status != "Recorded" || value.ProjectBoqVersionId != publication.Id)
                    || measurements.Any(value => !sourceLinesById.ContainsKey(value.ProjectBoqVersionLineId)))
                    throw new InvalidOperationException("Every selected sheet must be a Recorded measurement against the current approved BoQ.");
                var alreadyUsed = (await _unitOfWork.Repository<ProjectBoqRemeasurementSource>().FindAsync(value =>
                    value.TenantId == _currentUserProvider.TenantId && selectedIds.Contains(value.MeasurementSheetId))).Any();
                if (alreadyUsed) throw new InvalidOperationException("One or more selected measurements already belong to a governed remeasurement revision.");

                var measuredByLine = measurements.GroupBy(value => value.ProjectBoqVersionLineId)
                    .ToDictionary(group => group.Key, group => decimal.Round(group.Sum(value => value.TotalMeasuredQuantity), 4, MidpointRounding.AwayFromZero));
                if (measuredByLine.Any(pair => pair.Value < 0m)) throw new InvalidOperationException("A revised BoQ quantity cannot be negative.");
                var now = DateTime.UtcNow;
                var candidateLines = sourceLines.Select(source =>
                {
                    var line = CloneBoqPublicationLine(source, Guid.Empty, now);
                    if (measuredByLine.TryGetValue(source.Id, out var revised))
                    {
                        line.Quantity = revised;
                        line.LineAmount = line.UnitRate.HasValue
                            ? decimal.Round(revised * line.UnitRate.Value, 2, MidpointRounding.AwayFromZero)
                            : null;
                    }
                    return line;
                }).ToList();
                var changed = candidateLines.Zip(sourceLines)
                    .Where(pair => pair.First.Quantity != pair.Second.Quantity).ToList();
                if (changed.Count == 0) throw new InvalidOperationException("The selected measurements do not change any approved BoQ quantity.");

                QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.CreateRemeasurementVersion);
                var version = new ProjectBoqVersion
                {
                    TenantId = _currentUserProvider.TenantId, ProjectId = projectId, SourceVersionId = publication.Id,
                    VersionNumber = versions.Select(value => value.VersionNumber).DefaultIfEmpty(0).Max() + 1,
                    VersionType = QuantitySurveyBoqVersionType.Remeasurement, Status = ProjectBoqVersionStatuses.Draft,
                    ApprovalStatus = ProjectBoqVersionStatuses.Draft, ChangeSummary = summary,
                    AuditAction = QuantitySurveyAuditEventMap.CreateRemeasurementVersion,
                    SnapshotHash = string.Empty, LineCount = candidateLines.Count, SnapshotAt = now,
                    ActorRoles = CurrentBoqActorRoles(), CorrelationId = NormalizeCorrelationId(correlationId),
                    CreatedAt = now, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
                };
                foreach (var line in candidateLines) line.ProjectBoqVersionId = version.Id;
                version.SnapshotHash = ComputeBoqSnapshotHash(candidateLines);
                var measurementSetHash = ComputeMeasurementSetHash(measurements);
                var revision = new ProjectBoqRemeasurementRevision
                {
                    TenantId = _currentUserProvider.TenantId, ProjectId = projectId, ProjectBoqVersionId = version.Id,
                    SourceApprovedBoqVersionId = publication.Id, ClientRequestId = dto.ClientRequestId,
                    RequestHash = requestHash, MeasurementSetHash = measurementSetHash,
                    SelectedMeasurementCount = measurements.Count, ChangedLineCount = changed.Count,
                    TotalAbsoluteQuantityDelta = changed.Sum(pair => Math.Abs(pair.First.Quantity - pair.Second.Quantity)),
                    IsFinalized = false, CreatedAt = now, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
                };
                await _unitOfWork.Repository<ProjectBoqVersion>().AddAsync(version);
                foreach (var line in candidateLines) await _unitOfWork.Repository<ProjectBoqVersionLine>().AddAsync(line);
                await _unitOfWork.Repository<ProjectBoqRemeasurementRevision>().AddAsync(revision);

                foreach (var pair in changed)
                {
                    var candidateLine = pair.First;
                    var sourceLine = pair.Second;
                    var lineage = new ProjectBoqRemeasurementLine
                    {
                        TenantId = _currentUserProvider.TenantId, ProjectId = projectId, RemeasurementRevisionId = revision.Id,
                        ProjectBoqVersionId = version.Id, ProjectBoqVersionLineId = candidateLine.Id,
                        SourceApprovedBoqVersionLineId = sourceLine.Id, BoqLineKey = sourceLine.LineKey,
                        PreviousQuantity = sourceLine.Quantity, RevisedQuantity = candidateLine.Quantity,
                        QuantityDelta = candidateLine.Quantity - sourceLine.Quantity,
                        CreatedAt = now, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
                    };
                    await _unitOfWork.Repository<ProjectBoqRemeasurementLine>().AddAsync(lineage);
                    foreach (var measurement in measurements.Where(value => value.ProjectBoqVersionLineId == sourceLine.Id))
                    {
                        await _unitOfWork.Repository<ProjectBoqRemeasurementSource>().AddAsync(new ProjectBoqRemeasurementSource
                        {
                            TenantId = _currentUserProvider.TenantId, ProjectId = projectId,
                            RemeasurementRevisionId = revision.Id, RemeasurementLineId = lineage.Id,
                            MeasurementSheetId = measurement.Id, MeasurementReferenceSnapshot = measurement.SheetReference,
                            MeasuredQuantitySnapshot = measurement.TotalMeasuredQuantity,
                            RecordedAtSnapshot = measurement.RecordedAt ?? measurement.UpdatedAt ?? measurement.CreatedAt,
                            MeasurementRequestHashSnapshot = measurement.RequestHash,
                            CreatedAt = now, CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
                        });
                    }
                }

                await _unitOfWork.SaveChangesAsync();
                revision.IsFinalized = true;
                revision.UpdatedBy = _currentUserProvider.Username;
                revision.LastModifiedById = _currentUserProvider.UserId;
                await _unitOfWork.Repository<ProjectBoqRemeasurementRevision>().UpdateAsync(revision);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitAsync();
                transactionStarted = false;
                return version.Id;
            }
            catch
            {
                try { if (transactionStarted && _unitOfWork.HasActiveTransaction) await _unitOfWork.RollbackAsync(); }
                finally { _unitOfWork.ClearTrackedChanges(); }
                throw;
            }
        });

        return await GetProjectBoqVersionAsync(projectId, versionId);
    }

    private async Task ValidateRemeasurementVersionAsync(ProjectBoqVersion version)
    {
        if (version.VersionType != QuantitySurveyBoqVersionType.Remeasurement) return;
        var revision = (await _unitOfWork.Repository<ProjectBoqRemeasurementRevision>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectBoqVersionId == version.Id))
            .SingleOrDefault() ?? throw new InvalidOperationException("The remeasurement version has no governed measurement lineage.");
        if (!revision.IsFinalized || revision.SourceApprovedBoqVersionId != version.SourceVersionId
            || revision.SelectedMeasurementCount <= 0 || revision.ChangedLineCount <= 0)
            throw new InvalidOperationException("The remeasurement lineage is incomplete and cannot enter approval.");
        var lines = (await _unitOfWork.Repository<ProjectBoqRemeasurementLine>().FindAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.RemeasurementRevisionId == revision.Id)).ToList();
        var sources = (await _unitOfWork.Repository<ProjectBoqRemeasurementSource>().FindAsync(value =>
            value.TenantId == _currentUserProvider.TenantId && value.RemeasurementRevisionId == revision.Id)).ToList();
        if (lines.Count != revision.ChangedLineCount || sources.Count != revision.SelectedMeasurementCount
            || lines.Any(line => decimal.Round(sources.Where(source => source.RemeasurementLineId == line.Id)
                    .Sum(source => source.MeasuredQuantitySnapshot), 4, MidpointRounding.AwayFromZero) != line.RevisedQuantity))
            throw new InvalidOperationException("The remeasurement quantity and measurement-source lineage no longer reconcile.");
    }

    private async Task ApplyApprovedRemeasurementToWorkingBoqAsync(ProjectBoqVersion version, string correlationId)
    {
        if (version.VersionType != QuantitySurveyBoqVersionType.Remeasurement) return;
        await ValidateRemeasurementVersionAsync(version);
        var revision = (await _unitOfWork.Repository<ProjectBoqRemeasurementRevision>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectBoqVersionId == version.Id))
            .Single();
        var changedLines = (await _unitOfWork.Repository<ProjectBoqRemeasurementLine>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.RemeasurementRevisionId == revision.Id))
            .ToList();
        var versionLines = (await _unitOfWork.Repository<ProjectBoqVersionLine>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId && value.ProjectBoqVersionId == version.Id))
            .ToDictionary(value => value.Id);
        var working = (await GetProjectBoqItemEntitiesAsync(version.ProjectId)).ToDictionary(value => value.Id);
        var affectedPackages = new HashSet<Guid>();
        foreach (var changed in changedLines)
        {
            if (!versionLines.TryGetValue(changed.ProjectBoqVersionLineId, out var candidateLine)
                || !candidateLine.SourceBoqItemId.HasValue
                || !working.TryGetValue(candidateLine.SourceBoqItemId.Value, out var item)
                || item.VersionLineKey != changed.BoqLineKey)
                throw new InvalidOperationException("A working BoQ line no longer matches the approved remeasurement lineage.");
            if (item.Quantity != changed.PreviousQuantity && item.Quantity != changed.RevisedQuantity)
                throw new InvalidOperationException("The working BoQ changed after the remeasurement was prepared. Approval cannot overwrite the newer quantity.");
            if (item.Quantity == changed.RevisedQuantity) continue;
            item.Quantity = changed.RevisedQuantity;
            item.UpdatedBy = _currentUserProvider.Username;
            item.LastModifiedById = _currentUserProvider.UserId;
            affectedPackages.Add(item.ProjectPackageId);
            await _unitOfWork.Repository<ProjectBoqItem>().UpdateAsync(item);
        }
        foreach (var packageId in affectedPackages) await SyncProjectPackageAmountsFromBoqAsync(packageId);

        var jointRequests = (await _unitOfWork.Repository<QuantitySurveyJointMeasurementRequest>().FindAsync(value =>
                value.TenantId == _currentUserProvider.TenantId
                && value.ProjectId == version.ProjectId
                && value.RemeasurementVersionId == version.Id))
            .ToList();
        if (jointRequests.Count > 1)
            throw new InvalidOperationException("The approved remeasurement revision is linked to more than one joint-measurement request.");
        if (jointRequests.Count == 0) return;

        var joint = jointRequests[0];
        if (joint.Status == QuantitySurveyJointMeasurementStatuses.Applied) return;
        if (joint.Status != QuantitySurveyJointMeasurementStatuses.BoqWorkflowPending
            || !joint.MeasurementSheetId.HasValue
            || version.Status != ProjectBoqVersionStatuses.Approved)
            throw new InvalidOperationException("The joint-measurement request is not ready for approved BoQ application.");
        var sourceMatches = (await _unitOfWork.Repository<ProjectBoqRemeasurementSource>().FindAsync(value =>
            value.TenantId == _currentUserProvider.TenantId
            && value.RemeasurementRevisionId == revision.Id
            && value.MeasurementSheetId == joint.MeasurementSheetId.Value)).SingleOrDefault();
        if (sourceMatches is null)
            throw new InvalidOperationException("The approved BoQ revision does not contain the joint measurement's governed source sheet.");

        QuantitySurveyAuditEventMap.GetRequired(QuantitySurveyAuditEventMap.ApplyJointMeasurementBoqRevision);
        var now = DateTime.UtcNow;
        var oldValues = JsonSerializer.Serialize(new
        {
            joint.Id, joint.Status, joint.ApprovalStatus, joint.RemeasurementVersionId, joint.AppliedAt
        });
        joint.Status = QuantitySurveyJointMeasurementStatuses.Applied;
        joint.ApprovalStatus = ProjectBoqVersionStatuses.Approved;
        joint.AppliedAt = now;
        joint.AuditAction = QuantitySurveyAuditEventMap.ApplyJointMeasurementBoqRevision;
        joint.CorrelationId = NormalizeCorrelationId(correlationId);
        joint.UpdatedAt = now;
        joint.UpdatedBy = _currentUserProvider.Username;
        joint.LastModifiedById = _currentUserProvider.UserId;
        var newValues = JsonSerializer.Serialize(new
        {
            joint.Id, joint.Status, joint.ApprovalStatus, joint.RemeasurementVersionId, joint.AppliedAt,
            ApprovedRemeasurementVersionId = version.Id, SourceMeasurementSheetId = joint.MeasurementSheetId
        });
        await _unitOfWork.Repository<QuantitySurveyJointMeasurementRequest>().UpdateAsync(joint);
        await _unitOfWork.Repository<QuantitySurveyJointMeasurementRevision>().AddAsync(new QuantitySurveyJointMeasurementRevision
        {
            Id = Guid.NewGuid(), TenantId = _currentUserProvider.TenantId, RequestId = joint.Id,
            Action = QuantitySurveyAuditEventMap.ApplyJointMeasurementBoqRevision,
            ActorUserId = _currentUserProvider.UserId, ActorName = _currentUserProvider.Username,
            ActorRoles = CurrentBoqActorRoles(), CorrelationId = NormalizeCorrelationId(correlationId),
            Reason = "Approved remeasurement publication applied to the working BoQ.",
            BeforeJson = oldValues, AfterJson = newValues, CreatedAt = now,
            CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
        });
        await _unitOfWork.Repository<AuditLog>().AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = _currentUserProvider.TenantId,
            UserId = _currentUserProvider.UserId, Username = _currentUserProvider.Username,
            Action = QuantitySurveyAuditEventMap.ApplyJointMeasurementBoqRevision,
            Resource = nameof(QuantitySurveyJointMeasurementRequest), ResourceId = joint.Id.ToString(),
            OldValues = oldValues,
            NewValues = JsonSerializer.Serialize(new { correlationId = NormalizeCorrelationId(correlationId), value = JsonSerializer.Deserialize<JsonElement>(newValues) }),
            IpAddress = "Unknown", Timestamp = now, CreatedAt = now,
            CreatedBy = _currentUserProvider.Username, CreatedById = _currentUserProvider.UserId
        });
    }

    private static string ComputeRemeasurementRequestHash(Guid projectId, Guid publicationId, IReadOnlyCollection<Guid> ids, string summary)
        => Sha256(new { projectId, publicationId, measurementSheetIds = ids.OrderBy(value => value), changeSummary = summary });

    private static string ComputeMeasurementSetHash(IEnumerable<QuantitySurveyMeasurementSheet> measurements)
        => Sha256(measurements.OrderBy(value => value.Id).Select(value => new
        {
            value.Id, value.ProjectBoqVersionLineId, value.TotalMeasuredQuantity, value.RecordedAt, value.RequestHash
        }));

    private static string Sha256(object value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

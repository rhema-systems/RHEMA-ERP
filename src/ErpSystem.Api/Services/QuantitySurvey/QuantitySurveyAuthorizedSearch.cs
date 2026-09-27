using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Api.Services.QuantitySurvey;

/// <summary>Pages matching owner queries until the authorized result limit is filled.</summary>
internal static class QuantitySurveyAuthorizedSearch
{
    internal static async Task<IReadOnlyList<T>> ReadAsync<T>(IOrderedQueryable<T> query,
        Func<T, Guid> projectId, Func<Guid, Task<bool>> canReadProject, int take, CancellationToken token)
    {
        var limit = Math.Clamp(take, 1, 50);
        var result = new List<T>();
        var access = new Dictionary<Guid, bool>();
        for (var offset = 0; result.Count < limit; offset += 50)
        {
            token.ThrowIfCancellationRequested();
            var batch = await query.Skip(offset).Take(50).ToListAsync(token);
            foreach (var record in batch)
            {
                token.ThrowIfCancellationRequested();
                var project = projectId(record);
                if (!access.TryGetValue(project, out var allowed))
                    access[project] = allowed = await canReadProject(project);
                if (allowed) result.Add(record);
                if (result.Count == limit) break;
            }
            if (batch.Count < 50) break;
        }
        return result;
    }
}

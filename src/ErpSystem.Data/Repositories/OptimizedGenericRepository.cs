using System.Linq.Expressions;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

/// <summary>
/// High-performance optimized generic repository with advanced querying capabilities
/// </summary>
public class OptimizedGenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    protected readonly ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public OptimizedGenericRepository(ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    #region Basic CRUD Operations

    public async Task<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<T?> GetByIdWithIncludesAsync(
        object id,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        // Assume T has an Id property - this works for BaseEntity derived classes
        var parameter = Expression.Parameter(typeof(T), "x");
        var property = Expression.Property(parameter, "Id");
        var constant = Expression.Constant(id);
        var equals = Expression.Equal(property, constant);
        var lambda = Expression.Lambda<Func<T, bool>>(equals, parameter);

        return await query.FirstOrDefaultAsync(lambda);
    }

    public async Task<IEnumerable<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet.ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = await _dbSet.AddAsync(entity, cancellationToken);
        return result.Entity;
    }

    public async Task<T> AddAsync(T entity)
    {
        var result = await _dbSet.AddAsync(entity);
        return result.Entity;
    }

    public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entitiesList = entities.ToList();
        await _dbSet.AddRangeAsync(entitiesList, cancellationToken);
        return entitiesList;
    }

    public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        var entitiesList = entities.ToList();
        await _dbSet.AddRangeAsync(entitiesList);
        return entitiesList;
    }

    public void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public void UpdateRange(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
    }

    public async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity == null)
        {
            return false;
        }

        _dbSet.Remove(entity);
        return true;
    }

    public void Delete(T entity)
    {
        _dbSet.Remove(entity);
    }

    public void DeleteRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    #endregion

    #region Advanced Query Operations

    public async Task<IEnumerable<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public async Task<IEnumerable<T>> FindWithIncludesAsync(
        Expression<Func<T, bool>> predicate,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        return await query.Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await _dbSet.AnyAsync(predicate, cancellationToken);
    }

    public async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await _dbSet.CountAsync(cancellationToken)
            : await _dbSet.CountAsync(predicate, cancellationToken);
    }

    #endregion

    #region Pagination and Sorting

    public async Task<PagedResult<T>> GetPagedAsync<TKey>(
        int pageNumber,
        int pageSize,
        Expression<Func<T, TKey>> orderBy,
        bool descending = false,
        Expression<Func<T, bool>>? filter = null,
        params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;

        // Apply includes
        foreach (var include in includes)
        {
            query = query.Include(include);
        }

        // Apply filter
        if (filter != null)
        {
            query = query.Where(filter);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = descending
            ? query.OrderByDescending(orderBy)
            : query.OrderBy(orderBy);

        // Apply pagination
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    public async Task<PagedResult<TDto>> GetPagedWithProjectionAsync<TDto, TKey>(
        int pageNumber,
        int pageSize,
        Expression<Func<T, TKey>> orderBy,
        Expression<Func<T, TDto>> projection,
        bool descending = false,
        Expression<Func<T, bool>>? filter = null)
    {
        IQueryable<T> query = _dbSet;

        // Apply filter
        if (filter != null)
        {
            query = query.Where(filter);
        }

        // Get total count before pagination
        var totalCount = await query.CountAsync();

        // Apply sorting
        query = descending
            ? query.OrderByDescending(orderBy)
            : query.OrderBy(orderBy);

        // Apply pagination and projection
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync();

        return new PagedResult<TDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        };
    }

    #endregion

    #region Bulk Operations

    public async Task<int> BulkUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Expression<Func<T, T>> updateExpression,
        CancellationToken cancellationToken = default)
    {
        // Note: This would typically use EF Extensions or similar for better performance
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);

        foreach (var entity in entities)
        {
            var compiledUpdate = updateExpression.Compile();
            var updatedEntity = compiledUpdate(entity);
            _context.Entry(entity).CurrentValues.SetValues(updatedEntity);
        }

        return entities.Count;
    }

    public async Task<int> BulkDeleteAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync(cancellationToken);
        _dbSet.RemoveRange(entities);
        return entities.Count;
    }

    #endregion

    #region Caching Support

    public async Task<T?> GetByIdCachedAsync(
        object id,
        TimeSpan? cacheExpiry = null,
        CancellationToken cancellationToken = default)
    {
        // This would integrate with a caching service
        // For now, just delegate to regular method
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<T>> FindCachedAsync(
        Expression<Func<T, bool>> predicate,
        string cacheKey,
        TimeSpan? cacheExpiry = null,
        CancellationToken cancellationToken = default)
    {
        // This would integrate with a caching service
        // For now, just delegate to regular method
        return await FindAsync(predicate, cancellationToken);
    }

    #endregion

    #region Raw SQL Support

    public async Task<IEnumerable<T>> FromSqlAsync(
        string sql,
        params object[] parameters)
    {
        return await _dbSet.FromSqlRaw(sql, parameters).ToListAsync();
    }

    public async Task<int> ExecuteSqlAsync(
        string sql,
        params object[] parameters)
    {
        return await _context.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    #endregion

    #region IGenericRepository Implementation

    public async Task<T?> GetByIdAsync(Guid id)
    {
        return await _dbSet.FindAsync(id);
    }

    public async Task<T?> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.ToListAsync();
    }

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.Where(predicate).ToListAsync();
    }

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
    {
        IQueryable<T> query = _dbSet;
        foreach (var include in includes)
        {
            query = query.Include(include);
        }
        return await query.FirstOrDefaultAsync(predicate);
    }

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.CountAsync(predicate);
    }

    public async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
    {
        return await _dbSet.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetPagedAsync<TKey>(int page, int pageSize, Expression<Func<T, TKey>> orderBy, bool descending = false)
    {
        var query = descending ? _dbSet.OrderByDescending(orderBy) : _dbSet.OrderBy(orderBy);
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task<IEnumerable<T>> GetPagedAsync<TKey>(int page, int pageSize, Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy, bool descending = false)
    {
        var query = _dbSet.Where(predicate);
        query = descending ? query.OrderByDescending(orderBy) : query.OrderBy(orderBy);
        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
    }

    public async Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await Task.CompletedTask;
    }

    public async Task UpdateRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.UpdateRange(entities);
        await Task.CompletedTask;
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await GetByIdAsync(id);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }

    public async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public async Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
        await Task.CompletedTask;
    }

    public async Task DeleteRangeAsync(Expression<Func<T, bool>> predicate)
    {
        var entities = await _dbSet.Where(predicate).ToListAsync();
        _dbSet.RemoveRange(entities);
    }

    public IQueryable<T> GetQueryable()
    {
        return _dbSet.AsQueryable();
    }

    public IQueryable<T> GetQueryable(Expression<Func<T, bool>> predicate)
    {
        return _dbSet.Where(predicate).AsQueryable();
    }

    public IQueryable<T> GetQueryableIncludingDeleted(Expression<Func<T, bool>> predicate)
    {
        return _dbSet.IgnoreQueryFilters().Where(predicate);
    }

    public async Task<IEnumerable<TResult>> GetProjectedAsync<TResult>(Expression<Func<T, TResult>> projection)
    {
        return await _dbSet.Select(projection).ToListAsync();
    }

    public async Task<IEnumerable<TResult>> GetProjectedAsync<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> projection)
    {
        return await _dbSet.Where(predicate).Select(projection).ToListAsync();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task HardDeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await Task.CompletedTask;
    }

    public async Task HardDeleteRangeAsync(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
        await Task.CompletedTask;
    }

    #endregion
}

/// <summary>
/// Enhanced interface for optimized generic repository
/// </summary>
public interface IOptimizedGenericRepository<T> : IGenericRepository<T> where T : BaseEntity
{
    // Advanced querying
    Task<T?> GetByIdWithIncludesAsync(object id, params Expression<Func<T, object>>[] includes);
    Task<IEnumerable<T>> FindWithIncludesAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes);
    Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    // Pagination
    Task<PagedResult<T>> GetPagedAsync<TKey>(int pageNumber, int pageSize, Expression<Func<T, TKey>> orderBy, bool descending = false, Expression<Func<T, bool>>? filter = null, params Expression<Func<T, object>>[] includes);
    Task<PagedResult<TDto>> GetPagedWithProjectionAsync<TDto, TKey>(int pageNumber, int pageSize, Expression<Func<T, TKey>> orderBy, Expression<Func<T, TDto>> projection, bool descending = false, Expression<Func<T, bool>>? filter = null);

    // Bulk operations
    Task<int> BulkUpdateAsync(Expression<Func<T, bool>> predicate, Expression<Func<T, T>> updateExpression, CancellationToken cancellationToken = default);
    Task<int> BulkDeleteAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    // Caching
    Task<T?> GetByIdCachedAsync(object id, TimeSpan? cacheExpiry = null, CancellationToken cancellationToken = default);
    Task<IEnumerable<T>> FindCachedAsync(Expression<Func<T, bool>> predicate, string cacheKey, TimeSpan? cacheExpiry = null, CancellationToken cancellationToken = default);

    // Raw SQL
    Task<IEnumerable<T>> FromSqlAsync(string sql, params object[] parameters);
    Task<int> ExecuteSqlAsync(string sql, params object[] parameters);
}

/// <summary>
/// Paged result container
/// </summary>
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

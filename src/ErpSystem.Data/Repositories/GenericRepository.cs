using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories
{
    public class GenericRepository<T> : IGenericRepository<T> where T : BaseEntity
    {
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public GenericRepository(ApplicationDbContext context)
        {
            _context = context;
            _dbSet = context.Set<T>();
        }

        public virtual async Task<T> GetByIdAsync(Guid id)
        {
            return await _dbSet.FirstOrDefaultAsync(e => e.Id == id && !e.IsDeleted);
        }

        public virtual async Task<T> GetByIdAsync(Guid id, params Expression<Func<T, object>>[] includes)
        {
            var query = _dbSet.Where(e => e.Id == id && !e.IsDeleted);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync()
        {
            return await _dbSet.Where(e => !e.IsDeleted).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetAllAsync(params Expression<Func<T, object>>[] includes)
        {
            var query = _dbSet.Where(e => !e.IsDeleted);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).Where(e => !e.IsDeleted).ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            var query = _dbSet.Where(predicate).Where(e => !e.IsDeleted);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.ToListAsync();
        }

        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).Where(e => !e.IsDeleted).FirstOrDefaultAsync();
        }

        public virtual async Task<T> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, params Expression<Func<T, object>>[] includes)
        {
            var query = _dbSet.Where(predicate).Where(e => !e.IsDeleted);
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).Where(e => !e.IsDeleted).AnyAsync();
        }

        public virtual async Task<int> CountAsync()
        {
            return await _dbSet.Where(e => !e.IsDeleted).CountAsync();
        }

        public virtual async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        {
            return await _dbSet.Where(predicate).Where(e => !e.IsDeleted).CountAsync();
        }

        // Pagination methods
        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize)
        {
            return await _dbSet
                .Where(e => !e.IsDeleted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync(int page, int pageSize, Expression<Func<T, bool>> predicate)
        {
            return await _dbSet
                .Where(predicate)
                .Where(e => !e.IsDeleted)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync<TKey>(int page, int pageSize, Expression<Func<T, TKey>> orderBy, bool descending = false)
        {
            var query = _dbSet.Where(e => !e.IsDeleted);

            if (descending)
            {
                query = query.OrderByDescending(orderBy);
            }
            else
            {
                query = query.OrderBy(orderBy);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public virtual async Task<IEnumerable<T>> GetPagedAsync<TKey>(int page, int pageSize, Expression<Func<T, bool>> predicate, Expression<Func<T, TKey>> orderBy, bool descending = false)
        {
            var query = _dbSet.Where(predicate).Where(e => !e.IsDeleted);

            if (descending)
            {
                query = query.OrderByDescending(orderBy);
            }
            else
            {
                query = query.OrderBy(orderBy);
            }

            return await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        // CRUD operations
        public virtual async Task<T> AddAsync(T entity)
        {
            entity.Id = Guid.NewGuid();
            entity.CreatedAt = DateTime.UtcNow;
            await _dbSet.AddAsync(entity);
            return entity;
        }
        public virtual async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
        {
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                entity.Id = Guid.NewGuid();
                entity.CreatedAt = DateTime.UtcNow;
            }
            await _dbSet.AddRangeAsync(entityList);
            return entityList;
        }

        public virtual Task UpdateAsync(T entity)
        {
            entity.UpdatedAt = DateTime.UtcNow;
            _dbSet.Update(entity);
            return Task.CompletedTask;
        }

        public virtual Task UpdateRangeAsync(IEnumerable<T> entities)
        {
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                entity.UpdatedAt = DateTime.UtcNow;
            }
            _dbSet.UpdateRange(entityList);
            return Task.CompletedTask;
        }

        public virtual async Task DeleteAsync(Guid id)
        {
            var entity = await GetByIdAsync(id);
            if (entity != null)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
                await UpdateAsync(entity);
            }
        }

        public virtual Task DeleteAsync(T entity)
        {
            entity.IsDeleted = true;
            entity.DeletedAt = DateTime.UtcNow;
            return UpdateAsync(entity);
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<T> entities)
        {
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                entity.IsDeleted = true;
                entity.DeletedAt = DateTime.UtcNow;
            }
            await UpdateRangeAsync(entityList);
        }

        public virtual async Task DeleteRangeAsync(Expression<Func<T, bool>> predicate)
        {
            var entities = await FindAsync(predicate);
            await DeleteRangeAsync(entities);
        }

        // Hard delete operations (permanent removal from database)
        public virtual async Task HardDeleteAsync(T entity)
        {
            // Use raw SQL to bypass EF tracking and soft delete interception
            var tableName = _context.Model.FindEntityType(typeof(T))?.GetTableName();
            await _context.Database.ExecuteSqlRawAsync($"DELETE FROM [{tableName}] WHERE Id = {{0}}", entity.Id);
        }

        public virtual async Task HardDeleteRangeAsync(IEnumerable<T> entities)
        {
            var entityList = entities.ToList();
            if (!entityList.Any())
            {
                return;
            }

            // Use raw SQL to bypass EF tracking and soft delete interception
            var tableName = _context.Model.FindEntityType(typeof(T))?.GetTableName();
            var ids = string.Join(",", entityList.Select(e => $"'{e.Id}'"));
            await _context.Database.ExecuteSqlRawAsync($"DELETE FROM [{tableName}] WHERE Id IN ({ids})");
        }


        // Advanced querying
        public virtual IQueryable<T> GetQueryable()
        {
            return _dbSet.Where(e => !e.IsDeleted);
        }

        public virtual IQueryable<T> GetQueryable(Expression<Func<T, bool>> predicate)
        {
            return _dbSet.Where(predicate).Where(e => !e.IsDeleted);
        }

        public virtual async Task<IEnumerable<TResult>> GetProjectedAsync<TResult>(Expression<Func<T, TResult>> projection)
        {
            return await _dbSet.Where(e => !e.IsDeleted).Select(projection).ToListAsync();
        }

        public virtual async Task<IEnumerable<TResult>> GetProjectedAsync<TResult>(Expression<Func<T, bool>> predicate, Expression<Func<T, TResult>> projection)
        {
            return await _dbSet.Where(predicate).Where(e => !e.IsDeleted).Select(projection).ToListAsync();
        }

        // Unit of work
        public virtual async Task<int> SaveChangesAsync()
        {
            return await _context.SaveChangesAsync();
        }
    }
}

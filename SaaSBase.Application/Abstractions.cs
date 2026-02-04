using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace SaaSBase.Application;

public interface IUnitOfWork
{
	Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
	IRepository<TEntity> Repository<TEntity>() where TEntity : class;
}

public interface IRepository<TEntity> where TEntity : class
{
	// Queryable access for complex queries
	System.Linq.IQueryable<TEntity> GetQueryable();

	Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
	Task<IEnumerable<TEntity>> FindManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
	Task<IEnumerable<TEntity>> FindManyAsync(Expression<Func<TEntity, bool>> predicate, int skip, int take, CancellationToken cancellationToken = default);
	Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);
	Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

	// Optimized methods for better performance
	Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> selector, CancellationToken cancellationToken = default);
	Task<TResult?> FindSelectAsync<TResult>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TResult>> selector, CancellationToken cancellationToken = default);
	Task<IEnumerable<TResult>> FindManySelectAsync<TResult>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TResult>> selector, CancellationToken cancellationToken = default);
	Task<int> UpdateManyAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TEntity>> updateExpression, CancellationToken cancellationToken = default);

	Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
	Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);
	void Update(TEntity entity);
	Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);
	void UpdateRange(IEnumerable<TEntity> entities);
	void Remove(TEntity entity);
	void RemoveRange(IEnumerable<TEntity> entities);
}



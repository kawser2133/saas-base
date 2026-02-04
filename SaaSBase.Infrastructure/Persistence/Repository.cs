using SaaSBase.Application;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SaaSBase.Infrastructure.Persistence;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
	private readonly AppDbContext _dbContext;
	private readonly DbSet<TEntity> _set;

	public Repository(AppDbContext dbContext)
	{
		_dbContext = dbContext;
		_set = _dbContext.Set<TEntity>();
	}

	public IQueryable<TEntity> GetQueryable()
	{
		return _set.AsQueryable();
	}

	public Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		return _set.FindAsync(new object?[] { id }, cancellationToken).AsTask();
	}

	public async Task<IEnumerable<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		return await _set.ToListAsync(cancellationToken);
	}

	public async Task<TEntity?> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await _set.FirstOrDefaultAsync(predicate, cancellationToken);
	}

	public async Task<IEnumerable<TEntity>> FindManyAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await _set.Where(predicate).ToListAsync(cancellationToken);
	}

	public async Task<IEnumerable<TEntity>> FindManyAsync(Expression<Func<TEntity, bool>> predicate, int skip, int take, CancellationToken cancellationToken = default)
	{
		return await _set.Where(predicate).Skip(skip).Take(take).ToListAsync(cancellationToken);
	}

	public async Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
	{
		if (predicate == null)
			return await _set.CountAsync(cancellationToken);
		return await _set.CountAsync(predicate, cancellationToken);
	}

	public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
	{
		return await _set.AnyAsync(predicate, cancellationToken);
	}

	// Optimized methods for better performance
	public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, object>> selector, CancellationToken cancellationToken = default)
	{
		return await _set.Where(predicate).Select(selector).AnyAsync(cancellationToken);
	}

	public async Task<TResult?> FindSelectAsync<TResult>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TResult>> selector, CancellationToken cancellationToken = default)
	{
		return await _set.Where(predicate).Select(selector).FirstOrDefaultAsync(cancellationToken);
	}

	public async Task<IEnumerable<TResult>> FindManySelectAsync<TResult>(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TResult>> selector, CancellationToken cancellationToken = default)
	{
		return await _set.Where(predicate).Select(selector).ToListAsync(cancellationToken);
	}

	public async Task<int> UpdateManyAsync(Expression<Func<TEntity, bool>> predicate, Expression<Func<TEntity, TEntity>> updateExpression, CancellationToken cancellationToken = default)
	{
		var entities = await _set.Where(predicate).ToListAsync(cancellationToken);
		foreach (var entity in entities)
		{
			var updatedEntity = updateExpression.Compile()(entity);
			_set.Update(updatedEntity);
		}
		return entities.Count;
	}

	public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
	{
		await _set.AddAsync(entity, cancellationToken);
	}

	public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
	{
		await _set.AddRangeAsync(entities, cancellationToken);
	}

	public void Update(TEntity entity)
	{
		_set.Update(entity);
	}

	public async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
	{
		_set.Update(entity);
		await Task.CompletedTask;
	}

	public void UpdateRange(IEnumerable<TEntity> entities)
	{
		_set.UpdateRange(entities);
	}

	public void Remove(TEntity entity)
	{
		_set.Remove(entity);
	}

	public void RemoveRange(IEnumerable<TEntity> entities)
	{
		_set.RemoveRange(entities);
	}
}

public class UnitOfWork : IUnitOfWork
{
	private readonly AppDbContext _dbContext;
	private readonly Dictionary<Type, object> _repositories = new();

	public UnitOfWork(AppDbContext dbContext)
	{
		_dbContext = dbContext;
	}

	public IRepository<TEntity> Repository<TEntity>() where TEntity : class
	{
		var type = typeof(TEntity);
		if (!_repositories.ContainsKey(type))
		{
			_repositories[type] = new Repository<TEntity>(_dbContext);
		}
		return (IRepository<TEntity>)_repositories[type];
	}

	public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
		=> _dbContext.SaveChangesAsync(cancellationToken);
}

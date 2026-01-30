using System.Linq.Expressions;

namespace NinjaDAM.Entity.IRepositories
{
    public interface IRepository<T> where T : class
    {

        IQueryable<T> Query();
        Task<IEnumerable<T>> GetAllAsync();
        Task<T?> GetByIdAsync(object id);
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
        Task AddAsync(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task SaveAsync();
        Task<T?> GetSingleAsync(Expression<Func<T, bool>> predicate);
    }
}

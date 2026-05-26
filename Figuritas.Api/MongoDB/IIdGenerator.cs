public interface IIdGenerator
{
    int GetNextId(string sequenceName);
    int GetNextId<T>();
}


public interface IDomain
{
	T? GetFirst<T>(bool includeSelf = false) where T : class;
	List<T> GetAll<T>(bool includeSelf = false, bool recursive = true) where T : class;
	void CollectPrepareAll(IDARAction action);
	void CollectAbortAll(IDARAction action);
	void CollectBeforeAll(IDARAction action);
	void CollectAfterAll(IDARAction action);
}
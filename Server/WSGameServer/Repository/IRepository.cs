using WSGameServer;

namespace WSGameServer;

public interface IRepository
{
    public long Key { get;  }
    
    public Task ExecuteAsync(DbConnection connection);
    public void Apply();
}
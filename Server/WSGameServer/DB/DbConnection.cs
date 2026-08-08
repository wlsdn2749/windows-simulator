using System.Data;
using Dapper;

namespace WSGameServer;

/// <summary>
/// DB 스레드에서 리포지토리에게 건네주는 쿼리 실행기. Dapper 호출 관례(AsList 등)를 한 곳에 모은다 —
/// 리포지토리는 커넥션이 아니라 이 타입의 메서드 4개만 안다. 트랜잭션이 필요해지면 여기에 얹는다.
/// </summary>
public sealed class DbConnection(IDbConnection conn)
{
    /// <summary>여러 행 조회. List로 바로 받는다.</summary>
    public async Task<List<T>> QueryAsync<T>(string sql, object? param = null)
        => (await conn.QueryAsync<T>(sql, param)).AsList();

    /// <summary>한 행 조회. 없으면 null.</summary>
    public Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
        => conn.QueryFirstOrDefaultAsync<T>(sql, param);

    /// <summary>INSERT/UPDATE/DELETE. 영향 행 수를 돌려준다.</summary>
    public Task<int> ExecuteAsync(string sql, object? param = null)
        => conn.ExecuteAsync(sql, param);

    /// <summary>단일 값 조회 (RETURNING 등).</summary>
    public Task<T> ExecuteScalarAsync<T>(string sql, object? param = null)
        => conn.ExecuteScalarAsync<T>(sql, param)!;
}

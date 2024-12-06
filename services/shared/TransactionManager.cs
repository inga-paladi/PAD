using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace shared;

public class TransactionManager<TDbContext> where TDbContext : DbContext, new()
{
    private static readonly Lazy<TransactionManager<TDbContext>> _instance = new(() => new TransactionManager<TDbContext>());
    private ConcurrentDictionary<string, (TDbContext, IDbContextTransaction)> _transactions = new();

    public static TransactionManager<TDbContext> Instance => _instance.Value;

    public bool HasTransaction(string transactionId)
    {
        NLog.LogManager.GetCurrentClassLogger().Info($"Check if transaction {transactionId} is present. current cound is {_transactions.Count}");
        return _transactions.ContainsKey(transactionId);
    }

    public bool StartTransaction(string transactionId)
    {
        NLog.LogManager.GetCurrentClassLogger().Info($"Start transaction {transactionId}.");
        var context = new TDbContext();
        var transaction = context.Database.BeginTransaction();
        return _transactions.TryAdd(transactionId, (context, transaction));
    }

    public IDbContextTransaction GetTransaction(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var content);
        return content.Item2;
    }

    public TDbContext GetDbContext(string transactionId)
    {
        _transactions.TryGetValue(transactionId, out var content);
        return content.Item1;
    }

    public bool CommitTransaction(string transactionId)
    {
        if (!_transactions.TryRemove(transactionId, out var content))
            return false;

        content.Item2.Commit();
        content.Item2.Dispose();
        return true;
    }

    public bool RollbackTransaction(string transactionId)
    {
        if (!_transactions.TryRemove(transactionId, out var content))
            return false;

        content.Item2.Rollback();
        content.Item2.Dispose();
        return true;
    }

    public bool DisposeTransaction(string transactionId)
    {
        return _transactions.TryRemove(transactionId, out var _);
    }
}
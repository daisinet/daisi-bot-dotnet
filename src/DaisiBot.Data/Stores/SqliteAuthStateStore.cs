using DaisiBot.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DaisiBot.Data.Stores;

public class SqliteAuthStateStore(IDbContextFactory<DaisiBotDbContext> dbFactory)
{
    public async Task<AuthState> LoadAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var state = await db.AuthStates.FindAsync(1);
        return state ?? new AuthState();
    }

    public async Task SaveAsync(AuthState state)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        state.Id = 1;
        var existing = await db.AuthStates.FindAsync(1);
        if (existing is null)
        {
            db.AuthStates.Add(state);
        }
        else
        {
            db.Entry(existing).CurrentValues.SetValues(state);
        }
        await db.SaveChangesAsync();
    }

    public async Task ClearAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.AuthStates.FindAsync(1);
        if (existing is not null)
        {
            db.AuthStates.Remove(existing);
            await db.SaveChangesAsync();
        }
    }
}

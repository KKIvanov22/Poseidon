using Microsoft.EntityFrameworkCore;

namespace Poseidon.Server.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}

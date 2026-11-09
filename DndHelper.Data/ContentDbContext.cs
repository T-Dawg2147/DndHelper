using DndHelper.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DndHelper.Data;

public class ContentDbContext(DbContextOptions<ContentDbContext> options) : DbContext(options)
{
    public DbSet<CreatureEntity> Creatures => Set<CreatureEntity>();
    public DbSet<SpellEntity> Spells => Set<SpellEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CreatureEntity>(e =>
        {
            e.HasKey(x => x.Key);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Type);
        });

        modelBuilder.Entity<SpellEntity>(e =>
        {
            e.HasKey(x => x.Key);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Level);
        });
    }
}
using Microsoft.EntityFrameworkCore;

namespace Radar.Data;
public class RadarDbContext(DbContextOptions<RadarDbContext> options) :
 DbContext(options)
{
    public DbSet<StopEvent> StopEvents => Set<StopEvent>();

    public DbSet<Line> Lines => Set<Line>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StopEvent>(entity =>
        {
            entity.HasKey(e => new { e.OperatingDate, e.JourneyRef, e.StopOrder });

        });

        modelBuilder.Entity<Line>(entity =>
        {
            entity.HasKey(e => e.LineRef);
        });

    }
}
using Microsoft.EntityFrameworkCore;

namespace TextReport.Infrastructure.Persistence;

internal sealed class TextReportDbContext(DbContextOptions<TextReportDbContext> options)
    : DbContext(options)
{
}

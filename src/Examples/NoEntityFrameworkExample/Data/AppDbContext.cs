using NoEntityFrameworkExample.Models;
using Microsoft.EntityFrameworkCore;

namespace NoEntityFrameworkExample.Data
{
    public sealed class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        { }

        public DbSet<TodoItem> TodoItems { get; set; }
    }
}

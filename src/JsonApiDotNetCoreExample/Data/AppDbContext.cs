using JsonApiDotNetCoreExample.Models;
using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCoreExample.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TodoItem>()
                .HasOne(t => t.Assignee)
                .WithMany(p => p.AssignedTodoItems)
                .HasForeignKey(t => t.AssigneeId);
            
            modelBuilder.Entity<TodoItem>()
                .HasOne(t => t.Owner)
                .WithMany(p => p.TodoItems)
                .HasForeignKey(t => t.OwnerId);
        }

        public DbSet<TodoItem> TodoItems { get; set; }
        public DbSet<Person> People { get; set; }
        public DbSet<TodoItemCollection> TodoItemCollections { get; set; }
    }
}

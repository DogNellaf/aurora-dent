using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;

namespace DentalClinic
{
    public class DatabaseContext : IdentityDbContext<Profile, Role, long>
    {
        public DbSet<Staff> Staffs { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Profile> Profiles { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdentityUserLogin<long>>(b => b.HasKey(x => new { x.LoginProvider, x.ProviderKey }));
            modelBuilder.Entity<IdentityUserRole<long>>(b => b.HasKey(x => new { x.UserId, x.RoleId }));
            modelBuilder.Entity<IdentityUserToken<long>>(b => b.HasKey(x => new { x.UserId, x.LoginProvider, x.Name }));
            modelBuilder.Entity<IdentityRoleClaim<long>>(b => b.HasKey(x => x.Id));
            modelBuilder.Entity<IdentityUserClaim<long>>(b => b.HasKey(x => x.Id));

            modelBuilder.Entity<Service>().HasMany(e => e.Staff).WithMany(e => e.Services);
            modelBuilder.Entity<Service>().HasMany(e => e.Appointments).WithMany(e => e.Services);

            // The four application roles are part of the schema, so a fresh database works out of the box.
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = RoleIds.Client, Name = "Клиент", NormalizedName = "КЛИЕНТ", Title = "Клиент", ConcurrencyStamp = "role-1" },
                new Role { Id = RoleIds.Admin, Name = "Администратор", NormalizedName = "АДМИНИСТРАТОР", Title = "Администратор", ConcurrencyStamp = "role-2" },
                new Role { Id = RoleIds.Manager, Name = "Менеджер", NormalizedName = "МЕНЕДЖЕР", Title = "Менеджер", ConcurrencyStamp = "role-3" },
                new Role { Id = RoleIds.Doctor, Name = "Доктор", NormalizedName = "ДОКТОР", Title = "Доктор", ConcurrencyStamp = "role-4" });
        }
    }
}

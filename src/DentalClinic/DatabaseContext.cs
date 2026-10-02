using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using DentalClinic.Models;

namespace DentalClinic
{
    public class DatabaseContext : IdentityDbContext<Profile, Role, long>, IDataProtectionKeyContext
    {
        public DbSet<Staff> Staffs { get; set; } = null!;
        public DbSet<Appointment> Appointments { get; set; } = null!;
        public DbSet<Profile> Profiles { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Service> Services { get; set; } = null!;

        /// <summary>Keys that protect cookies and password reset tokens, kept in the database so they survive restarts.</summary>
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

        public DatabaseContext(DbContextOptions<DatabaseContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdentityUserLogin<long>>(b => b.HasKey(x => new { x.LoginProvider, x.ProviderKey }));
            modelBuilder.Entity<IdentityUserRole<long>>(b => b.HasKey(x => new { x.UserId, x.RoleId }));
            modelBuilder.Entity<IdentityUserToken<long>>(b => b.HasKey(x => new { x.UserId, x.LoginProvider, x.Name }));
            modelBuilder.Entity<IdentityRoleClaim<long>>(b => b.HasKey(x => x.Id));
            modelBuilder.Entity<IdentityUserClaim<long>>(b => b.HasKey(x => x.Id));

            // A profile has exactly one application role (Profile.RoleId). The ASP.NET Identity user-role
            // table stays empty, authorization goes through the [RoleRequired] filter instead.
            modelBuilder.Entity<Profile>().HasOne<Role>().WithMany().HasForeignKey(p => p.RoleId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Service>(e =>
            {
                e.Property(s => s.Price).HasPrecision(18, 2);
                e.HasMany(s => s.Staff).WithMany(s => s.Services);
                e.HasMany(s => s.Appointments).WithMany(a => a.Services);
            });

            modelBuilder.Entity<Staff>(e =>
            {
                e.HasOne(s => s.Profile).WithMany().HasForeignKey(s => s.ProfileId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(s => s.ProfileId).IsUnique();
            });

            modelBuilder.Entity<Appointment>(e =>
            {
                e.HasOne(a => a.Staff).WithMany().HasForeignKey(a => a.StaffId).OnDelete(DeleteBehavior.Cascade);

                // NO ACTION on purpose: SQL Server rejects a second cascading path (Profile -> Staff -> Appointment).
                // ProfileService frees or anonymises a patient's visits before deleting the profile.
                e.HasOne(a => a.Client).WithMany().HasForeignKey(a => a.ClientId).OnDelete(DeleteBehavior.NoAction);

                // A doctor cannot have two appointments that start at the same moment.
                e.HasIndex(a => new { a.StaffId, a.StartAt }).IsUnique();
                e.HasIndex(a => a.ClientId);
                e.HasIndex(a => a.StartAt);
            });

            // One review per patient, removed together with the profile.
            modelBuilder.Entity<Review>(e =>
            {
                e.HasOne<Profile>().WithMany().HasForeignKey(r => r.ProfileId).OnDelete(DeleteBehavior.Cascade);
                e.HasIndex(r => r.ProfileId).IsUnique();
            });

            // The four application roles are part of the schema, so a fresh database works out of the box.
            modelBuilder.Entity<Role>().HasData(
                new Role { Id = RoleIds.Client, Name = "Клиент", NormalizedName = "КЛИЕНТ", Title = "Клиент", ConcurrencyStamp = "role-1" },
                new Role { Id = RoleIds.Admin, Name = "Администратор", NormalizedName = "АДМИНИСТРАТОР", Title = "Администратор", ConcurrencyStamp = "role-2" },
                new Role { Id = RoleIds.Manager, Name = "Менеджер", NormalizedName = "МЕНЕДЖЕР", Title = "Менеджер", ConcurrencyStamp = "role-3" },
                new Role { Id = RoleIds.Doctor, Name = "Доктор", NormalizedName = "ДОКТОР", Title = "Доктор", ConcurrencyStamp = "role-4" });
        }
    }
}

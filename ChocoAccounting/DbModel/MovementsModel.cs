/*
    This file is part of Choco Accounting.

    Choco Accounting is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Choco Accounting is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Choco Accounting. If not, see <http://www.gnu.org/licenses/>.
*/
namespace ChocoAccounting.DbModel
{
    using System.Data.Entity;

    public partial class MovementsModel : DbContext
    {
        public MovementsModel(string connectionString)
            : base(connectionString)
        {
        }

        public virtual DbSet<Account> AccountSet { get; set; }
        public virtual DbSet<Entity> EntitySet { get; set; }
        public virtual DbSet<MovementCategory> MovementCategorySet { get; set; }
        public virtual DbSet<Movement> MovementSet { get; set; }
        public virtual DbSet<MovementSubcategory> MovementSubcategorySet { get; set; }
        public virtual DbSet<MovementType> MovementTypeSet { get; set; }
        public virtual DbSet<Person> PersonSet { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>()
                .HasMany(e => e.MovementSet)
                .WithRequired(e => e.Account)
                .HasForeignKey(e => e.AccountId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Entity>()
                .HasMany(e => e.MovementSet)
                .WithOptional(e => e.Entity)
                .HasForeignKey(e => e.EntityId);

            modelBuilder.Entity<MovementCategory>()
                .Property(e => e.TaxPercentage)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MovementCategory>()
                .HasMany(e => e.MovementSet)
                .WithRequired(e => e.MovementCategory)
                .HasForeignKey(e => e.MovementCategoryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MovementCategory>()
                .HasMany(e => e.MovementSubcategorySet)
                .WithRequired(e => e.MovementCategory)
                .HasForeignKey(e => e.MovementCategoryId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Movement>()
                .Property(e => e.Quantity)
                .HasPrecision(18, 2);

            modelBuilder.Entity<MovementSubcategory>()
                .HasMany(e => e.MovementSet)
                .WithOptional(e => e.MovementSubcategory)
                .HasForeignKey(e => e.MovementSubcategoryId);

            modelBuilder.Entity<MovementType>()
                .HasMany(e => e.MovementSet)
                .WithRequired(e => e.MovementType)
                .HasForeignKey(e => e.MovementTypeId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Person>()
                .HasMany(e => e.MovementSet)
                .WithOptional(e => e.Person)
                .HasForeignKey(e => e.PersonId);
        }
    }
}

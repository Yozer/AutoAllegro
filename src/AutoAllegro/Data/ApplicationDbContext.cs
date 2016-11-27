using System;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AutoAllegro.Models;

namespace AutoAllegro.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public DbSet<Auction> Auctions { get; set; }
        public DbSet<Buyer> Buyers { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<GameCode> GameCodes { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<ShippingAddress> SendAddresses { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Auction>().HasIndex(t => t.AllegroAuctionId).IsUnique();
            builder.Entity<Buyer>().HasIndex(t => t.AllegroUserId).IsUnique();
            builder.Entity<Event>().HasIndex(t => t.AllegroEventId).IsUnique();
            builder.Entity<Order>().HasIndex(t => t.AllegroDealId).IsUnique();
            builder.Entity<Transaction>().HasIndex(t => t.AllegroTransactionId).IsUnique();
        }
    }
}

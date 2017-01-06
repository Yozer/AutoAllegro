using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using AutoAllegro.Data;
using AutoAllegro.Models;

namespace AutoAllegro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20170106155752_initial")]
    partial class initial
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.0-rtm-22752");

            modelBuilder.Entity("AutoAllegro.Models.AllegroRefundReason", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Reason");

                    b.HasKey("Id");

                    b.ToTable("AllegroRefundReasons");
                });

            modelBuilder.Entity("AutoAllegro.Models.Auction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("AllegroAuctionId");

                    b.Property<int>("Converter");

                    b.Property<DateTime>("CreationDate");

                    b.Property<DateTime>("EndDate");

                    b.Property<decimal>("Fee");

                    b.Property<bool>("IsMonitored");

                    b.Property<bool>("IsVirtualItem");

                    b.Property<decimal>("OpenCost");

                    b.Property<decimal>("PricePerItem");

                    b.Property<string>("Title");

                    b.Property<string>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("AllegroAuctionId")
                        .IsUnique();

                    b.HasIndex("UserId");

                    b.ToTable("Auctions");
                });

            modelBuilder.Entity("AutoAllegro.Models.Buyer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Address");

                    b.Property<long>("AllegroUserId");

                    b.Property<string>("City");

                    b.Property<string>("Email");

                    b.Property<string>("FirstName");

                    b.Property<string>("LastName");

                    b.Property<string>("Phone");

                    b.Property<string>("Phone2");

                    b.Property<string>("PostCode");

                    b.Property<string>("UserLogin");

                    b.HasKey("Id");

                    b.HasIndex("AllegroUserId")
                        .IsUnique();

                    b.ToTable("Buyers");
                });

            modelBuilder.Entity("AutoAllegro.Models.Event", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("AllegroEventId");

                    b.Property<DateTime>("EventTime");

                    b.Property<int>("EventType");

                    b.Property<int>("OrderId");

                    b.HasKey("Id");

                    b.HasIndex("AllegroEventId")
                        .IsUnique();

                    b.HasIndex("OrderId");

                    b.ToTable("Events");
                });

            modelBuilder.Entity("AutoAllegro.Models.GameCode", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("AddDate");

                    b.Property<int>("AuctionId");

                    b.Property<string>("Code");

                    b.Property<int?>("OrderId");

                    b.HasKey("Id");

                    b.HasIndex("AuctionId");

                    b.HasIndex("OrderId");

                    b.ToTable("GameCodes");
                });

            modelBuilder.Entity("AutoAllegro.Models.Order", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("AllegroDealId");

                    b.Property<int?>("AllegroRefundId");

                    b.Property<int>("AuctionId");

                    b.Property<int>("BuyerId");

                    b.Property<DateTime>("OrderDate");

                    b.Property<int>("OrderStatus");

                    b.Property<int>("Quantity");

                    b.Property<int?>("ShippingAddressId");

                    b.HasKey("Id");

                    b.HasIndex("AllegroDealId")
                        .IsUnique();

                    b.HasIndex("AuctionId");

                    b.HasIndex("BuyerId");

                    b.HasIndex("ShippingAddressId")
                        .IsUnique();

                    b.ToTable("Orders");
                });

            modelBuilder.Entity("AutoAllegro.Models.ShippingAddress", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("Address");

                    b.Property<string>("City");

                    b.Property<string>("FirstName");

                    b.Property<string>("LastName");

                    b.Property<string>("MessageToSeller");

                    b.Property<string>("PostCode");

                    b.HasKey("Id");

                    b.ToTable("SendAddresses");
                });

            modelBuilder.Entity("AutoAllegro.Models.Transaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<long>("AllegroTransactionId");

                    b.Property<decimal>("Amount");

                    b.Property<int>("OrderId");

                    b.Property<int>("TransactionStatus");

                    b.HasKey("Id");

                    b.HasIndex("AllegroTransactionId")
                        .IsUnique();

                    b.HasIndex("OrderId");

                    b.ToTable("Transactions");
                });

            modelBuilder.Entity("AutoAllegro.Models.User", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<int>("AccessFailedCount");

                    b.Property<string>("AllegroHashedPass");

                    b.Property<long>("AllegroJournalStart");

                    b.Property<string>("AllegroKey");

                    b.Property<string>("AllegroUserName");

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Email")
                        .HasMaxLength(256);

                    b.Property<bool>("EmailConfirmed");

                    b.Property<bool>("LockoutEnabled");

                    b.Property<DateTimeOffset?>("LockoutEnd");

                    b.Property<string>("NormalizedEmail")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedUserName")
                        .HasMaxLength(256);

                    b.Property<string>("PasswordHash");

                    b.Property<string>("PhoneNumber");

                    b.Property<bool>("PhoneNumberConfirmed");

                    b.Property<string>("SecurityStamp");

                    b.Property<bool>("TwoFactorEnabled");

                    b.Property<string>("UserName")
                        .HasMaxLength(256);

                    b.Property<int?>("VirtualItemSettingsId");

                    b.HasKey("Id");

                    b.HasIndex("NormalizedEmail")
                        .HasName("EmailIndex");

                    b.HasIndex("NormalizedUserName")
                        .IsUnique()
                        .HasName("UserNameIndex");

                    b.HasIndex("VirtualItemSettingsId");

                    b.ToTable("AspNetUsers");
                });

            modelBuilder.Entity("AutoAllegro.Models.VirtualItemSettings", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("DisplayName");

                    b.Property<string>("MessageSubject");

                    b.Property<string>("MessageTemplate");

                    b.Property<string>("ReplyTo");

                    b.HasKey("Id");

                    b.ToTable("VirtualItemSettings");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRole", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ConcurrencyStamp")
                        .IsConcurrencyToken();

                    b.Property<string>("Name")
                        .HasMaxLength(256);

                    b.Property<string>("NormalizedName")
                        .HasMaxLength(256);

                    b.HasKey("Id");

                    b.HasIndex("NormalizedName")
                        .IsUnique()
                        .HasName("RoleNameIndex");

                    b.ToTable("AspNetRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRoleClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("RoleId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetRoleClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserClaim<string>", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("ClaimType");

                    b.Property<string>("ClaimValue");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserClaims");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserLogin<string>", b =>
                {
                    b.Property<string>("LoginProvider");

                    b.Property<string>("ProviderKey");

                    b.Property<string>("ProviderDisplayName");

                    b.Property<string>("UserId")
                        .IsRequired();

                    b.HasKey("LoginProvider", "ProviderKey");

                    b.HasIndex("UserId");

                    b.ToTable("AspNetUserLogins");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserRole<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("RoleId");

                    b.HasKey("UserId", "RoleId");

                    b.HasIndex("RoleId");

                    b.ToTable("AspNetUserRoles");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserToken<string>", b =>
                {
                    b.Property<string>("UserId");

                    b.Property<string>("LoginProvider");

                    b.Property<string>("Name");

                    b.Property<string>("Value");

                    b.HasKey("UserId", "LoginProvider", "Name");

                    b.ToTable("AspNetUserTokens");
                });

            modelBuilder.Entity("AutoAllegro.Models.Auction", b =>
                {
                    b.HasOne("AutoAllegro.Models.User", "User")
                        .WithMany("Auctions")
                        .HasForeignKey("UserId");
                });

            modelBuilder.Entity("AutoAllegro.Models.Event", b =>
                {
                    b.HasOne("AutoAllegro.Models.Order", "Order")
                        .WithMany("Events")
                        .HasForeignKey("OrderId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("AutoAllegro.Models.GameCode", b =>
                {
                    b.HasOne("AutoAllegro.Models.Auction", "Auction")
                        .WithMany("GameCodes")
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AutoAllegro.Models.Order", "Order")
                        .WithMany("GameCodes")
                        .HasForeignKey("OrderId");
                });

            modelBuilder.Entity("AutoAllegro.Models.Order", b =>
                {
                    b.HasOne("AutoAllegro.Models.Auction", "Auction")
                        .WithMany("Orders")
                        .HasForeignKey("AuctionId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AutoAllegro.Models.Buyer", "Buyer")
                        .WithMany("Orders")
                        .HasForeignKey("BuyerId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AutoAllegro.Models.ShippingAddress", "ShippingAddress")
                        .WithOne("Order")
                        .HasForeignKey("AutoAllegro.Models.Order", "ShippingAddressId");
                });

            modelBuilder.Entity("AutoAllegro.Models.Transaction", b =>
                {
                    b.HasOne("AutoAllegro.Models.Order", "Order")
                        .WithMany("Transactions")
                        .HasForeignKey("OrderId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("AutoAllegro.Models.User", b =>
                {
                    b.HasOne("AutoAllegro.Models.VirtualItemSettings", "VirtualItemSettings")
                        .WithMany()
                        .HasForeignKey("VirtualItemSettingsId");
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRoleClaim<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRole")
                        .WithMany("Claims")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserClaim<string>", b =>
                {
                    b.HasOne("AutoAllegro.Models.User")
                        .WithMany("Claims")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserLogin<string>", b =>
                {
                    b.HasOne("AutoAllegro.Models.User")
                        .WithMany("Logins")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });

            modelBuilder.Entity("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityUserRole<string>", b =>
                {
                    b.HasOne("Microsoft.AspNetCore.Identity.EntityFrameworkCore.IdentityRole")
                        .WithMany("Users")
                        .HasForeignKey("RoleId")
                        .OnDelete(DeleteBehavior.Cascade);

                    b.HasOne("AutoAllegro.Models.User")
                        .WithMany("Roles")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
        }
    }
}

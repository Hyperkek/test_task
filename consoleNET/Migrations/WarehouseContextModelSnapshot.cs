﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace consoleNET.Migrations
{
    [DbContext(typeof(WarehouseContext))]
    partial class WarehouseContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.6");

            modelBuilder.Entity("Box", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Depth")
                        .HasColumnType("INTEGER");

                    b.Property<DateOnly>("ExpireDate")
                        .HasColumnType("TEXT");

                    b.Property<uint>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<uint?>("PalletId")
                        .HasColumnType("INTEGER");

                    b.Property<DateOnly?>("ProductionDate")
                        .HasColumnType("TEXT");

                    b.Property<uint>("Weight")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PalletId");

                    b.ToTable("Boxes");
                });

            modelBuilder.Entity("Pallet", b =>
                {
                    b.Property<uint>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Depth")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Height")
                        .HasColumnType("INTEGER");

                    b.Property<uint>("Width")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("Pallets");
                });

            modelBuilder.Entity("Box", b =>
                {
                    b.HasOne("Pallet", "Pallet")
                        .WithMany("Boxes")
                        .HasForeignKey("PalletId");

                    b.Navigation("Pallet");
                });

            modelBuilder.Entity("Pallet", b =>
                {
                    b.Navigation("Boxes");
                });
#pragma warning restore 612, 618
        }
    }
}

﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using api.Database;

#nullable disable

namespace api.Migrations
{
    [DbContext(typeof(IdaDbContext))]
    [Migration("20241127101552_InitialCreate")]
    partial class InitialCreate
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "8.0.8")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("api.Database.Analysis", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("InspectionDataId")
                        .HasColumnType("text");

                    b.Property<int>("Status")
                        .HasColumnType("integer");

                    b.Property<int?>("Type")
                        .HasColumnType("integer");

                    b.Property<string>("Uri")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.HasIndex("InspectionDataId");

                    b.ToTable("Analysis");
                });

            modelBuilder.Entity("api.Database.InspectionData", b =>
                {
                    b.Property<string>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("text");

                    b.Property<int[]>("AnalysisToBeRun")
                        .IsRequired()
                        .HasColumnType("integer[]");

                    b.Property<int>("AnonymizerWorkflowStatus")
                        .HasColumnType("integer");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("InspectionId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("InstallationCode")
                        .IsRequired()
                        .HasColumnType("text");

                    b.HasKey("Id");

                    b.ToTable("InspectionData");
                });

            modelBuilder.Entity("api.Database.Analysis", b =>
                {
                    b.HasOne("api.Database.InspectionData", null)
                        .WithMany("Analysis")
                        .HasForeignKey("InspectionDataId");
                });

            modelBuilder.Entity("api.Database.InspectionData", b =>
                {
                    b.OwnsOne("api.Database.BlobStorageLocation", "AnonymizedBlobStorageLocation", b1 =>
                        {
                            b1.Property<string>("InspectionDataId")
                                .HasColumnType("text");

                            b1.Property<string>("BlobContainer")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<string>("BlobName")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<string>("StorageAccount")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.HasKey("InspectionDataId");

                            b1.ToTable("InspectionData");

                            b1.WithOwner()
                                .HasForeignKey("InspectionDataId");
                        });

                    b.OwnsOne("api.Database.BlobStorageLocation", "RawDataBlobStorageLocation", b1 =>
                        {
                            b1.Property<string>("InspectionDataId")
                                .HasColumnType("text");

                            b1.Property<string>("BlobContainer")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<string>("BlobName")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.Property<string>("StorageAccount")
                                .IsRequired()
                                .HasColumnType("text");

                            b1.HasKey("InspectionDataId");

                            b1.ToTable("InspectionData");

                            b1.WithOwner()
                                .HasForeignKey("InspectionDataId");
                        });

                    b.Navigation("AnonymizedBlobStorageLocation")
                        .IsRequired();

                    b.Navigation("RawDataBlobStorageLocation")
                        .IsRequired();
                });

            modelBuilder.Entity("api.Database.InspectionData", b =>
                {
                    b.Navigation("Analysis");
                });
#pragma warning restore 612, 618
        }
    }
}

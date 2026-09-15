using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestShieldAI.Api.Persistence;

#nullable disable

namespace TestShieldAI.Api.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260914153000_AddProjectBaselinesAndTestRuns")]
partial class AddProjectBaselinesAndTestRuns
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        AddProjectBaselinesAndTestRunsModel.Build(modelBuilder);
    }
}

internal static class AddProjectBaselinesAndTestRunsModel
{
    public static void Build(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "8.0.20");

        modelBuilder.Entity("TestShieldAI.Api.Persistence.OpenApiSpecificationRecord", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("ImportedAt")
                .HasColumnType("TEXT");

            b.Property<Guid>("ProjectId")
                .HasColumnType("TEXT");

            b.Property<string>("RawSpecification")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ProjectId");

            b.ToTable("OpenApiSpecifications");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectBaselineRecord", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("EstablishedAt")
                .HasColumnType("TEXT");

            b.Property<Guid>("ProjectId")
                .HasColumnType("TEXT");

            b.Property<string>("SnapshotJson")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<Guid?>("SpecificationId")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ProjectId")
                .IsUnique();

            b.ToTable("ProjectBaselines");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectRecord", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<string>("BaseUrl")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Name")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("Projects");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectTestRunRecord", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<DateTimeOffset>("CompletedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Decision")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<int>("ErrorCount")
                .HasColumnType("INTEGER");

            b.Property<int>("FailedCount")
                .HasColumnType("INTEGER");

            b.Property<int>("FindingCount")
                .HasColumnType("INTEGER");

            b.Property<int>("PassedCount")
                .HasColumnType("INTEGER");

            b.Property<Guid>("ProjectId")
                .HasColumnType("TEXT");

            b.Property<string>("ResultsJson")
                .IsRequired()
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ProjectId")
                .IsUnique();

            b.ToTable("ProjectTestRuns");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.OpenApiSpecificationRecord", b =>
        {
            b.HasOne("TestShieldAI.Api.Persistence.ProjectRecord", "Project")
                .WithMany("Specifications")
                .HasForeignKey("ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectBaselineRecord", b =>
        {
            b.HasOne("TestShieldAI.Api.Persistence.ProjectRecord", "Project")
                .WithOne("Baseline")
                .HasForeignKey("TestShieldAI.Api.Persistence.ProjectBaselineRecord", "ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectTestRunRecord", b =>
        {
            b.HasOne("TestShieldAI.Api.Persistence.ProjectRecord", "Project")
                .WithOne("LastRun")
                .HasForeignKey("TestShieldAI.Api.Persistence.ProjectTestRunRecord", "ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectRecord", b =>
        {
            b.Navigation("Baseline");

            b.Navigation("LastRun");

            b.Navigation("Specifications");
        });
#pragma warning restore 612, 618
    }
}

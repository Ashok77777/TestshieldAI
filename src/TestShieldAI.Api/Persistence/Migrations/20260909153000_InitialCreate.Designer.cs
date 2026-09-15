using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TestShieldAI.Api.Persistence;

#nullable disable

namespace TestShieldAI.Api.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260909153000_InitialCreate")]
partial class InitialCreate
{
    /// <inheritdoc />
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        InitialCreateModel.Build(modelBuilder);
    }
}

internal static class InitialCreateModel
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

        modelBuilder.Entity("TestShieldAI.Api.Persistence.OpenApiSpecificationRecord", b =>
        {
            b.HasOne("TestShieldAI.Api.Persistence.ProjectRecord", "Project")
                .WithMany("Specifications")
                .HasForeignKey("ProjectId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("Project");
        });

        modelBuilder.Entity("TestShieldAI.Api.Persistence.ProjectRecord", b =>
        {
            b.Navigation("Specifications");
        });
#pragma warning restore 612, 618
    }
}

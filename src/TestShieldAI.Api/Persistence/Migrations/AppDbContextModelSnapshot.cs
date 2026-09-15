using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using TestShieldAI.Api.Persistence;

#nullable disable

namespace TestShieldAI.Api.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
partial class AppDbContextModelSnapshot : ModelSnapshot
{
    /// <inheritdoc />
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        AddProjectBaselinesAndTestRunsModel.Build(modelBuilder);
    }
}

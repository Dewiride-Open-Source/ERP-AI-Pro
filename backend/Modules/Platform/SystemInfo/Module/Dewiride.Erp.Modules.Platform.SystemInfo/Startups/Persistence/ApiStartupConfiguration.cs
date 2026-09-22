using Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dewiride.Erp.Modules.Platform.SystemInfo.Startups.Persistence;

internal sealed class ApiStartupConfiguration : IEntityTypeConfiguration<ApiStartup>
{
    public void Configure(EntityTypeBuilder<ApiStartup> builder)
    {
        builder.ToTable("Startups");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.ApplicationName).HasMaxLength(ApiStartup.ApplicationNameMaxLength);
        builder.ComplexProperty(s => s.Build, build =>
        {
            build.Property(b => b.Version).HasMaxLength(ApiStartup.VersionMaxLength);
            build.Property(b => b.Framework).HasMaxLength(ApiStartup.FrameworkMaxLength);
        });
        builder.Property(s => s.EnvironmentName).HasMaxLength(ApiStartup.EnvironmentNameMaxLength);
        builder.Property(s => s.ConfigurationLabel).HasMaxLength(ApiStartup.ConfigurationLabelMaxLength);
        builder.Property(s => s.MachineName).HasMaxLength(ApiStartup.MachineNameMaxLength);
        builder.HasIndex(s => s.StartedAt).IsDescending();
    }
}

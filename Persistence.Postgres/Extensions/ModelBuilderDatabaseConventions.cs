using Anvil.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Anvil.Persistence.Postgres.Extensions;

public static class ModelBuilderDatabaseConventions
{
    public static ModelBuilder ApplyDatabaseManagedConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(IEntity).IsAssignableFrom(entityType.ClrType))
            {
                var primaryKey = entityType.FindPrimaryKey();
                if (primaryKey is not null)
                {
                    foreach (var keyProperty in primaryKey.Properties)
                    {
                        keyProperty.ValueGenerated = ValueGenerated.OnAdd;
                    }
                }

                var createdProperty = entityType.FindProperty(nameof(IEntity.Created));
                if (createdProperty is not null)
                {
                    createdProperty.ValueGenerated = ValueGenerated.OnAdd;
                    createdProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                    createdProperty.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
                }

                var updatedProperty = entityType.FindProperty(nameof(IEntity.Updated));
                if (updatedProperty is not null)
                {
                    updatedProperty.ValueGenerated = ValueGenerated.OnAddOrUpdate;
                    updatedProperty.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
                    updatedProperty.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
                }
            }
        }

        return modelBuilder;
    }
}

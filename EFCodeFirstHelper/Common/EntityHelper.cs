using System;
using System.Data.Entity;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Linq;

namespace EFCodeFirstHelper.Common {

    /// <summary>
    /// A helper class to get tables, columns and relationships information of Entity Framework models.
    /// References: 
    /// http://romiller.com/tag/metadataworkspace/
    /// http://www.codeproject.com/Tips/890432/Entity-Framework-DiagnosticsContext-Get-Detailed
    /// 
    /// </summary>
    public class EntityHelper {
        private readonly DbContext _context;
        private readonly MetadataWorkspace _metadata;

        public EntityHelper(DbContext context) {
            _context = context;
            _metadata = ((IObjectContextAdapter)_context).ObjectContext.MetadataWorkspace;
        }

        /// <summary>
        /// Get all entity model types in the context.
        /// </summary>
        /// <returns></returns>
        public Type[] GetAllModels() {
            var asm = _context.GetType().AssemblyQualifiedName;
            if (asm == null)
                throw new Exception("Could not resolve the assembly qualified name of database context.");

            asm = asm.Remove(0, asm.IndexOf(",", StringComparison.Ordinal));
            return _metadata.GetItemCollection(DataSpace.OSpace).GetItems<EntityType>()
                .Select(x => Type.GetType(x.FullName + asm))
                .ToArray();
        }

        /// <summary>
        /// Get all primary keys of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        public string[] GetPKs(Type type) {
            return GetEntityMetadata(type).KeyProperties.Select(p => GetColumnName(type, p.Name)).ToArray();
        }

        /// <summary>
        /// Get all column names of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        public string[] GetColums(Type type) {
            return GetEntityMetadata(type).Properties.Select(p => GetColumnName(type, p.Name)).ToArray();
        }

        /// <summary>
        /// Get all foreign key column names of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        public string[] GetFKs(Type type) {
            
            // Find associations related to the entity type
            var associationTypes = _metadata.GetItems<AssociationType>(DataSpace.CSpace)
                .Where(x => x.IsForeignKey && x.Constraint.ToProperties.Any(c => c.DeclaringType.Name == type.Name));

            // Return the column name of the associated target properties
            return associationTypes
                .SelectMany(x => x.Constraint.ToProperties).Select(x => GetColumnName(type, x.Name))
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Get table name of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        public string GetTableName(Type type) {
            var mapping = GetEntitySetMapping(type);
            // Find the storage entity set (table) that the entity is mapped
            var tableEntitySet = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .StoreEntitySet;

            // Return the table name from the storage entity set
            return (string)tableEntitySet.MetadataProperties["Table"].Value ?? tableEntitySet.Name;
        }

        /// <summary>
        /// Get the entity type of the model where you can get the specific properties of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        private EntityType GetEntityMetadata(Type type) {
            // Get the mapping between CLR types and metadata OSpace
            var objectItemCollection = ((ObjectItemCollection) _metadata.GetItemCollection(DataSpace.OSpace));

            // Get metadata for given CLR type
            var entityMetadata = _metadata
                .GetItems<EntityType>(DataSpace.OSpace)
                .Single(e => objectItemCollection.GetClrType(e) == type);
            return entityMetadata;
        }

        /// <summary>
        /// Get column name of the property of the model.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <param name="propertyName">The model's property name</param>
        /// <returns></returns>
        private string GetColumnName(Type type, string propertyName) {
            var mapping = GetEntitySetMapping(type);

            // Find the storage property (column) that the property is mapped
            var columnName = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .PropertyMappings
                .OfType<ScalarPropertyMapping>()
                .Single(m => m.Property.Name == propertyName)
                .Column
                .Name;

            return columnName;
        }

        /// <summary>
        /// Get entity set mapping information of the model. 
        /// This is where you can get the real table name and column name applied in the database.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <returns></returns>
        private EntitySetMapping GetEntitySetMapping(Type type) {
            // Get the part of the model that contains info about the actual CLR types
            var objectItemCollection = ((ObjectItemCollection) _metadata.GetItemCollection(DataSpace.OSpace));

            // Get the entity type from the model that maps to the CLR type
            var entityType = _metadata
                .GetItems<EntityType>(DataSpace.OSpace)
                .Single(e => objectItemCollection.GetClrType(e) == type);

            // Get the entity set that uses this entity type
            var entitySet = _metadata
                .GetItems<EntityContainer>(DataSpace.CSpace)
                .Single()
                .EntitySets
                .Single(s => s.ElementType.Name == entityType.Name);

            // Find the mapping between conceptual and storage model for this entity set
            var mapping = _metadata.GetItems<EntityContainerMapping>(DataSpace.CSSpace)
                .Single()
                .EntitySetMappings
                .Single(s => s.EntitySet == entitySet);
            return mapping;
        }

        /// <summary>
        /// Ensure that the column name has stored generated pattern identity.
        /// </summary>
        /// <param name="type">The model type</param>
        /// <param name="columnName">Name of table column</param>
        /// <returns></returns>
        public bool IsIdentity(Type type, string columnName) {

            var mapping = GetEntitySetMapping(type);

            // Find the storage property (column) that the column name is mapped
            var propertyMapping = mapping
                .EntityTypeMappings.Single()
                .Fragments.Single()
                .PropertyMappings
                .OfType<ScalarPropertyMapping>().Single(x => x.Column.Name == columnName);

            // Should have stored generated pattern identity
            return propertyMapping.Column.StoreGeneratedPattern == StoreGeneratedPattern.Identity;

        }
    }
}
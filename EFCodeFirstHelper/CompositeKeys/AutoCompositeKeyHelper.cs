using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using EFCodeFirstHelper.Common;

namespace EFCodeFirstHelper.CompositeKeys {

    /// <summary>
    /// A helper class to build triggers generator that supports autoincrement Id for composite keyed table.
    /// </summary>
    public  class AutoCompositeKeyHelper {

        private readonly DbContext _context;
        private readonly EntityHelper _helper;

        private readonly string _sequenceTableName = "__Sequences";
        private readonly string _namespace = "dbo";
        private readonly string _triggerPrefixName = "EFCFH";
        private string _triggerSuffixName;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Database Context</param>
        /// <param name="namespace">Database namespace. eg: dbo. Please fill without bracket.</param>
        /// <param name="sequenceTableName">The sequence table name. Please fill without bracket.</param>
        /// <param name="triggerPrefixName">The prefix name of the trigger.</param>
        /// <param name="triggerSuffixName">The suffix name of the trigger.</param>
        public AutoCompositeKeyHelper(DbContext context, string @namespace, string sequenceTableName, string triggerPrefixName, string triggerSuffixName) {
            _context = context;
            _namespace = @namespace;
            _sequenceTableName = sequenceTableName;
            _triggerPrefixName = triggerPrefixName;
            _triggerSuffixName = triggerSuffixName;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="context">Database Context</param>
        public AutoCompositeKeyHelper(DbContext context) {
            _context = context;
            _helper = new EntityHelper(_context);
        }

        /// <summary>
        /// Initialize sequence tablle and build all triggers for all models in the context that contains composite key.
        /// </summary>
        public  void Build() {
            InitSequencesTable();
            var entities = _helper.GetAllModels();
            foreach (var entity in entities) {
                BuildTrigger(entity);
            }
        }

        /// <summary>
        /// Create sequences table if it's does not exists yet.
        /// </summary>
        public void InitSequencesTable() {
            const string sql = @"IF OBJECT_ID('{0}.{1}', 'U') IS NULL
                BEGIN
                    CREATE TABLE [{0}].[{1}](
	                    [Model] [nvarchar](50) NOT NULL,
	                    [Constrains] [nvarchar](300) NOT NULL,
	                    [LastId] [bigint] NULL,
                    CONSTRAINT [PK_Sequence] PRIMARY KEY CLUSTERED 
                    (
	                    [Model] ASC,
	                    [Constrains] ASC
                    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
                    ) ON [PRIMARY]
                END";

            _context.Database.ExecuteSqlCommand(string.Format(sql, _namespace, _sequenceTableName));
        }

        /// <summary>
        /// Build trigger for the model. If the model doesn't have composite key then this method will do nothing.
        /// </summary>
        /// <param name="type"></param>
        public  void BuildTrigger(Type type) {

            // Get all primary keys
            var allPKs = _helper.GetPKs(type);

            // Do nothing for single PK
            if (allPKs.Count() == 1)
                return;

            // Get table name
            var tableName = _helper.GetTableName(type);

            // Get all columns
            var allColums = _helper.GetColums(type);

            // Get all foreign keys
            var allFKs = _helper.GetFKs(type);

            // Here's the real PK, should be only one PK, otherwise we wont take it
            var pk = allPKs.Single(x => !allFKs.Contains(x));

            if (!_helper.IsIdentity(type, pk))
                throw new Exception("'[" + tableName + "].[" + pk + "]' is not an identity column. You have to set it as an identity column.");

            // Get foreign keys that marked as PK
            var otherPKs = allFKs.Where(allPKs.Contains).ToList();

            // Get other fields than primary keys
            var fields = allColums.Where(x => x != pk && !otherPKs.Contains(x)).ToList();

            // Generate trigger's drop/create SQLs
            var sql = TriggerSQL(tableName, pk, otherPKs, fields);

            // Execute SQL
            _context.Database.ExecuteSqlCommand(sql);

        }
        
        /// <summary>
        /// Generate SQL string for INSTEAD OF trigger for the table. 
        /// We want to modify the original primary key. And it should set automatically by this trigger.
        /// And the highest value of primary key within the partition is recorded on sequences table.
        /// </summary>
        /// <param name="tableName">The table name.</param>
        /// <param name="pk">Original primary key column name.</param>
        /// <param name="otherPKs">Other primary keys as the constrains key of sequence.</param>
        /// <param name="fields">Fields of the table. We need these fields for 'instead of insert' purpose.</param>
        /// <returns></returns>
        private  string TriggerSQL(string tableName, string pk, IEnumerable<string> otherPKs, IEnumerable<string> fields) {

            const string sql = @"
IF OBJECT_ID ('{2}.{0}', 'TR') IS NOT NULL
    DROP TRIGGER [{2}].[{0}];
                
DECLARE @sql nvarchar(max);
SET @sql = '
-- ---------------------------------------------------------------------------------------------------
-- Trap the insertion process of [{2}].[{3}] to modify PK value before inserted.
-- This trigger automatically generated by EFCodeFirstHelper.
-- ---------------------------------------------------------------------------------------------------

CREATE TRIGGER [{2}].[{0}]
ON [{2}].[{3}]
INSTEAD OF INSERT
AS 
BEGIN
    -- We don''t want to return any affected row number
	SET NOCOUNT ON;

    -- Open identity insertion access
    SET IDENTITY_INSERT [{2}].[{3}] ON;

    -- The model name
    DECLARE @model as nvarchar(max);
    SET @model = ''[{2}].[{3}]'';

    -- Build sequence unique key
	DECLARE @constrains as nvarchar(max);
	SET @constrains = (SELECT {7} FROM INSERTED);

    -- Get the higher value of PK ([{5}]), just incase that data may modified outside or trigger were applied on dirty data.
    DECLARE @last_id as bigint;
    SET @last_id = ISNULL((SELECT MAX([{5}]) FROM [{2}].[{3}] WHERE {8}), 0);

    -- Get last PK ([{5}]) value from sequence
    DECLARE @seq_id as bigint;
	SET @seq_id = ISNULL((SELECT [LastId] FROM [{2}].[{1}] WHERE Model = @model AND [Constrains] = @constrains), 0);

    -- Pick the highest value to be use as PK value
    IF (@seq_id > @last_id)                        
        SET @last_id = @seq_id;
    
    -- Increment
    SET @last_id = @last_id + 1;

    IF (@seq_id > 0)
        -- There is an already sequence for this table, just update it
		UPDATE [{2}].[{1}] SET [LastId]=@last_id WHERE [Model] = @model AND [Constrains] = @constrains;
	ELSE
        -- Otherwise, make a new one
		INSERT INTO [{2}].[{1}] ([Model], [Constrains], [LastId]) VALUES(@model, @constrains, @last_id);
	
    -- Do insertion with modified PK value
    insert into [{2}].[{3}] ([{5}],{4},{6}) select @last_Id,{4},{6} from INSERTED;
    
    -- Close identity insertion access
    SET IDENTITY_INSERT [{2}].[{3}] OFF;

    -- EF needs these values
    SELECT @last_Id AS [{5}],{4} FROM INSERTED;
END'

EXEC sp_executesql @sql;";

            var pKs = otherPKs as string[] ?? otherPKs.ToArray();
            var sequenceConstraintsCondition = string.Join(" AND ", pKs.Select(x => string.Format("[{0}] IN (SELECT [{0}] FROM INSERTED)", x)));

            _triggerSuffixName = "Composite_Key_Identity";
            return string.Format(sql,

                // 0: Trigger name
                _triggerPrefixName + "_" + tableName.Replace(" ", "_") + "_"+ _triggerSuffixName,

                // 1: Sequence table name
                _sequenceTableName,

                // 2: Namespace for both model table and sequence table
                _namespace,

                // 3: Model table name
                tableName,

                // 4: List of other primary keys (other than main PK) for INSTEAD insertion purpose.
                string.Join(",", pKs.Select(x => string.Format("[{0}]", x))),

                // 5: PK name
                pk,

                // 6: List of rest of fields for INSTEAD insertion purpose.
                string.Join(",", fields.Select(x => string.Format("[{0}]", x))),

                // 7: Sequence key
                SequenceConstrainsKey(pKs),

                // 8: WHERE condition to get max id of the model
                sequenceConstraintsCondition);
        }

        /// <summary>
        /// Generate constrains key from other PKs. The constrains key would be [KeyColumn1]=[value]|[KeyColumn2]=[value]...
        /// </summary>
        /// <param name="otherPKs"></param>
        /// <returns></returns>
        private  string SequenceConstrainsKey(IEnumerable<string> otherPKs) {
            var pKs = otherPKs as string[] ?? otherPKs.ToArray();
            return string.Join(" + ''|'' + ", pKs.Select(x => string.Format("''[{0}]='' + CAST([{0}] AS nvarchar(max))", x)));
        }        
    }
}
# Entity Framework CodeFirst Helper

This helper was made to help Entity Framework developers when using CodeFirst to improve the productivity.

## AutoCompositeKeyHelper
Is a helper to deal with automatic incremental primary key for an entity that has multiple primay keys (Composite Key).

For example you have these models:

```
public class School {
    public long Id { get; set; }
    public string Name { get; set; }
    public ICollection<Student> Students { get; set; }
}
    
public class Student {
    public long Id { get; set; }

    public School School { get; set; }
    public long SchoolId { get; set; }

    public string Name { get; set; }
}

```

With this configuration:

```
protected override void OnModelCreating(DbModelBuilder builder) {
    builder.Entity<School>()
    	.HasKey(x => x.Id);
        
	builder.Entity<Student>()
        .HasKey(x => new { x.SchoolId, x.Id })
        .HasRequired(x => x.School)
        .WithMany(x=>x.Students)
        .HasForeignKey(x => x.SchoolId);
}
```                


The `Student` class has composite key `Id` and `SchoolId`.

Say that we have 2 Schools, with Id 1 and 2. In every time you add `Student` you will need to create, either calculate the value of `Id` since database won't generate it for you. Unless you give `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` attribute on `Id` property then you will not bothered again to think about it's values for every insertion. 

Then you'll get these results:

| SchoolId        | Id           | Name  |
| :-------------: |:-------------:| -----|
| 1 | 1 | Ane |
| 1 | 2 | Budi |
| 1 | 3 | Barbara |
| 2 | 4 | Erwin |
| 2 | 5 | Rois |
| 2 | 6 | Samantha |

That the values of `Id` sequentially generated by database automatically. But for some reason you may want to expect these results:


| SchoolId        | Id           | Name  |
| :-------------: |:-------------:| -----|
| 1 | 1 | Ane |
| 1 | 2 | Budi |
| 1 | 3 | Barbara |
| 2 | 1 | Erwin |
| 2 | 2 | Rois |
| 2 | 3 | Samantha |

That the `Id` of Student should be reset to 1 for every new `School` added. 

To achieve it, we need a database trigger to help us to modify the `Id` value on everytime you insert new data on `Student`. We are also need 1 helper table to store the latest inserted `Id` for particular group in the table, let's name this table as **__Sequences**, with structure like this:

| Column Name        | Type           | Description |
| :-------------: |:-------------:| -----|
| *Model* | nvarchar(50) | Name of table |
| *Constrains* | nvarchar(300) | Other primary keys name and value of the table |
| LastId | bigint | Highest value of Id used by the table filtered by Constrains |

And here's the trigger should look like:

```
CREATE TRIGGER [dbo].[Student_Auto_Composite_Key]
ON [dbo].[Student]
INSTEAD OF INSERT
AS 
BEGIN
    -- We don't want to return any affected row number
	SET NOCOUNT ON;

    -- Open identity insertion access
    SET IDENTITY_INSERT [dbo].[Student] ON;

    -- The model name
    DECLARE @model as nvarchar(max);
    SET @model = '[dbo].[Student]';

    -- Build sequence unique key
	DECLARE @constrains as nvarchar(max);
	SET @constrains = (SELECT '[SchoolId]=' + CAST([SchoolId] AS nvarchar(max)) FROM INSERTED);

    -- Get the higher value of PK ([Id]), just incase that data may modified outside or trigger were applied on dirty data.
    DECLARE @last_id as bigint;
    SET @last_id = ISNULL((SELECT MAX([Id]) FROM [dbo].[Student] WHERE [SchoolId] IN (SELECT [SchoolId] FROM INSERTED)), 0);

    -- Get last PK ([Id]) value from sequence
    DECLARE @seq_id as bigint;
	SET @seq_id = ISNULL((SELECT [LastId] FROM [dbo].[__Sequences] WHERE Model = @model AND [Constrains] = @constrains), 0);

    -- Pick the highest value to be use as PK value
    IF (@seq_id > @last_id)                        
        SET @last_id = @seq_id;
    
    -- Increment
    SET @last_id = @last_id + 1;

    IF (@seq_id > 0)
        -- There is an already sequence for this table, just update it
		UPDATE [dbo].[__Sequences] SET [LastId]=@last_id WHERE [Model] = @model AND [Constrains] = @constrains;
	ELSE
        -- Otherwise, make a new one
		INSERT INTO [dbo].[__Sequences] ([Model], [Constrains], [LastId]) VALUES(@model, @constrains, @last_id);
	
    -- Do insertion with modified PK value
    insert into [dbo].[Student] ([Id],[SchoolId],[Name]) select @last_Id,[SchoolId],[Name] from INSERTED;
    
    -- Close identity insertion access
    SET IDENTITY_INSERT [dbo].[Student] OFF;

    -- EF needs these values
    SELECT @last_Id AS [Id],[SchoolId] FROM INSERTED;
END
```


Seems that was a good idea, but not if you have to write the trigger by hand :) Specially if you have bunch of composite keyed tables. So this helper created to helps us to generate that trigger for us.

### Usage

Firstly, you have to make sure that the main primary key of your model decorated with `[DatabaseGenerated(DatabaseGeneratedOption.Identity)]` attribute. In the example above, you need to decorate the propery `Id` of `Student`:

```
public class Student {
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public School School { get; set; }
    public long SchoolId { get; set; }

    public string Name { get; set; }
}
```
Or by fluent you do this:
```
builder.Entity<Student>()
	.Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
```

Now, you need to call the helper inside the ``Seed(...)`` method from the *Database Initializer* or *Database Migration Configuration* class if you are using Migrations.

First, create the helper instance:

```
protected override void Seed(DataContext context) {
    // Helper instance
    var helper = CompositeKeys.AutoCompositeKeyHelper(context);
    ...
```

Next, within this method, if you want to create triggers for all entities you can continue with:

```
    // Build __Sequences table and triggers for all composite keyed tables in the context
    helper.Build();    
```

The helper will determines the entities that contains composite key automatically and build the triggers for you. The helper will also create **__Sequences** table if you don't have it yet.

Otherwise, you can customize the process. Specially, if you want to build for only single or several entities, you can do with:

```
    // Here's sequence table will be created if its doesn't exists yet
    helper.InitSequencesTable();
    
    // Tell helper to build trigger for specific entities
    helper.BuildTrigger(typeof(School));
    helper.BuildTrigger(typeof(Course));
```

Once this ``Seed(...)`` method called, the triggers should automatically created in your database.

For more detail investigation you can play with the `EFCodeFirstHelper.Test` test project.

### Notes
If you are in the middle of working on some project and the data is already there, then this helper should working fine. 

You also need no worry if the **__Sequences** table accidentally deleted because trigger will also look for the highest key value from the table data it self.

Just let me know if something wrong happened by put it on this repository **Issues**.
### Limitation
- Only works for SQL Server.
- Type of primary keys must be an integer family.

### Contribute
Feel free for all suggestions, improvements and pull requests.

### Credits
Thanks to:

http://romiller.com/tag/metadataworkspace/
http://www.codeproject.com/Tips/890432/Entity-Framework-DiagnosticsContext-Get-Detailed

## License
Feel free to use for any kind of purposes except for something that smells like a crime :)

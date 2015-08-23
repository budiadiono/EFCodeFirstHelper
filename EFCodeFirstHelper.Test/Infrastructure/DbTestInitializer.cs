using System.Data.Entity;

namespace EFCodeFirstHelper.Test.Infrastructure {
    public class DbTestInitializer : DropCreateDatabaseAlways<DataContext> {
        protected override void Seed(DataContext context) {
            (new CompositeKeys.AutoCompositeKeyHelper(context)).Build();
        }
    }
}
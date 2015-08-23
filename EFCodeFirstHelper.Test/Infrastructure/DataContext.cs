using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using EFCodeFirstHelper.Test.Infrastructure.Models;

namespace EFCodeFirstHelper.Test.Infrastructure {

    /// <summary>
    /// Data context for testing purpose that always dropped and created in every test.
    /// </summary>
    public class DataContext : DbContext {
        public DataContext()
            // Modify App.config to change the connection string.
            : base("EFCodeFirstHelper.Test") {

            Database.SetInitializer(new DbTestInitializer());
        }

        protected override void OnModelCreating(DbModelBuilder builder) {

            builder.Entity<School>()
                .HasKey(x => x.Id);

            builder.Entity<CourseGroup>()
                .HasKey(x => new { x.SchoolId, x.Id })
                .HasRequired(x => x.School)
                .WithMany(x=>x.CourseGroups)
                .HasForeignKey(x => x.SchoolId);

            builder.Entity<Course>()
                .HasKey(x => new { x.SchoolId, x.CourseGroupId, x.Id })
                .HasRequired(x => x.School)
                .WithMany()
                .HasForeignKey(x => x.SchoolId);

            builder.Entity<Course>()
                .HasRequired(x => x.CourseGroup)
                .WithMany(x => x.Courses)
                .HasForeignKey(x => new {x.SchoolId, x.CourseGroupId})
                .WillCascadeOnDelete(false);


            builder.Entity<Class>()
                .HasKey(x => new {x.SchoolId, x.CourseGroupId, x.Id});

            builder.Entity<Class>()
                .Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            builder.Entity<Class>()
                .HasRequired(x => x.CourseGroup)
                .WithMany(x => x.Classes)
                .HasForeignKey(x => new { x.SchoolId, x.CourseGroupId })
                .WillCascadeOnDelete(false);

            builder.Entity<Assessment>()
                .HasKey(x => new { x.SchoolId, x.CourseGroupId, x.CourseId, x.Id });
            builder.Entity<Assessment>()
                .Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);
            builder.Entity<Assessment>()
                .HasRequired(x => x.Course)
                .WithMany(x => x.Assessments)
                .HasForeignKey(x => new { x.SchoolId, x.CourseGroupId, x.CourseId })
                .WillCascadeOnDelete(false);

            builder.Entity<Student>()
                .HasKey(x => new {x.SchoolId, x.Id})
                .HasRequired(x => x.School)
                .WithMany(x => x.Students)
                .HasForeignKey(x => x.SchoolId);
            builder.Entity<Student>()
                .Property(x => x.Id).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            builder.Entity<AssessmentStudent>()
                .HasKey(x => new {x.SchoolId, x.CourseGroupId, x.CourseId, x.AssessmentId, x.AssessmentStudentId});
            builder.Entity<AssessmentStudent>()
                .Property(x => x.AssessmentStudentId).HasDatabaseGeneratedOption(DatabaseGeneratedOption.Identity);

            builder.Entity<AssessmentStudent>()
                .HasRequired(x => x.Assessment)
                .WithMany(x => x.AssessmentStudents)
                .HasForeignKey(x => new {x.SchoolId, x.CourseGroupId, x.CourseId, x.AssessmentId})
                .WillCascadeOnDelete(false);

            builder.Entity<AssessmentStudent>()
                .HasRequired(x => x.Student)
                .WithMany(x => x.AssessmentStudents)
                .HasForeignKey(x => new { x.SchoolId, x.StudentId });
        }

        
    }
}
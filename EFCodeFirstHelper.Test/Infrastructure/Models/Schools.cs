using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace EFCodeFirstHelper.Test.Infrastructure.Models {

    [Table("School Table")]
    public class School {
        public School() {
            CourseGroups = new Collection<CourseGroup>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public ICollection<CourseGroup> CourseGroups { get; set; }
        public ICollection<Student> Students { get; set; }
    }

    public class CourseGroup {
        public CourseGroup() {
            Courses = new Collection<Course>();
            Classes = new Collection<Class>();
        }

        [Column("Course Group Id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public School School { get; set; }
        public long SchoolId { get; set; }

        public string Name { get; set; }
        public ICollection<Course> Courses { get; set; }
        public ICollection<Class> Classes { get; set; }
    }

    [Table("Course Table")]
    public class Course {

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public School School { get; set; }
        public long SchoolId { get; set; }

        public CourseGroup CourseGroup { get; set; }
        public long CourseGroupId { get; set; }

        [Column("Course Name")]
        public string Name { get; set; }
        public ICollection<Assessment> Assessments { get; set; }
    }

    public class Class {
        public long Id { get; set; }
        public CourseGroup CourseGroup { get; set; }
        [Column("S Id")]
        public long SchoolId { get; set; }
        [Column("CGID")]
        public long CourseGroupId { get; set; }

        [Column("ClassName")]
        public string Name { get; set; }
    }

    public class Assessment {
        public long Id { get; set; }
        public Course Course { get; set; }
        public long SchoolId { get; set; }
        public long CourseGroupId { get; set; }
        public long CourseId { get; set; }
        public string Name { get; set; }
        public ICollection<AssessmentStudent> AssessmentStudents { get; set; }
    }

    public class Student {
        [Column("StudentId")]
        public long Id { get; set; }
        public string Name { get; set; }
        public School School { get; set; }
        [Column("School Id")]
        public long SchoolId { get; set; }
        public ICollection<AssessmentStudent> AssessmentStudents { get; set; }
    }

    [Table("Assessment Student")]
    public class AssessmentStudent {
        public Assessment Assessment { get; set; }
        public long SchoolId { get; set; }
        public long CourseGroupId { get; set; }
        public long CourseId { get; set; }
        public long AssessmentId { get; set; }
        public long AssessmentStudentId { get; set; }

        public Student Student { get; set; }
        public long StudentId { get; set; }

        public DateTime Date { get; set; }
        public decimal Score { get; set; }
    }
}
using System;
using System.Collections.ObjectModel;
using System.Linq;
using EFCodeFirstHelper.Test.Infrastructure;
using EFCodeFirstHelper.Test.Infrastructure.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EFCodeFirstHelper.Test.Tests {
    [TestClass]
    public class AutoCompositeKeyTest {
        [TestMethod]
        public void AddDataViaToManyRelationshipTest() {
            
            using (var context = new DataContext()) {

                
                var schools = context.Set<School>();
                

                for (int i = 0; i < 3; i++) {
                    var school = schools.Add(new School {
                        Name = "School " + i,
                        CourseGroups = new Collection<CourseGroup>()
                    });
                    var studentCounter = 1;

                    for (int j = 0; j < 3; j++) {
                        var courseGroup = new CourseGroup {
                            Name = "Course Group " + i + " - " + j,
                            Courses = new Collection<Course>(),
                            Classes = new Collection<Class>()
                        };
                        school.CourseGroups.Add(courseGroup);

                        for (int k = 0; k < 3; k++) {
                            var course = new Course {
                                Name = "Course " + i + " - " + j + " - " + k,
                                School = school,
                                Assessments = new Collection<Assessment>()
                            };
                            courseGroup.Courses.Add(course);

                            var @class = new Class {
                                Name = "Course " + i + " - " + j + " - " + k                                
                            };
                            courseGroup.Classes.Add(@class);


                            for (int l = 0; l < 15; l++) {
                                var assesment = new Assessment {
                                    Name = "Assessment " + i + " - " + j + " - " + k + " - " + l,
                                    AssessmentStudents = new Collection<AssessmentStudent>()
                                };
                                course.Assessments.Add(assesment);

                                for (int m = 0; m < 10; m++) {
                                    var student = new Student {
                                        School = school,
                                        Name = "Student " + studentCounter
                                    };
                                    studentCounter++;

                                    for (int n = 0; n < 3; n++) {
                                        assesment.AssessmentStudents.Add(new AssessmentStudent {
                                            Student = student,
                                            Date = DateTime.Now,
                                            Score = m * 100
                                        });
                                    }

                                }

                            }
                        }

                    }
                }

                context.SaveChanges();

                // Reset counter
                

                var data = schools.ToList();
                for (int i = 0; i < 3; i++) {
                    var school = data[i];
                    var studentCounter = 1;

                    // School Id should correct
                    Assert.AreEqual(i+1, school.Id);
                    var courseGroups = school.CourseGroups.OrderBy(x => x.Id).ToArray();

                    for (int j = 0; j < 3; j++) {
                        
                        var courseGroup = courseGroups[j];

                        // CourseGroup Id should correct
                        Assert.AreEqual(j+1, courseGroup.Id);


                        var courses = courseGroup.Courses.OrderBy(x => x.Id).ToArray();
                        var classes = courseGroup.Classes.OrderBy(x => x.Id).ToArray();

                        for (int k = 0; k < 3; k++) {
                            
                            var course = courses[k];

                            // Course Id should correct
                            Assert.AreEqual(k + 1, course.Id);

                            
                            var @class = classes[k];

                            // Class Id should correct
                            Assert.AreEqual(k + 1, @class.Id);

                            var assesments = course.Assessments.OrderBy(x => x.Id).ToArray();
                            for (int l = 0; l < 15; l++) {                                
                                var assessment = assesments[l];

                                // Assessment Id should correct
                                Assert.AreEqual(l + 1, assessment.Id);

                                var assessmentStudents =
                                    assessment.AssessmentStudents.OrderBy(x => x.AssessmentStudentId).ToArray();
                                var assessmentStudentCounter = 1;
                                for (int m = 0; m < 10; m++) {

                                    var student =
                                        assessmentStudents.Where(x => x.Student.Name == "Student " + studentCounter)
                                            .Select(x => x.Student).First();
                                    
                                    // Student Id should correct
                                    Assert.AreEqual(studentCounter, student.Id);
                                    studentCounter++;

                                    var assessmentStudents2 = assessmentStudents.Where(x => x.Student == student)
                                        .OrderBy(x => x.AssessmentStudentId).ToArray();

                                    for (int n = 0; n < 3; n++) {
                                        var assessmentStudent = assessmentStudents2[n];
                                        // Student Id should correct
                                        Assert.AreEqual(assessmentStudentCounter, assessmentStudent.AssessmentStudentId);
                                        assessmentStudentCounter++;
                                    }
                                }
                            }
                        }

                    }
                }
            }

        }
    }
}

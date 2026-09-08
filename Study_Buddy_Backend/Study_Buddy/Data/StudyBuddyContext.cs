using Microsoft.EntityFrameworkCore;
using StudyBuddy.Models;

namespace StudyBuddy.Data
{
    public class StudyBuddyContext : DbContext
    {
        public StudyBuddyContext(DbContextOptions<StudyBuddyContext> options)
            : base(options)
        {
        }

        public DbSet<ChatMessage> ChatHistory { get; set; }
        public DbSet<QuizResult> QuizResults { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Note> Notes { get; set; }
        public DbSet<StudyActivity> StudyActivities { get; set; }
        public DbSet<Exam> Exams { get; set; }
        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Chapter> Chapters { get; set; }
        public DbSet<Topic> Topics { get; set; }
        public DbSet<QuizQuestion> QuizQuestions { get; set; }
        public DbSet<TopicContent> TopicContents { get; set; }
        public DbSet<ChapterContent> ChapterContents { get; set; }
        public DbSet<Bookmark> Bookmarks { get; set; }
        public DbSet<QuestionPaper> QuestionPapers { get; set; }
        public DbSet<PaperQuestion> PaperQuestions { get; set; }
        public DbSet<TopicProgress> TopicProgresses { get; set; }
        public DbSet<ReviewSchedule> ReviewSchedules { get; set; }
        public DbSet<QuizTemplate> QuizTemplates { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var now = Database.IsSqlServer() ? "GETDATE()" : "CURRENT_TIMESTAMP";

            modelBuilder.Entity<ChatMessage>()
                        .ToTable("ChatHistory")
                        .HasKey(c => c.Id);

            modelBuilder.Entity<ChatMessage>()
                        .Property(c => c.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<QuizResult>()
                        .ToTable("QuizResults")
                        .HasKey(q => q.Id);

            modelBuilder.Entity<QuizResult>()
                        .Property(q => q.CompletedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<User>()
                        .ToTable("Users")
                        .HasKey(u => u.Id);

            modelBuilder.Entity<User>()
                        .Property(u => u.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<User>()
                        .HasIndex(u => u.Email)
                        .IsUnique();

            modelBuilder.Entity<Document>()
                        .ToTable("Documents")
                        .HasKey(d => d.Id);

            modelBuilder.Entity<Document>()
                        .Property(d => d.UploadedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Document>()
                        .HasOne(d => d.User)
                        .WithMany()
                        .HasForeignKey(d => d.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizResult>()
                        .HasOne(q => q.User)
                        .WithMany()
                        .HasForeignKey(q => q.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Note>()
                        .ToTable("Notes")
                        .HasKey(n => n.Id);

            modelBuilder.Entity<Note>()
                        .Property(n => n.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Note>()
                        .Property(n => n.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Note>()
                        .HasOne(n => n.User)
                        .WithMany()
                        .HasForeignKey(n => n.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<StudyActivity>()
                        .ToTable("StudyActivities")
                        .HasKey(s => s.Id);

            modelBuilder.Entity<StudyActivity>()
                        .Property(s => s.Timestamp)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<StudyActivity>()
                        .HasOne(s => s.User)
                        .WithMany()
                        .HasForeignKey(s => s.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Exam>()
                        .ToTable("Exams")
                        .HasKey(x => x.Id);

            modelBuilder.Entity<Exam>()
                        .Property(x => x.Name)
                        .IsRequired()
                        .HasMaxLength(120);

            modelBuilder.Entity<Exam>()
                        .Property(x => x.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Subject>()
                        .ToTable("Subjects")
                        .HasKey(s => s.Id);

            modelBuilder.Entity<Chapter>()
                        .ToTable("Chapters")
                        .HasKey(c => c.Id);

            modelBuilder.Entity<Chapter>()
                        .HasOne(c => c.Subject)
                        .WithMany(s => s.Chapters)
                        .HasForeignKey(c => c.SubjectId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Topic>()
                        .ToTable("Topics")
                        .HasKey(t => t.Id);

            modelBuilder.Entity<Topic>()
                        .Property(t => t.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Topic>()
                        .HasOne(t => t.Chapter)
                        .WithMany(c => c.Topics)
                        .HasForeignKey(t => t.ChapterId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizQuestion>()
                        .ToTable("QuizQuestions")
                        .HasKey(q => q.Id);

            modelBuilder.Entity<QuizQuestion>()
                        .HasOne(q => q.Topic)
                        .WithMany()
                        .HasForeignKey(q => q.TopicId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizQuestion>()
                        .HasOne(q => q.User)
                        .WithMany()
                        .HasForeignKey(q => q.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TopicContent>()
                        .ToTable("TopicContents")
                        .HasKey(t => t.Id);

            modelBuilder.Entity<TopicContent>()
                        .Property(t => t.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<TopicContent>()
                        .HasOne(t => t.Topic)
                        .WithMany()
                        .HasForeignKey(t => t.TopicId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicContent>()
                        .HasOne(t => t.User)
                        .WithMany()
                        .HasForeignKey(t => t.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ChapterContent>()
                        .ToTable("ChapterContents")
                        .HasKey(c => c.Id);

            modelBuilder.Entity<ChapterContent>()
                        .Property(c => c.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<ChapterContent>()
                        .HasOne(c => c.Chapter)
                        .WithMany()
                        .HasForeignKey(c => c.ChapterId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ChapterContent>()
                        .HasOne(c => c.User)
                        .WithMany()
                        .HasForeignKey(c => c.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Bookmark>()
                        .ToTable("Bookmarks")
                        .HasKey(b => b.Id);

            modelBuilder.Entity<Bookmark>()
                        .Property(b => b.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<Bookmark>()
                        .HasOne(b => b.Topic)
                        .WithMany()
                        .HasForeignKey(b => b.TopicId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Bookmark>()
                        .HasOne(b => b.User)
                        .WithMany()
                        .HasForeignKey(b => b.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuestionPaper>()
                        .ToTable("QuestionPapers")
                        .HasKey(p => p.Id);

            modelBuilder.Entity<QuestionPaper>()
                        .Property(p => p.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<QuestionPaper>()
                        .HasOne(p => p.User)
                        .WithMany()
                        .HasForeignKey(p => p.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PaperQuestion>()
                        .ToTable("PaperQuestions")
                        .HasKey(q => q.Id);

            modelBuilder.Entity<PaperQuestion>()
                        .HasOne(q => q.Paper)
                        .WithMany(p => p.Questions)
                        .HasForeignKey(q => q.PaperId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicProgress>()
                        .ToTable("TopicProgresses")
                        .HasKey(p => p.Id);

            modelBuilder.Entity<TopicProgress>()
                        .Property(p => p.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<TopicProgress>()
                        .HasOne(p => p.Topic)
                        .WithMany()
                        .HasForeignKey(p => p.TopicId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TopicProgress>()
                        .HasOne(p => p.User)
                        .WithMany()
                        .HasForeignKey(p => p.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ReviewSchedule>()
                        .ToTable("ReviewSchedules")
                        .HasKey(r => r.Id);

            modelBuilder.Entity<ReviewSchedule>()
                        .Property(r => r.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<ReviewSchedule>()
                        .Property(r => r.DueDate)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<ReviewSchedule>()
                        .HasOne(r => r.Topic)
                        .WithMany()
                        .HasForeignKey(r => r.TopicId)
                        .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewSchedule>()
                        .HasOne(r => r.User)
                        .WithMany()
                        .HasForeignKey(r => r.UserId)
                        .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<QuizTemplate>()
                        .ToTable("QuizTemplates")
                        .HasKey(t => t.Id);

            modelBuilder.Entity<QuizTemplate>()
                        .Property(t => t.CreatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<QuizTemplate>()
                        .Property(t => t.UpdatedAt)
                        .HasDefaultValueSql(now);

            modelBuilder.Entity<QuizTemplate>()
                        .HasOne(t => t.User)
                        .WithMany()
                        .HasForeignKey(t => t.UserId)
                        .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
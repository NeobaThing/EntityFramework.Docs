using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EFModeling.CustomFunctionMapping
{
    public class BloggingContext : DbContext
    {
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Tag> Tags { get; set; }

        #region BasicFunctionDefinition
        public int UniqueTagsCountForBlogPosts(int blogId)
            => throw new NotSupportedException();
        #endregion

        #region HasTranslationFunctionDefinition
        public int Difference(int first, int second)
            => throw new NotSupportedException();
        #endregion

        #region QueryableFunctionDefinition
        public IQueryable<Post> PostsTaggedWith(string tag)
            => throw new NotSupportedException();
        #endregion

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var noSeeding = false;
            if (noSeeding)
            {
                #region EntityConfiguration
                modelBuilder.Entity<Blog>()
                    .HasMany(b => b.Posts)
                    .WithOne(p => p.Blog)
                    .OnDelete(DeleteBehavior.NoAction);

                modelBuilder.Entity<Post>()
                    .HasMany(p => p.Tags)
                    .WithMany(t => t.Posts);
                #endregion
            }

            modelBuilder.Entity<Blog>()
                .HasMany(b => b.Posts)
                .WithOne(p => p.Blog)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Post>()
                .HasMany(p => p.Tags)
                .WithMany(t => t.Posts)
                .UsingEntity<Dictionary<string, object>>(
                    "PostTag",
                    r => r.HasOne<Tag>().WithMany().HasForeignKey("TagId"),
                    l => l.HasOne<Post>().WithMany().HasForeignKey("PostId"),
                    je =>
                    {
                        je.HasKey("PostId", "TagId");
                        je.HasData(
                            new { PostId = 1, TagId = "general" },
                            new { PostId = 1, TagId = "informative" },
                            new { PostId = 2, TagId = "classic" },
                            new { PostId = 3, TagId = "opinion" },
                            new { PostId = 4, TagId = "opinion" },
                            new { PostId = 4, TagId = "informative" });
                    });

            modelBuilder.Entity<Blog>()
                .HasData(
                    new Blog { BlogId = 1, Url = @"https://devblogs.microsoft.com/dotnet", Rating = 5 },
                    new Blog { BlogId = 2, Url = @"https://mytravelblog.com/", Rating = 4 });

            modelBuilder.Entity<Post>()
                .HasData(
                    new Post { PostId = 1, BlogId = 1, Title = "What's new", Content = "Lorem ipsum dolor sit amet", Rating = 5 },
                    new Post { PostId = 2, BlogId = 2, Title = "Around the World in Eighty Days", Content = "consectetur adipiscing elit", Rating = 5 },
                    new Post { PostId = 3, BlogId = 2, Title = "Glamping *is* the way", Content = "sed do eiusmod tempor incididunt", Rating = 4 },
                    new Post { PostId = 4, BlogId = 2, Title = "Travel in the time of pandemic", Content = "ut labore et dolore magna aliqua", Rating = 3 });

            modelBuilder.Entity<Tag>()
                .HasData(
                    new Tag { TagId = "general" },
                    new Tag { TagId = "classic" },
                    new Tag { TagId = "opinion" },
                    new Tag { TagId = "informative" });

            #region BasicFunctionConfiguration
            modelBuilder.HasDbFunction(typeof(BloggingContext).GetMethod(nameof(UniqueTagsCountForBlogPosts), new[] { typeof(int) }))
                .HasName("DistinctTagsCountForBlogPosts");
            #endregion

            #region HasTranslationFunctionConfiguration
            var sqlExpressionFactory = this.GetService<ISqlExpressionFactory>();
            
            // ABS(first - second)
            modelBuilder.HasDbFunction(typeof(BloggingContext).GetMethod(nameof(Difference), new[] { typeof(int), typeof(int) }))
                .HasTranslation(args => sqlExpressionFactory.Function(
                    name: "ABS",
                    arguments: new[]
                    {
                        sqlExpressionFactory.Subtract(
                            args.First(),
                            args.Skip(1).First())
                    }, 
                    nullable: false,
                    argumentsPropagateNullability: new[] { false, false },
                    returnType: typeof(int)));
            #endregion

            #region QueryableFunctionConfigurationToFunction
            modelBuilder.Entity<BlogWithMultiplePosts>().HasNoKey().ToFunction("BlogsWithMultiplePosts");
            #endregion

            #region QueryableFunctionConfigurationHasDbFunction
            modelBuilder.Entity<Post>().ToTable("Posts");
            modelBuilder.HasDbFunction(typeof(BloggingContext).GetMethod(nameof(PostsTaggedWith), new[] { typeof(string) }));
            #endregion
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=EFModeling.CustomFunctionMapping;Trusted_Connection=True;ConnectRetryCount=0");
        }
    }
}

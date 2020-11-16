using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace EFModeling.CustomFunctionMapping
{
    class Program
    {
        static void Main(string[] args)
        {
            using var context = new BloggingContext();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            context.Database.ExecuteSqlRaw(
                @"CREATE FUNCTION dbo.BlogsWithMultiplePosts()
                    RETURNS @blogs TABLE
                    (
                        Rating int,
                        Url nvarchar(max),
                        PostCount int not null
                    )
                    AS
                    BEGIN
                        INSERT INTO @blogs
                        SELECT b.Rating, b.Url, COUNT(p.BlogId)
                        FROM Blogs AS b
                        JOIN Posts AS p ON b.BlogId = p.BlogId
                        GROUP BY b.Rating, b.Url
                        HAVING COUNT(p.BlogId) > 1

                        RETURN
                    END");

            context.Database.ExecuteSqlRaw(
                @"CREATE FUNCTION dbo.DistinctTagsCountForBlogPosts(@id int)
                    RETURNS int
                    AS
                    BEGIN
                        RETURN (SELECT COUNT(*) FROM(
                            SELECT DISTINCT t.TagId FROM dbo.Tags AS t
                            JOIN dbo.PostTag AS pt ON t.TagId = pt.TagId
                            JOIN dbo.Posts AS p ON p.PostId = pt.PostId
                            JOIN dbo.Blogs AS b ON b.BlogId = p.BlogId
                            WHERE b.BlogId = @id) AS subquery);
                    END");

            context.Database.ExecuteSqlRaw(
                @"CREATE FUNCTION dbo.PostsTaggedWith(@tag varchar(max))
                    RETURNS @posts TABLE
                    (
                        PostId int not null,
                        BlogId int not null,
                        Content nvarchar(max),
                        Rating int not null,
                        Title nvarchar(max)
                    )
                    AS
                    BEGIN
                        INSERT INTO @posts
                        SELECT p.PostId, p.BlogId, p.Content, p.Rating, p.Title
                        FROM Posts AS p
                        WHERE EXISTS (
                            SELECT 1
                            FROM PostTag AS pt
                            INNER JOIN Tags AS t ON pt.TagId = t.TagId
                            WHERE (p.PostId = pt.PostId) AND (t.TagId = @tag))

                        RETURN
                    END");

            #region BasicQuery
            var query1 = from b in context.Blogs
                         where context.UniqueTagsCountForBlogPosts(b.BlogId) > 2
                         select b;
            #endregion
            var result1 = query1.ToList();

            #region HasTranslationQuery
            var query2 = from p in context.Posts
                         where p.PostId < context.Difference(p.BlogId, 3)
                         select p;
            #endregion
            var result2 = query2.ToList();

            #region ToFunctionQuery
            var query3 = from b in context.Set<BlogWithMultiplePosts>()
                         where b.Rating > 3
                         select new { b.Url, b.PostCount };
            #endregion
            var result3 = query3.ToList();

            #region TableValuedFunctionQuery
            var query4 = from t in context.Tags
                         where t.TagId.Length < 10
                         select context.PostsTaggedWith(t.TagId).ToList();
            #endregion
            var result4 = query4.ToList();
        }
    }
}

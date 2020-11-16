---
title: Custom function mapping - EF Core
description: Mapping user-defined functions to database functions
author: maumar
ms.date: 11/23/2020
uid: core/modeling/custom-function-mapping
---
# Custom function mapping

EF Core allows for using user-defined SQL functions in queries. To do that, the functions need to be mapped to a CLR method during model configuration. When translating the LINQ query to SQL, user-defined function will be called instead of the CLR function it has been mapped to.

## Mapping method to a SQL function

To illustrate the custom function mapping, lets define the following entities:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Blog.cs#Entity)]

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Post.cs#Entity)]

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Tag.cs#Entity)]

And the following model configuration:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#EntityConfiguration)]

Blog can have many posts, each post can be tagged with multiple tags, and each tag can be associated with multiple posts: many-to-many relationship.

Next, create the user-defined function `DistinctTagsCountForBlogPosts`, which returns the count of unique tags associated with all the posts of a given blog, based on the blog `Id`:

```sql
CREATE FUNCTION dbo.DistinctTagsCountForBlogPosts(@id int)
    RETURNS int
    AS
    BEGIN
        RETURN (SELECT COUNT(*) FROM(
            SELECT DISTINCT t.TagId FROM dbo.Tags AS t
            JOIN dbo.PostTag AS pt ON t.TagId = pt.TagId
            JOIN dbo.Posts AS p ON p.PostId = pt.PostId
            JOIN dbo.Blogs AS b ON b.BlogId = p.BlogId
            WHERE b.BlogId = @id) AS subquery);
    END
```

To use it in EF Core, define the following CLR method, which we will map to the user-defined function:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#BasicFunctionDefinition)]

In the example, the method is defined on `DbContext`, but it can also be defined in other places.

> [!NOTE]
> Body of the CLR method is not important. EF Core only looks at the method signature.

This function definition can be associated with user-defined function in the model configuration:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#BasicFunctionConfiguration)]

> [!NOTE]
> By default EF Core tries to map CLR function to a user-defined function with the same name. If the names are different, we can use `HasName` to select the correct name for the user-defined function we want to map to.

Now, executing the following query:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Program.cs#BasicQuery)]

Will produce this SQL:

```sql
SELECT [b].[BlogId], [b].[Rating], [b].[Url]
FROM [Blogs] AS [b]
WHERE [dbo].[DistinctTagsCountForBlogPosts]([b].[BlogId]) > 2
```

## Mapping method to function defined in the model

EF Core also allows for user-defined functions that don't map to the corresponding function in the database. It can be done by specifying the function body using the [Microsoft.EntityFrameworkCore.Query.SqlExpressions](dotnet/api/microsoft.entityframeworkcore.query.sqlexpressions) API. Function body is provided using `HasTranslation` method during user-defined function configuration.

In the example below, we'll create a function that computes difference between two integers.

CLR method is as follows:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#HasTranslationFunctionDefinition)]

The function definition is as follows:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#HasTranslationFunctionConfiguration)]

[SqlExpressionFactory](dotnet/api/microsoft.entityframeworkcore.query.sqlexpressionfactory) can be used to construct `SqlExpression` tree.

Once we define the function, it can be used in the query. Instead of calling database function, EF Core will translate the method body directly into SQL.

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Program.cs#HasTranslationQuery)]

Produces the following SQL:

```sql
SELECT [p].[PostId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Posts] AS [p]
WHERE [p].[PostId] < ABS([p].[BlogId] - 3)
```

## Mapping DbSet to a Table-Valued Function

It's also possible to map a `DbSet` of entities to a Table-Valued function instead of a table in the database. To illustrate this lets define another entity that represents blog with multiple posts. In the example, the entity is [keyless](keyless-entity-types), but it doesn't have to be.

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BlogWithMultiplePosts.cs#Entity)]

Next, create the following Table-Valued Function on the database, which returns only blogs with multiple posts as well as the number of posts associated with each of these blogs:

```sql
CREATE FUNCTION dbo.BlogsWithMultiplePosts()
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
END
```

Now, the `DbSet<BlogWithMultiplePost>` can be mapped to this function in a following way:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#QueryableFunctionConfigurationToFunction)]

> [!NOTE]
> In order to map DbSet to a Table-Valued Function the function must be parameterless. Also, names of the entity properties should match the names of the columns returned by the TVF. Any discrepancies can be configured using `HasColumnName` method, just like mapping to a regular table.

When the set is mapped to a Table-Valued function, the query:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Program.cs#ToFunctionQuery)]

Produces the following SQL:

```sql
SELECT [b].[Url], [b].[PostCount]
FROM [dbo].[BlogsWithMultiplePosts]() AS [b]
WHERE [b].[Rating] > 3
```

## Mapping Queryable function to a Table-Valued Function

EF Core also supports mapping to Table-Valued Function using a user-defined CLR function returning `IQueryable` of entity types. This allows EF Core to use Table-Valued Function with parameters. Process is similar to mapping a scalar user-defined function to a SQL function. We need a Table-Valued function on the database, CLR function that will be used in the LINQ queries and mapping between the two.

As an example we'll use a Table-Valued Function that returns all posts marked with a specific tag:

```sql
CREATE FUNCTION dbo.PostsTaggedWith(@tag varchar(max))
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
END
```

CLR function signature is as follows:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#QueryableFunctionDefinition)]

And below is the mapping:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/BloggingContext.cs#QueryableFunctionConfigurationHasDbFunction)]

> [!CAUTION]
> Mapping to queryable of entity types overrides the default mapping to a table for this set. If necessary (for example when the entity is not keyless) mapping to the table must be specified explicitly using `ToTable` method.

When the function is mapped, the following query:

[!code-csharp[Main](../../../samples/core/Modeling/CustomFunctionMapping/Program.cs#TableValuedFunctionQuery)]

will produce:

```sql
SELECT [t].[TagId], [p].[PostId], [p].[BlogId], [p].[Content], [p].[Rating], [p].[Title]
FROM [Tags] AS [t]
OUTER APPLY [dbo].[PostsTaggedWith]([t].[TagId]) AS [p]
WHERE CAST(LEN([t].[TagId]) AS int) < 10
ORDER BY [t].[TagId], [p].[PostId]
```

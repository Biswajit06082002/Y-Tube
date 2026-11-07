using Comment.BLL.Interfaces;
using Comment.BLL.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Comment.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddCommentBll(this IServiceCollection services)
    {
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IAuthorSyncHandler, AuthorSyncHandler>();
        return services;
    }
}

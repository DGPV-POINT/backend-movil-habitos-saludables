using HealthyMind.Application.Common;
using HealthyMind.Application.Features.Habits;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Application.Features.Recommendations;

internal sealed class RecommendationUseCases(
    IRecommendationRepository recommendations,
    OwnedResources owned,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRecommendationUseCases
{
    public async Task<RecommendationResponse?> GetLatestAsync(Guid userId, CancellationToken cancellationToken)
    {
        var latest = await recommendations.GetLatestAsync(userId, cancellationToken);
        return latest is null ? null : RecommendationResponse.From(latest);
    }

    public async Task<PagedResult<RecommendationResponse>> ListAsync(
        Guid userId,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        var result = await recommendations.ListByUserAsync(userId, page, cancellationToken);
        return result.Map(RecommendationResponse.From);
    }

    public async Task<RecommendationResponse> GetAsync(
        Guid userId,
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        var recommendation = await recommendations.GetByIdAsync(recommendationId, cancellationToken);
        return recommendation is not null && recommendation.UserId == userId
            ? RecommendationResponse.From(recommendation)
            : throw new NotFoundException("Recomendación no encontrada.");
    }

    public async Task RateAsync(
        Guid userId,
        Guid recommendationId,
        RecommendationFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        var recommendation = await recommendations.GetForUpdateAsync(recommendationId, cancellationToken);
        if (recommendation is null || recommendation.UserId != userId)
        {
            throw new NotFoundException("Recomendación no encontrada.");
        }

        recommendation.RateHelpful(request.Helpful, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkSeenAsync(Guid userId, RecommendationsSeenRequest request, CancellationToken cancellationToken)
    {
        var user = await owned.UserAsync(userId, cancellationToken);
        user.MarkRecommendationsSeen(request.SeenAt, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}

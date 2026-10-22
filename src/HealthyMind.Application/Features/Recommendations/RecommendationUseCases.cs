using HealthyMind.Application.Common;
using HealthyMind.Application.Ports.In;
using HealthyMind.Application.Ports.Out;

namespace HealthyMind.Application.Features.Recommendations;

internal sealed class RecommendationUseCases(IRecommendationRepository recommendations) : IRecommendationUseCases
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
}

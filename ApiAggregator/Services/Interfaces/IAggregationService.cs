namespace ApiAggregator.Services.Interfaces;

public interface IAggregationService
{
    Task<AggregatedResponse> AggregateDataAsync(AggregationRequest request, CancellationToken ct = default);
}

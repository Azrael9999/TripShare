namespace TripShare.Application.Contracts;

public sealed record AdSlotDto(string Slot, string Html, bool Enabled);

public sealed record AdConfigurationDto(bool Enabled, int FrequencyCapPerSession, int MaxSlotsPerPage, IReadOnlyList<AdSlotDto> Slots);

public sealed record AdImpressionRequest(string Slot, string? SessionId);

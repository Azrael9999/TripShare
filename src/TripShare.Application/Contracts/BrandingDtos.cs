namespace TripShare.Application.Contracts;

public sealed record BrandingConfigDto(
    string? LogoUrl,
    string? HeroImageUrl,
    string? MapOverlayUrl,
    string? LoginIllustrationUrl
);

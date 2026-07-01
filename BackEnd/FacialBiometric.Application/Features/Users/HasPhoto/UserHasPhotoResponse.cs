namespace FacialBiometric.Application.Features.Users.HasPhoto;

public sealed record UserHasPhotoResponse(long UserId, bool HasPhoto, DateTime? PhotoRegisteredAt);

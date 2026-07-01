using FacialBiometric.Application.Abstractions.Messaging;

namespace FacialBiometric.Application.Features.Users.HasPhoto;

public sealed record GetUserHasPhotoQuery(long UserId) : IQuery<UserHasPhotoResponse>;

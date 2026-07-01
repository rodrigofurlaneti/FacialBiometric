using FacialBiometric.Application.Abstractions.Messaging;
using FacialBiometric.Domain.Primitives;
using FacialBiometric.Domain.Repositories;

namespace FacialBiometric.Application.Features.Users.HasPhoto;

internal sealed class GetUserHasPhotoQueryHandler(IUserRepository repository)
    : IQueryHandler<GetUserHasPhotoQuery, UserHasPhotoResponse>
{
    public async Task<Result<UserHasPhotoResponse>> Handle(
        GetUserHasPhotoQuery request, CancellationToken cancellationToken)
    {
        var user = await repository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserHasPhotoResponse>(
                new Error("User.NotFound", $"Usuário {request.UserId} não encontrado."));
        }

        var response = new UserHasPhotoResponse(user.Id, user.HasPhoto, user.PhotoRegisteredAt);
        return Result.Success(response);
    }
}

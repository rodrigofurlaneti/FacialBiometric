using FacialBiometric.Application.Features.Users.HasPhoto;
using FacialBiometric.Domain.Entities;
using FacialBiometric.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FacialBiometric.Tests.Application.Users;

public sealed class GetUserHasPhotoQueryHandlerTests
{
    private readonly IUserRepository _repository = Substitute.For<IUserRepository>();

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenUserHasNoPhotoRegistered()
    {
        var user = User.Create("Rodrigo Furlaneti").Value;
        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetUserHasPhotoQueryHandler(_repository);

        var result = await handler.Handle(new GetUserHasPhotoQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasPhoto.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnTrue_WhenUserHasPhotoRegistered()
    {
        var user = User.Create("Rodrigo Furlaneti").Value;
        user.RegisterPhoto("1/foto.jpg", faceEmbedding: null);
        _repository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(user);

        var handler = new GetUserHasPhotoQueryHandler(_repository);

        var result = await handler.Handle(new GetUserHasPhotoQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HasPhoto.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenUserDoesNotExist()
    {
        _repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((User?)null);

        var handler = new GetUserHasPhotoQueryHandler(_repository);

        var result = await handler.Handle(new GetUserHasPhotoQuery(99), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("User.NotFound");
    }
}

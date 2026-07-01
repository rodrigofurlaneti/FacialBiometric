using FacialBiometric.Domain.Primitives;
using MediatR;

namespace FacialBiometric.Application.Abstractions.Messaging;

public interface ICommand : IRequest<Result>
{
}

public interface ICommand<TResponse> : IRequest<Result<TResponse>>
{
}

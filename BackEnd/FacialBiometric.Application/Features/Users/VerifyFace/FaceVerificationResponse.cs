namespace FacialBiometric.Application.Features.Users.VerifyFace;

public sealed record FaceVerificationResponse(long UserId, bool IsMatch, double Confidence);

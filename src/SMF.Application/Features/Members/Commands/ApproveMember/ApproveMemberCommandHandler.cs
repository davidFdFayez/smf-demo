using MediatR;
using Microsoft.Extensions.Logging;
using SMF.Application.Common.Exceptions;
using SMF.Application.Common.Interfaces;

namespace SMF.Application.Features.Members.Commands.ApproveMember;

public sealed class ApproveMemberCommandHandler
    : IRequestHandler<ApproveMemberCommand, Unit>
{
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<ApproveMemberCommandHandler> _logger;

    public ApproveMemberCommandHandler(
        IMemberRepository memberRepository,
        ILogger<ApproveMemberCommandHandler> logger)
    {
        _memberRepository = memberRepository;
        _logger = logger;
    }

    public async Task<Unit> Handle(ApproveMemberCommand request, CancellationToken cancellationToken)
    {
        var member = await _memberRepository.GetByIdAsync(request.MemberId, cancellationToken)
            ?? throw new NotFoundException("Member", request.MemberId);

        member.Approve();

        await _memberRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Approved member {MemberId} (SMF_ID {SmfId})",
            member.Id, member.SMF_ID);

        return Unit.Value;
    }
}

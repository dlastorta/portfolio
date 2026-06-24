using FluentValidation;

namespace ModularMonolith.Application.Modules.Jobs.Commands.ChangeJobStatus;

public sealed class ChangeJobStatusCommandValidator : AbstractValidator<ChangeJobStatusCommand>
{
    public ChangeJobStatusCommandValidator()
    {
        RuleFor(x => x.JobId)
            .NotEmpty().WithMessage("JobId is required.");

        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("NewStatus must be a valid job status.");
    }
}

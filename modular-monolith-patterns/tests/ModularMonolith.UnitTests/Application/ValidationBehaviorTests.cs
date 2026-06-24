using FluentAssertions;
using FluentValidation;
using ModularMonolith.Application.Common.Behaviors;
using ModularMonolith.Application.Modules.Jobs;
using ModularMonolith.Application.Modules.Jobs.Commands.CreateJob;
using ModularMonolith.Domain.Common;
using Xunit;

namespace ModularMonolith.UnitTests.Application;

public class ValidationBehaviorTests
{
    private static ValidationBehavior<CreateJobCommand, Result<JobDto>> CreateBehavior() =>
        new(new IValidator<CreateJobCommand>[] { new CreateJobCommandValidator() });

    [Fact]
    public async Task Short_circuits_with_a_failed_result_when_validation_fails()
    {
        var behavior = CreateBehavior();
        var handlerWasCalled = false;

        Task<Result<JobDto>> Next()
        {
            handlerWasCalled = true;
            return Task.FromResult(Result.Success(new JobDto()));
        }

        var result = await behavior.Handle(new CreateJobCommand(string.Empty), Next, CancellationToken.None);

        handlerWasCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Calls_the_handler_when_validation_passes()
    {
        var behavior = CreateBehavior();

        var result = await behavior.Handle(
            new CreateJobCommand("A valid title"),
            () => Task.FromResult(Result.Success(new JobDto { Title = "A valid title" })),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("A valid title");
    }
}

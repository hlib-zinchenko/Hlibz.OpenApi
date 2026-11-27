using System.Text.Json.Nodes;

namespace Hlibz.OpenApi.UseCases.Tests;

/// <summary>Signs a user up.</summary>
/// <param name="Email">The user's email.</param>
/// <param name="Country">The user's country.</param>
/// <param name="Plan">The plan the user starts on.</param>
/// <param name="Tags">Labels for the account.</param>
/// <param name="Referrer">Who sent the user.</param>
[RuleSummary("A user signs up once per email.")]
public sealed record SignUpCommand(
    [Required, EmailAddress] string Email,
    [CountryCode] string Country,
    [RuleSummary("Business plans need a sales call first.")] Plan Plan,
    [DistinctItems] IReadOnlyList<string> Tags,
    [NotZeroGuid] Guid? Referrer) : IUseCase;

/// <summary>A subscription plan.</summary>
public enum Plan
{
    /// <summary>Free.</summary>
    Free,

    /// <summary>Paid.</summary>
    Business,
}

public sealed record AccountDto(string Email);

public interface ISignUpCommandHandler : IUseCaseHandler<SignUpCommand, AccountDto>;

internal sealed class SignUpCommandHandler : ISignUpCommandHandler
{
    public Task<Result<AccountDto>> HandleAsync(SignUpCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<AccountDto>.Success(new AccountDto(useCase.Email)));
}

/// <summary>Changes an account's email, declaring its errors.</summary>
public sealed record ChangeEmailCommand([Required, EmailAddress] string Email) : IUseCase;

[Error("accounts_1001", ResultStatus.Conflict, "The email is taken.")]
public sealed record EmailTaken(string Email) : Error;

public union ChangeEmailError(EmailTaken);

public interface IChangeEmailCommandHandler : IUseCaseHandler<ChangeEmailCommand, AccountDto, ChangeEmailError>;

internal sealed class ChangeEmailCommandHandler : IChangeEmailCommandHandler
{
    public Task<Result<AccountDto, ChangeEmailError>> HandleAsync(ChangeEmailCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult<Result<AccountDto, ChangeEmailError>>(new AccountDto(useCase.Email));
}

/// <summary>Says hello: nothing to validate.</summary>
public sealed record HelloQuery(string? Name) : IUseCase;

internal sealed class HelloQueryHandler : IUseCaseHandler<HelloQuery, string>
{
    public Task<Result<string>> HandleAsync(HelloQuery useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result<string>.Success($"Hello, {useCase.Name}"));
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class CountryCodeAttribute : ValidationAttribute, IDescribedRule
{
    public const string Text = "A two-letter ISO country code in capitals.";

    public override bool IsValid(object? value) =>
        value is string { Length: 2 } code && code.All(char.IsAsciiLetterUpper);

    public void Describe(RuleDescription rule)
    {
        rule.Pattern = "^[A-Z]{2}$";
        rule.AddText(Text);
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class DistinctItemsAttribute : ValidationAttribute, IDescribedRule
{
    public override bool IsValid(object? value) =>
        value is not IEnumerable<string> items || items.Distinct(StringComparer.Ordinal).Count() == items.Count();

    public void Describe(RuleDescription rule)
    {
        rule.UniqueItems = true;
        rule.MaxItems = 10;
        rule.AddText("No duplicates, at most 10.");
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotZeroGuidAttribute : ValidationAttribute, IDescribedRule
{
    public override bool IsValid(object? value) => value is not Guid guid || guid != Guid.Empty;

    public void Describe(RuleDescription rule)
    {
        rule.NotConst = JsonValue.Create(Guid.Empty.ToString());
        rule.AddText("Must not be the empty id.");
    }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class PercentAttribute : ValidationAttribute, IDescribedRule
{
    public override bool IsValid(object? value) => value is not decimal number || number is > 0 and <= 100;

    public void Describe(RuleDescription rule)
    {
        rule.ExclusiveMinimum = 0;
        rule.Maximum = 100;
        rule.AddText("A percentage above 0.");
    }
}

/// <summary>DataAnnotations on positional record parameters, which ASP.NET Core doesn't publish.</summary>
public sealed record OrderCommand(
    [Range(1, 5)] int Seats,
    [StringLength(20, MinimumLength = 2)] string Note,
    [MinLength(1), MaxLength(3)] IReadOnlyList<string> Lines,
    [Range(0d, 1d, MinimumIsExclusive = true)] double Share,
    [RegularExpression("^[a-z]+$")] string Slug,
    [Url] string Site) : IUseCase;

internal sealed class OrderCommandHandler : IUseCaseHandler<OrderCommand>
{
    public Task<Result> HandleAsync(OrderCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

/// <summary>Both kinds of rule on one component-typed property.</summary>
public sealed record ChangePlanCommand(
    [OpenApiRule("Plan changes are logged.")][RuleSummary("Only to a plan you can pay for.")] Plan To) : IUseCase;

internal sealed class ChangePlanCommandHandler : IUseCaseHandler<ChangePlanCommand>
{
    public Task<Result> HandleAsync(ChangePlanCommand useCase, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Success());
}

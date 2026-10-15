namespace OmniRemote.Core.Validation;

public sealed class ProfileValidationResult
{
    public static readonly ProfileValidationResult Valid = new([]);

    private ProfileValidationResult(IReadOnlyList<string> errors) => Errors = errors;

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }

    public static ProfileValidationResult Fail(params string[] errors) => new(errors);
    public static ProfileValidationResult Fail(IReadOnlyList<string> errors) => new(errors);
}

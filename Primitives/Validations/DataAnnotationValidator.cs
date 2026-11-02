using System.ComponentModel.DataAnnotations;

namespace Anvil.Primitives.Validations;

public static class DataAnnotationValidator
{
    public static Result<ResultNone> Validate<TDto>(TDto? instance) where TDto : class
    {
        if (instance is null)
        {
            return Result.Failure<ResultNone>(Error.Validation(ErrorCode.InvalidArgument, "Request payload cannot be null."));
        }

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(instance);
        var isValid = Validator.TryValidateObject(instance, context, validationResults, validateAllProperties: true);

        if (isValid)
        {
            return Result.Success(new ResultNone());
        }
        else
        {
            var violations = new List<FieldViolation>(validationResults.Count);
            foreach (var validationResult in validationResults)
            {
                var message = validationResult.ErrorMessage ?? "Invalid field value.";
                if (validationResult.MemberNames.Any())
                {
                    foreach (var memberName in validationResult.MemberNames)
                    {
                        violations.Add(new FieldViolation(memberName, message));
                    }
                }
                else
                {
                    violations.Add(new FieldViolation(string.Empty, message));
                }
            }

            return Result.Failure<ResultNone>(Error.Validation(violations));
        }
    }
}

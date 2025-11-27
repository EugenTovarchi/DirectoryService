using DirectoryService.SharedKernel;
using FluentValidation.Results;

namespace DirectoryService.Core.Validation;

public static class ValidationExtensions
{
    public static Failure ToErrors(this ValidationResult validationResult)
    {
        var validationErrors = validationResult.Errors;

        var errors = from validationError in validationErrors
                     let errorMessage = validationError.ErrorMessage
                     let error = Error.Deserialize(errorMessage)
                     select Error.Validation(error.Code, error.Message, validationError.PropertyName);

        return new Failure(errors.ToList());
    }
}

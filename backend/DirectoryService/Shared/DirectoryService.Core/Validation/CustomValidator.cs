using CSharpFunctionalExtensions;
using DirectoryService.SharedKernel;
using FluentValidation;

namespace DirectoryService.Core.Validation;

public static class CustomValidator
{
    public static IRuleBuilderOptionsConditions<T, TElement> MustBeValueObject<T, TElement, TValueObject>(
        this IRuleBuilder<T, TElement> ruleBuilder, Func<TElement, Result<TValueObject, Error>> factoryMethod)
    {
        return ruleBuilder.Custom((value, context) =>
        {
            Result<TValueObject, Error> result = factoryMethod(value);

            if (!result.IsSuccess)
            {
                context.AddFailure(result.Error.Serialize());
            }
        });
    }

    public static IRuleBuilderOptions<T, TProperty> WithError<T, TProperty>(
     this IRuleBuilderOptions<T, TProperty> rule, Error error)
    {
        return rule.WithMessage(error.Serialize());
    }
}

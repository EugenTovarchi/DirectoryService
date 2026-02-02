namespace DirectoryService.SharedKernel;

public static class Errors
{
    public static class General
    {
        public static Error ValueIsInvalid(string? name = null)
        {
            string label = name ?? "value";
            return Error.Validation("value.is.invalid", $"{label} is invalid.");
        }

        public static Error NotFound(Guid? id = null)
        {
            string forId = id == null ? " " : $"for id '{id}'";
            return Error.NotFound("record.not.found", $"record not found {forId}");
        }

        public static Error NotFoundValue(string? field = null)
        {
            return Error.NotFound("value.not.found", $"value not found {field}");
        }

        public static Error ValueIsRequired(string? name = null)
        {
            string label = name == null ? " " : " " + name + " ";
            return Error.Validation("length.is.invalid", $"invalid {label} length");
        }

        public static Error ValueIsEmptyOrWhiteSpace(string field)
        {
            return Error.Validation("value.is.empty", $"invalid {field} is empty or white space");
        }

        public static Error Duplicate(string field)
        {
            return Error.Conflict("field.duplicate", $"{field} already exists");
        }

        public static Error EmptyId(Guid id)
        {
            return Error.Validation("id.empty", $"{id} cannot be empty Guid");
        }

        public static Error ValueIsZero(string field)
        {
            return Error.Validation("value.is.zero", $"{field} can not be zero.");
        }

        public static Error ValueMustBePositive(string field)
        {
            return Error.Validation("value.not.positive", $"{field} can not be negative.");
        }

        public static Error NotFoundEntity(string? field = null)
        {
            return Error.NotFound("entity.not.found", $"entity not found {field}");
        }

        public static Error ValueIsEmpty(string? field = null)
        {
            return Error.Validation("value.is.empty", $" value {field} must be not empty");
        }

        public static Error ValueIsTooLarge(string field, int maxValue)
        {
            return Error.Validation(
                "value.is.too.large",
                $"value '{field}' is too large. Max value: {maxValue}",
                invalidField: field);
        }

        public static Error DatabaseError(string operation = "operation")
        {
            return Error.Conflict(
                "database.error",
                $"Database {operation} failed");
        }

        public static Error ResourceLocked(string? field = null)
        {
            return Error.Conflict(
                "database.error",
                $"{field}  already locked");
        }
    }

    public static class Validation
    {
        public static Error RecordIsInvalid(string? field = null)
        {
            return Error.Validation("record.is.invalid", $"{field} is invalid.");
        }
    }

    public static class Database
    {
        public static Error ResourceLocked(string? field = null)
        {
            return Error.Validation("resource.is.invalid", $"{field} is invalid.");
        }
    }

    public static class Minio
    {
        public static Error FailUpload(string? field = null)
        {
            return Error.Failure("files.not.uploaded", $"fail to  upload {field} files.");
        }
    }

    public static class User
    {
        public static Error InvalidCredentials(string? field = null)
        {
            return Error.Validation("credentials.is.invalid", "User credentials is invalid");
        }
    }

    public static class Tokens
    {
        public static Error ExpiredToken(string? field = null)
        {
            return Error.Validation("token.is.expired", "Your token is expired");
        }

        public static Error InvalidToken(string? field = null)
        {
            return Error.Validation("token.is.invalid", "Your token is invalid");
        }
    }
}
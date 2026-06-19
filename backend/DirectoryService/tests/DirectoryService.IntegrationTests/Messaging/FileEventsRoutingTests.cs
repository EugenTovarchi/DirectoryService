using FluentAssertions;
using SharedService.SharedKernel.Messaging.Files;

namespace DirectoryService.IntegrationTests.Messaging;

public class FileEventsRoutingTests
{
    public static TheoryData<string, string> ExpectedBindings => new()
    {
        { FileEventsRouting.ALL_FILE_UPLOADED, "file.uploaded.#" },
        { FileEventsRouting.ALL_FILE_DELETED, "file.deleted.#" },
        { FileEventsRouting.DEPARTMENT_FILE_UPLOADED, "file.uploaded.*.department" },
        { FileEventsRouting.DEPARTMENT_FILE_DELETED, "file.deleted.*.department" },
        { FileEventsRouting.LOCATION_FILE_UPLOADED, "file.uploaded.*.location" },
        { FileEventsRouting.LOCATION_FILE_DELETED, "file.deleted.*.location" },
        { FileEventsRouting.POSITION_FILE_UPLOADED, "file.uploaded.*.position" },
        { FileEventsRouting.POSITION_FILE_DELETED, "file.deleted.*.position" },
    };

    public static TheoryData<string, string> MatchingBindings => new()
    {
        { FileEventsRouting.ALL_FILE_UPLOADED, "file.uploaded.photo.department" },
        { FileEventsRouting.ALL_FILE_UPLOADED, "file.uploaded.video.location" },
        { FileEventsRouting.ALL_FILE_UPLOADED, "file.uploaded.document.position" },
        { FileEventsRouting.ALL_FILE_DELETED, "file.deleted.photo.department" },
        { FileEventsRouting.ALL_FILE_DELETED, "file.deleted.video.location" },
        { FileEventsRouting.ALL_FILE_DELETED, "file.deleted.document.position" },
        { FileEventsRouting.DEPARTMENT_FILE_UPLOADED, "file.uploaded.photo.department" },
        { FileEventsRouting.DEPARTMENT_FILE_DELETED, "file.deleted.photo.department" },
        { FileEventsRouting.LOCATION_FILE_UPLOADED, "file.uploaded.video.location" },
        { FileEventsRouting.LOCATION_FILE_DELETED, "file.deleted.video.location" },
        { FileEventsRouting.POSITION_FILE_UPLOADED, "file.uploaded.document.position" },
        { FileEventsRouting.POSITION_FILE_DELETED, "file.deleted.document.position" },
    };

    public static TheoryData<string, string> NonMatchingBindings => new()
    {
        { FileEventsRouting.DEPARTMENT_FILE_UPLOADED, "file.deleted.photo.department" },
        { FileEventsRouting.DEPARTMENT_FILE_UPLOADED, "file.uploaded.photo.location" },
        { FileEventsRouting.DEPARTMENT_FILE_UPLOADED, "file.uploaded.photo.position" },
        { FileEventsRouting.DEPARTMENT_FILE_DELETED, "file.uploaded.photo.department" },
        { FileEventsRouting.LOCATION_FILE_UPLOADED, "file.uploaded.video.department" },
        { FileEventsRouting.LOCATION_FILE_DELETED, "file.deleted.video.position" },
        { FileEventsRouting.POSITION_FILE_UPLOADED, "file.uploaded.document.department" },
        { FileEventsRouting.POSITION_FILE_DELETED, "file.deleted.document.location" },
        { FileEventsRouting.ALL_FILE_UPLOADED, "directory.uploaded.photo.department" },
        { FileEventsRouting.ALL_FILE_DELETED, "file.archived.photo.department" },
    };

    [Theory]
    [MemberData(nameof(ExpectedBindings))]
    public void BindingKeys_Should_Have_Expected_Topic_Patterns(
        string bindingKey,
        string expectedBindingKey)
    {
        bindingKey.Should().Be(expectedBindingKey);
    }

    [Theory]
    [MemberData(nameof(MatchingBindings))]
    public void BindingKeys_Should_Match_Expected_File_Event_Routing_Keys(
        string bindingKey,
        string routingKey)
    {
        TopicMatches(bindingKey, routingKey).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(NonMatchingBindings))]
    public void BindingKeys_Should_Not_Match_Other_File_Event_Routing_Keys(
        string bindingKey,
        string routingKey)
    {
        TopicMatches(bindingKey, routingKey).Should().BeFalse();
    }

    [Theory]
    [InlineData("PHOTO", "DEPARTMENT", FileEventsRouting.DEPARTMENT_FILE_UPLOADED)]
    [InlineData("Video", "Location", FileEventsRouting.LOCATION_FILE_UPLOADED)]
    [InlineData("document", "position", FileEventsRouting.POSITION_FILE_UPLOADED)]
    public void FileUploaded_RoutingKey_Should_Match_Target_Entity_Binding(
        string assetType,
        string entityType,
        string bindingKey)
    {
        var routingKey = FileEventsRouting.RoutingKeys.FileUploaded(assetType, entityType);

        TopicMatches(bindingKey, routingKey).Should().BeTrue();
        TopicMatches(FileEventsRouting.ALL_FILE_UPLOADED, routingKey).Should().BeTrue();
    }

    [Theory]
    [InlineData("PHOTO", "DEPARTMENT", FileEventsRouting.DEPARTMENT_FILE_DELETED)]
    [InlineData("Video", "Location", FileEventsRouting.LOCATION_FILE_DELETED)]
    [InlineData("document", "position", FileEventsRouting.POSITION_FILE_DELETED)]
    public void FileDeleted_RoutingKey_Should_Match_Target_Entity_Binding(
        string assetType,
        string entityType,
        string bindingKey)
    {
        var routingKey = FileEventsRouting.RoutingKeys.FileDeleted(assetType, entityType);

        TopicMatches(bindingKey, routingKey).Should().BeTrue();
        TopicMatches(FileEventsRouting.ALL_FILE_DELETED, routingKey).Should().BeTrue();
    }

    private static bool TopicMatches(string bindingKey, string routingKey)
    {
        var bindingParts = bindingKey.Split('.');
        var routingParts = routingKey.Split('.');

        return TopicMatches(bindingParts, routingParts, 0, 0);
    }

    private static bool TopicMatches(
        IReadOnlyList<string> bindingParts,
        IReadOnlyList<string> routingParts,
        int bindingIndex,
        int routingIndex)
    {
        while (bindingIndex < bindingParts.Count)
        {
            var bindingPart = bindingParts[bindingIndex];

            if (bindingPart == "#")
            {
                if (bindingIndex == bindingParts.Count - 1)
                {
                    return true;
                }

                for (var index = routingIndex; index <= routingParts.Count; index++)
                {
                    if (TopicMatches(bindingParts, routingParts, bindingIndex + 1, index))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (routingIndex >= routingParts.Count)
            {
                return false;
            }

            if (bindingPart != "*" && bindingPart != routingParts[routingIndex])
            {
                return false;
            }

            bindingIndex++;
            routingIndex++;
        }

        return routingIndex == routingParts.Count;
    }
}

using System.Collections.Generic;
using System.Linq;

namespace AvatarPackBuilder.Models;

public sealed record SpriteState(string DisplayName, string Prefix);
public sealed record SpriteFrameGroup(string Id, string DisplayName, string FilePrefix, int DefaultCount);

public static class SpriteCatalog
{
    public static readonly List<SpriteState> States = new()
    {
        new("Normal", "normal"),
        new("Sad", "sad"),
        new("Angry", "angry"),
        new("Happy", "happy"),
        new("Surprised", "surprised"),
        new("Shocked", "shocked"),
        new("Sly", "sly"),
        new("Excited", "excited"),
        new("Thinking", "thinking"),
        new("Confused", "confused"),
        new("Pointing at Audience", "pointing_at_audience"),
        new("Both Arms Up", "both_arms_up"),
        new("One Arm Up", "one_arm_up"),
        new("Wave", "wave"),
        new("Hands on Hips", "hands_on_hips"),
        new("Shrug", "shrug"),
        new("Crossed Arms", "crossed_arms"),
        new("Presenting to Side", "presenting_to_side"),
        new("Celebrating Jump", "celebrating_jump")
    };

    public static readonly IReadOnlyList<SpriteFrameGroup> FrameGroups = new List<SpriteFrameGroup>
    {
        new("idle", "Idle", "idle", 2),
        new("idle_blink", "Blink", "idle_blink", 1),
        new("talk_small", "Talk Small", "talk_small", 2),
        new("talk", "Talk Normal", "talk", 2),
        new("talk_large", "Talk Large", "talk_large", 2)
    };

    private static readonly Dictionary<string, int> FrameGroupCounts =
        FrameGroups.ToDictionary(group => group.Id, group => group.DefaultCount, StringComparer.OrdinalIgnoreCase);

    public static IEnumerable<string> FileNamesFor(SpriteState state)
    {
        return FrameGroups.SelectMany(group => FileNamesFor(state, group.Id));
    }

    public static IEnumerable<string> FileNamesFor(SpriteState state, string groupId)
    {
        var group = FrameGroups.FirstOrDefault(candidate => candidate.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));
        if (group == null)
            return Enumerable.Empty<string>();

        int count = CountFor(group.Id);
        return Enumerable.Range(1, count)
            .Select(index => $"{state.Prefix}_{group.FilePrefix}{index:000}.png");
    }

    public static int AddFrameSlot(string groupId)
    {
        var group = FrameGroups.First(candidate => candidate.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase));
        int nextCount = CountFor(group.Id) + 1;
        FrameGroupCounts[group.Id] = nextCount;
        return nextCount;
    }

    public static int CountFor(string groupId)
    {
        return FrameGroupCounts.TryGetValue(groupId, out int count) ? count : 0;
    }
}

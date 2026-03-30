using Aion2DPSViewer.Packet;
using System.Collections.Generic;

namespace Aion2DPSViewer.Core;

internal class PartyTracker
{
    private HashSet<string> _members = new HashSet<string>();
    private int _epoch;

    public string? SelfNickname { get; private set; }
    public int? SelfServerId { get; private set; }
    public string? SelfServerName { get; private set; }
    public int Epoch => _epoch;

    public string[] OnPartySync(List<PartyMember> members)
    {
        _members = ToDedupedSet(members);
        return ToArray(_members);
    }

    public string[] OnPartyUpdate(List<PartyMember> members, out List<PartyMember> newMembers)
    {
        HashSet<string> dedupedSet = ToDedupedSet(members);
        newMembers = new List<PartyMember>();
        foreach (PartyMember member in members)
        {
            if (!_members.Contains($"{member.Nickname}:{member.ServerId}"))
                newMembers.Add(member);
        }
        _members = dedupedSet;
        return ToArray(dedupedSet);
    }

    public void Disband()
    {
        ++_epoch;
        _members.Clear();
    }

    public void SetSelf(string nickname, int? serverId, string? serverName)
    {
        SelfNickname = nickname;
        SelfServerId = serverId;
        SelfServerName = serverName;
    }

    public int IncrementEpoch() => ++_epoch;

    private static HashSet<string> ToDedupedSet(List<PartyMember> members)
    {
        HashSet<string> dedupedSet = new HashSet<string>();
        foreach (PartyMember member in members)
            dedupedSet.Add($"{member.Nickname}:{member.ServerId}");
        return dedupedSet;
    }

    private static string[] ToArray(HashSet<string> set)
    {
        string[] array = new string[set.Count];
        set.CopyTo(array);
        return array;
    }
}

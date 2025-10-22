using HarmonyLib;
using MTM101BaldAPI.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ABWEvents.Events;

public class MysteryEvent : BonusEventBase
{
    private RandomEventMetadata[] eventsPossible;
    private static FieldInfo _eventTime = AccessTools.DeclaredField(typeof(RandomEvent), "eventTime");
    private static FieldInfo ecEvents = AccessTools.DeclaredField(typeof(EnvironmentController), "events");

    public override void Initialize(EnvironmentController controller, Random rng)
    {
        base.Initialize(controller, rng);
        eventsPossible = RandomEventMetaStorage.Instance.FindAll(x =>
        !x.flags.HasFlag(RandomEventFlags.Special) &&
        !x.flags.HasFlag(RandomEventFlags.CharacterSpecific) &&
        !x.flags.HasFlag(RandomEventFlags.RoomSpecific) &&
        !x.flags.HasFlag(RandomEventFlags.AffectsGenerator) &&
        x.type != RandomEventType.Lockdown); // WHY MISSED THE TEXTURE, WHY?? THAT DOES AFFECTS THE LEVEL GENERATOR!!
    }
    
    public override void Begin()
    {
        base.Begin();
        var currentlist = ecEvents.GetValue(ec) as List<RandomEvent>;
        var newlist = eventsPossible.ToList().FindAll(x => !currentlist.Exists(j => j.Type == x.type));
        if (newlist.Count == 0) // Failsafe
            newlist = eventsPossible.ToList();
        var newEvent = Instantiate(newlist[crng.Next(0, newlist.Count)].value, ec.transform);
        newEvent.Initialize(ec, crng);
        _eventTime.SetValue(newEvent, EventTime);
        newEvent.Begin();
    }
}

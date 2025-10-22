using HarmonyLib;
using System.Reflection;

namespace ABWEvents.Events;

public class BonusEventBase : RandomEvent // This does nothing by its own except time being set afterwards all events and before the time over event.
{
    private static FieldInfo _eventTime = AccessTools.DeclaredField(typeof(RandomEvent), "eventTime");
    public void SetEventTime() => _eventTime.SetValue(this, GetEventTime());
    public float GetEventTime() => 0.5f * (maxEventTime - minEventTime) + minEventTime;

    private void Start()
    {
        if (eventJingleOverride == null)
            eventJingleOverride = ABWEventsPlugin._bonusJingle;
    }
}
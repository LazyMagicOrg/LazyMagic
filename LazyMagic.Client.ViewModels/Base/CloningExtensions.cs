
namespace LazyMagic.Client.ViewModels;

public static class LzCloningExtensions
{

    public static Dictionary<string, Delegate[]> GetAndClearEventSubscriptions(object instance)
    {
        var eventInfos = instance.GetType().GetEvents();
        var subscriptions = new Dictionary<string, Delegate[]>();

        foreach (var eventInfo in eventInfos)
        {
            var eventDelegate = GetEventDelegate(instance, eventInfo);
            if (eventDelegate != null)
            {
                subscriptions[eventInfo.Name] = eventDelegate.GetInvocationList();
                SetEventDelegate(instance, eventInfo, null);
            }
        }
        return subscriptions;
    }

    public static void RestoreEventSubscriptions(object instance, Dictionary<string, Delegate[]> subscriptions)
    {
        foreach (var subscription in subscriptions)
        {
            var eventInfo = instance.GetType().GetEvent(subscription.Key);
            var eventDelegate = Delegate.Combine(subscription.Value);
            SetEventDelegate(instance, eventInfo, eventDelegate);
        }
    }

    static Delegate? GetEventDelegate(object? instance, EventInfo? eventInfo)
    {
        if (instance == null) return null;
        if (eventInfo == null) return null;
        // Get all instance fields with non-public binding flags
        var fields = instance.GetType().GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic);

        // Find the field that matches the event name
        var fieldInfo = fields.FirstOrDefault(f => f.Name.Contains(eventInfo.Name));


        if (fieldInfo == null)
        {
            throw new InvalidOperationException($"BackingField for the event '{eventInfo.Name}' not found.");
        }

        // Get the event delegate from the field
        var eventDelegate = fieldInfo.GetValue(instance) as Delegate;

        return null;
    }

    static void SetEventDelegate(object instance, EventInfo? eventInfo, Delegate? value)
    {
        var fields = instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (var field in fields)
        {
            if (field.FieldType == eventInfo?.EventHandlerType)
            {
                field.SetValue(instance, value);
                break;
            }
        }
    }
}

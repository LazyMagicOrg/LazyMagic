namespace LazyMagic.Client.ViewModels;

/*
Notifications 
This ViewModel class supports INotificationSvc by providing:
    UpdateFromNotification(string data) -- TDTO object in JSON form
    NotificationEditOption = Cancel | Merge 
    TModel DataCopy
    TModel NotificationData - data updated from most recent notification if State == Edit
    [Reactive] bool NotificationReceived - fired when a notification recieved

The INotificationSvc receives Notification updates from a service (either by polling or websocket).
Notification
    Id - a GUID for each notification
    TopicId - we subscribe to topics in the INotificationSvc - this selects what notifications we receive from the service
    UserId - not currently used
    PayloadParentId - usually the Id field of the payload items parent - normally used by ItemsViewModel subscription
    PayloadId - usually the Id field from the payload class - normally used by ItemViewModel subscription
    PayloadType - The name of the class serialized into the Payload data
    Payload  - JSON string containing serialized instance of PayloadType
    PayloadAction - Create, Update, Delete. Note that only the Update action is meaningful for the ItemViewModel
                    Create and Delete are generally handled by the ItemsViewModel owning the ItemViewModel instances.
    CreatedAt - datetime utc ticks (store as long)

- We filter on Data.Id 
- We process Notification Actions to update the ViewModel Data.

What we do with a Notification depends on the current state of the ViewModel. 
State == New
    - Notification should not pass filter as the new Data has no Id
    - Note that the ItemsViewModel normally handles adding ItemViewModel instances
State == Current 
    - We update the ViewModel Data
State == Deleted
    - Ignore
    - The viewmodel is probably being disposed of when the notification arrives
State == Edit 
    This one is complicated and we use NotificationEditOption to govern behavior:
    NotificationEditOption == Cancel 
        - We cancel the current edit and update the ViewModel Data from the Notification
    NotificationEditOption == Merge 
        - We maintain three buffers 
            - DataCopy, a copy of the data as it was before edit 
            - NotificationBuffer, a copy of the data as updated by the last processed Notification 
            - Data, the currently edited data 
        - We maintain three ChangeLists Dictionary<propertyname, propertyvalue)
            - NotificationChanges: DataCopy propertyvalue <> NotificationData propertyvalue 
            - EditChanges: DataCopy propertyvalue <> Data propertyvalue 
            - Conflicts: EditChanges propertyvalue <> Data propertyvalue 
        - We let the UI do whatever it wants with this information. Example:
            - Selectively update the Data properties from NotificationChanges and EditChanges into Data
            - Update the UpdatedAt timestamp in Data from NotificationChanges so your SaveEditAsync() doesn't fail *** VERY IMPORTANT ***
            - Finalize the Edit 

** Effect of Notification on in-flight updates. **
When we finalize an Edit, we call SaveEditAsync(). If the State == Edit then this is calling an 
update on the service side. We use optimistic locking on the service side so it is possible that 
the update may fail if another client updated the record while we were editing it in our client.

If the notification created by the other client's edit arrives before we finalize our edit then 
the NotificationEditOption process (Cancel or Merge) kicks in. 

However, what happens if the Notification arrives after we Finalize our Edit (which makes the 
SaveEditAsymc() call) to do an update on the service side? We wait for the update to fail, 
swallow the error and proceed based on the NotificationEditOption.


** Using Notifications ** 
Add and initialize INotificationSvc NotificationSvc property to your implementing ViewModel. 
In your constructor, add a subscription:
    this.WhenAnyValue(x => x.NotificationSvc.Notification)
        .Where(x => x.PayloadId.Equals(Id)) // Remember we reflect Data.Id to ViewModel.Id by default
        .Subscribe(x => UpdateFromNotification(x.Payload));

 */

public enum LzItemViewModelState
{
    New,
    Edit,
    Current,
    Deleted
}

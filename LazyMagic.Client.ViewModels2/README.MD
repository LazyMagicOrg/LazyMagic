# ViewModel Guidelines

## General 
- All ViewModels should inherit from ReactiveObject. This implements INotifyPropertyChanged.
- All ViewModels should use ReactiveUI.Fody.Helpers where possible. [Reactive] [ObservableAsProperty]
- Always use a ViewModel, never directly hook up a View directly to the ClientSDK. This allows adding observability features in the ViewModel later on with little change to the Views. You can hook up a View to a Model through the ViewModel; e.g. make the Model a property of the ViewModel. (Direct Binding)
- Put all Persistence Methods, Model transformation logic in ViewModel
- Put non-trivial Model validation rules in ViewModel
- Models should implement INotifyPropertyChanged (LzViewModels inherits from ReactiveObject to achieve this)

## When to use Observability 
[Reactive] properties handle two-way binding to Model properties. This is used to bind View component elements to an instance of a Model in a ViewModel. 
If the Component inherits from ReactiveComponentBase<T> then the StateHasChanged() method will be called when the bound values are updated. In other words, 
you don't have to do anything special to get two-way binding to work with Models that implement INotifyPropertyChanged. 

[ObservableAsProperty] handle read properties that emit events that can be subscribed to by ViewModels and/or Components (both the ViewModel they are defined in and other ViewModels). Generally, it is better to implement most event handling at the ViewModel level for testability and simplicity. 

## ViewModel Classes 
This library provides a set of base classes that you can use to create your own ViewModels. 

### LzViewModelBase 
```
public abstract class LzViewModelBase: ReactiveObject, IDisposable
```
This ViewModel class is the base class for all ViewModels.


### ItemViewModel
These ViewModels represent a single item and provides a CRUDL interface to the item.
```
public abstract class LzItemViewModelBase<TDTO, TModel> : LzViewModelBase, ILzItemViewModel<TModel>
public abstract class LzItemViewModelNotificationsBase<TDTO, TModel> : LzItemViewModelBase<TDTO,TModel>, ILzItemViewModelNotificationsBase<TModel>
```

When using these classes, you will need to implement the abstract methods and properties. There are also methods and properties 
that are virtual, with default implementations, that you can override if you need to.
#### Virtual Properties 
```
public abstract string? Id { get; }	
public abstract long UpdatedAt { get; }
```

### ItemsViewModel
These ItemsViewModel classes represent a collection of ItemViewModels. 
```
public abstract class LzItemsViewModelBase<TVM, TDTO, TModel> : LzViewModelBase, INotifyCollectionChanged, ILzItemsViewModelBase<TVM, TDTO, TModel> 
public abstract class LzItemsViewModelNotificationsBase<TVM, TDTO, TModel> :  LzItemsViewModelBase<TVM, TDTO, TModel>,  INotifyCollectionChanged, ILzItemsViewModelNotificationsBase<TVM, TDTO, TModel>
```


## Service APIs 
When you create a ViewModel, you will need to provide a ServiceAPI. This is the API that is used to perform 
CRUDL operations. 

### LzItemViewModelBase 

DTO Access 
- _DTOCreateAsync(DTO)
- _DTOCreateIdAsync(Id,DTO)
- _DTOReadAsync(), this.Id to read the item 
- _DTOReadIdAsync(Id)
- _DTOUpdateAsync(DTO)
- _DTODeleteIdAsync(Id)
 

## Notes on manipulating Nested Classes
It is common to manipulate a document that contains nested data types. Our main approach to handling this situation is to create a class for that document that implements Create, Read, Update and Delete methods with the same signature as a DTO service interface. 

An example of when this approach is useful is when you load a DTO from a service and that DTO contains nested classes. If you want to create view models for the nested classes , you can create a new class that implements the same CRUDL interface as the DTO service and use that class when you create the nested view models.

If your database doesn't contain types with nested types, you use the DTO service directly. If you have nested types, you have two choices:
- Create a ViewModel for the type.
- Create a class that provides the Create, Read, Update and Delete methods for the nested classes.
 
## How to use the ViewModel Classes
ItemsViewModel manages a list of ItemViewModel instances.
ItemViewModel manages a single item.

### ItemViewModel 
We can create an ItemViewModel in three ways:
- Create a new() item. Usually used when we are creating a new item.
- Create a new item from a DTO. new(dto: value). Usually used when we have loaded an item through a DTO service.
- Create a new item from a Model. new(model: value). Raely used.

## ItemsViewModel
Normally we create an ItemsViewModel and then call ReadAsync() to load the items from a service. 
The ReadAsync() method will create a new ItemViewModel for each item in the list using the new(dto: value) constructor.

## Add, Edit
When we want to add a new item, we call the New() method. This will create a new ItemViewModel instance with the state set to New. This does not add the new item to the ItemsViewModel.ViewModels collection. If we subsequently Cancel the edit, the new item is discarded. If we Save the edit, the new item is added to the ItemsViewModel.ViewModels collection.

When we want to edit an item, we call the Edit() method. This will set the state to Edit and make a copy of the item data. If we subsequently Cancel the edit, the item data is restored from the copy. If we Save the edit, the item data is updated in the service.

## ViewModel StorageAPI Behaviors

ItemViewModel handles CRUDL operations for a single item.
ItemsViewModel handles Read operations for a collection of items.


StorageAPI.DTO States
Note that Data is of Type TModel.

|ViewModel|State|Method|Action|EndState|IsLoaded|
|---|---|---|---|---|
|ItemViewModel|na|new(dto: null, model: null)| Data = new().|New||
|ItemViewModel|na|new(dto: value)| dto.DeepClone(Data). | Current||
|ItemViewModel|na|new(model: value)| Data = value | Current||
|ItemViewModel|New|Create|UpdateData(_DTOCreateAsync(id,(TDTO)Data))|Current|True|
|ItemViewModel|any|Read|UpdateData(_DTOReadIdAsync(id)).|Current|True|
|ItemViewModel|Edit|Update|UpdateData(_DTOUpdateAsync((TDTO)Data))|Current|True|
|ItemViewModel|Current|Delete|_DTODeleteIdAsync(id)|Current|True|
|ItemViewModel|New|OpenEdit|MakeDataCopy()|New||
|ItemViewModel|Current|OpenEdit|MakeDataCopy()|Edit||
|ItemViewModel|New|SaveEdit|Create(id)|Current||
|ItemViewModel|Edit|SaveEdit|Update(id)|Current||
|ItemViewModel|New|CancelEdit|RestoreDataCopy()|New||
|ItemViewModel|Edit|CancelEdit|RestoreDataCopy()|Current||
|ItemsViewModel||New(dtos: null, models: null)| ViewModels.Count = 0 ||
|ItemsViewModel||New()| ViewModels = new()||
|ItemsViewModel||Read|_DTOReadListASync(). foreach(value in values) ViewModels.Add(new ItemViewModel(dto:value)||
|ItemsViewModel||Insert|||
|ItemsViewModel||Remove|||

Notes:
- MakeDataCopy() ```{_DataCopyJson = JsonConvert.SerializeObject((TDTO)Data);}``` Please note that only the DTO properties are copied. You can defeat this behavior by overloading the MakeDataCopy() method but you usually don't want to do that. Often, the additional properties are ItemsViewModels and you don't want to serialize ViewModels.
- RestoreFromDataCopy() ```{JsonConvert.PopulateObject(_DataCopyJson, Data);}```

   











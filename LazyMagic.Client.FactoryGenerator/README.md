# LazyMagic.Client.FactoryGenerator

This project generates factory class code for classes annotated with the [Factory] annotation.


## Using this Generator 
In the project you want to generate code for, add a reference to the LazyMagic.Generator project.
```<PackageReference Include="LazyMagic.Client.FactoryGenerator" Version="x.x.x" PrivateAssets="all" OutputItemType="Analyzer" />```
Note the OutputItemType attribute. This is required to make the generator work.

//Example: 

// Source Class
// Factory annotation specifies the class needs a DI factory 
// FactoryInject annotation specifies the parameter needs to be injected by the DI factory

//[Factory]
//public class YadaViewModel : LzItemViewModelNotificationsBase<Yada, YadaModel>
//{
//    public YadaViewModel(
//        [FactoryInject] IAuthProcess authProcess,
//        ISessionViewModel sessionViewModel,
//        ILzParentViewModel parentViewModel,
//        Yada Yada,
//        bool? isLoaded = null
//        ) : base(Yada, isLoaded)
//    {
//        ...
//    }
//	...
//}


// Generated Class - implements standard DI factory pattern
//public interface IYadaViewModelFactory
//{
//    YadaViewModel Create(
//        ISessionViewModel sessionViewModel,
//        ILzParentViewModel parentViewModel,
//        Yada item,
//        bool? isLoaded = null);
//}
//public class YadaViewModelFactory : IYadaViewModelFactory, ILzTransient
//{
//    public YadaViewModelFactory(IAuthProcess authProcess)
//    {
//        this.authProcess = authProcess;
//    }

//    private IAuthProcess authProcess;

//    public YadaViewModel Create(ISessionViewModel sessionViewModel, ILzParentViewModel parentViewModel, Yada item, bool? isLoaded = null)
//    {
//        return new YadaViewModel(
//            authProcess,
//            sessionViewModel,
//            parentViewModel,
//            item,
//            isLoaded);
//    }
//}

## Notes
The generator TargetFramework is 'netstandard2.0'. This is required to make the generator work.


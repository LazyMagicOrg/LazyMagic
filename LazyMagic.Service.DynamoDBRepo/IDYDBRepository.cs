namespace LazyMagic.Service.DynamoDBRepo;

public interface IDYDBRepository<T> : IDocumentRepo<T>
    where T : class, IItem, new()
{

}
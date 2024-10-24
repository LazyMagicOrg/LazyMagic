# ActionResult Cribsheet

```IActionResult``` is an interface that defines one method:
```
	Task ExecuteResultAsync(ActionContext context);
```
The MVC framework will call this method to execute the action result.

```ActionResult``` is an abstract class that represents the result of an action method. 
It is the base class for all action results. It implements the ```IActionResult``` interface.

```ObjectResult : ActionResult, IStatusCodeActionResult``` is a class that represents an action result that returns an object.
It is a concrete class that derives from ```ActionResult``` and has two important properties:
```
public object Value {get; set;}	
public int? StatusCode {get; set;} // satisfies the IStatusCodeActionResult interface
```	

```ActionResult<T>``` is a generic class that represents an action result that returns an object of type ```T```. It does
not implement ```IActionResult```. This class contains two proprties:
```
public T Value {get;}
public ActionResult Result {get;}	
```
Only one of these properties will be set. This is intentional to enforce a pattern where only a "good" result or "bad" result is 
possible. 


```ObjectResult : ActionResult, IStatusCodeActionResult``` is a class that represents an action result that returns an object.
It is a concrete class that derives from ```ActionResult``` and has two important properties:
```
public object Value {get; set;}
public int? StatusCode {get; set;} // satisfies the IStatusCodeActionResult interface
```	

Consider the following code:
```
[HttpGet 'Test']
public async Task<ActionResult<string>> GetTest()
{
	var result = await GetTestAsync();
	var innerResult = result.Result as ObjectResult;
	var innerStatusCode = innerResult is not null 
		? innerResult.StatusCode 
		: 500; // or whatever is appropriate for your application	
	return result;
}	

protected async Task<IActionResult<string>> GetTestAsync()
{
	var result = "Hello World";	
	return new ObjectResult(result.Value) { StatusCode = 200 };
}
```

Even though the ```GetTest``` method returns a ActionResutl<string> value, the MVC framework will unwrap the result and return 
the ObjectResult so the StatusCode gets sent back to the client. In the code above we see how we can reference the 
ObjectResult to get the object value and status code if needed. 

In most cases , this library just uses the ```ActionResult<T>``` class directly. However, in the case of the ListAsync methods,
se use ObjectResult so that we can return a 206 when a partial return result is required. In your OpenAPI spec, you need to 
specifically allow a 206 response from the endpoint or the 206 will be treated as an error. This is desierable behavior because 
the API tells the client what to expect.

Note that this doesn't replace the need for pagging interfaces in most cases.

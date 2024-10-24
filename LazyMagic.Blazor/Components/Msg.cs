namespace LazyMagic.Blazor;
using LazyMagic.Client.Base;
using Microsoft.AspNetCore.Components;
using System;

//<span class="static-content-message" accesskey="@Key">@message</span>

public class Msg : LzComponentBase<ILzMessages>
{ 
    [Parameter]
    public string Key { get; set; } = "";

    private MarkupString _content;

    protected override void OnParametersSet()
    {
        // Generate content based on the Key
        _content = GenerateContent();
    }

    private MarkupString GenerateContent()
    {
        // This is a simple example. You can make this more complex based on your needs.
        return this.Msg(Key);
    }

    private MarkupString message => (MarkupString)_viewModel!.Msg(Key);

    protected override void OnInitialized()
    {
        _viewModel = Messages;
        base.OnInitialized();
    }

    protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
    {
        base.BuildRenderTree(builder);

        // Build the render tree manually
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "class", "static-content-message");
        builder.AddContent(2, _content);
        builder.CloseElement();
    }
}

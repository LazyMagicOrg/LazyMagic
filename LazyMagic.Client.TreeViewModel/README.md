# LazyMagic.Client.TreeViewModel

This project contains a class LzTreeNodeViewModel.
LzTreeNodeViewModel is a recursive class that can be used to build a tree of nodes that can be navigated. 
Each node can be an item or a folder. 
Nodes usually map to LzItemViewModel class instances. 
Folders usuall map to LzItemsViewModel class instances.	

Annotations
[TreeNode]
Extends a class with the GetTreeNodeAsync method"
	public async Task<TreeItemViewModel> GetTreeNodeAsync() { ... } 
[TreeNodeName] 
[TreeNodePage]
[TreeNodeChild]
[TreeNodeParallelMax]
[TreeNodeIsFolder]

## Using this Generator 
In the project you want to generate code for, add a reference to the LazyMagic.Generator project.
```<ProjectReference Include="..\..\LazyMagicClient\LazyMagic.Client.TreeViewModel\LazyMagic.Client.TreeViewModel.csproj" OutputItemType="Analyzer" />```

Note the OutputItemType attribute. This is required to make the generator work.

## Notes
The generator TargetFramework is 'netstandard2.0'. This is required to make the generator work.


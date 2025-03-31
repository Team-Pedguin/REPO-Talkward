# Finder.MsBuild.Task

This package provides MSBuild tasks for finding and processing content from NuGet packages.

## FindNetStandardCompatibleContentViaNuGet

Finds content within NuGet packages that is compatible with .NET Standard frameworks.

### Recommended Usage

```xml
<FindNetStandardCompatibleContentViaNuGet
    NuGetPackageRoot="$(NuGetPackageRoot)"
    MaximumNetStandard="2.1" 
    Packages="@(PackageReference)">
  <Output TaskParameter="LibraryContentFiles" ItemName="AllLibraryContent" />
</FindNetStandardCompatibleContentViaNuGet>
```

#### Examples of Filtering

You may filter the content in your project after the task has run.

```xml
<!-- Filter public assets -->
<ItemGroup>
    <PublicLibraryContent Include="@(AllLibraryContent)"
                          Condition="'%(AllLibraryContent.IsPrivate)' != 'True'" />
</ItemGroup>
```

```xml
<!-- Filter private assets -->
<ItemGroup>
  <PrivateLibraryContent Include="@(AllLibraryContent)" 
                        Condition="'%(AllLibraryContent.IsPrivate)' == 'True'" />
</ItemGroup>
```

```xml
<!-- Remove private assets -->
<ItemGroup>
  <AllLibraryContent Remove="@(AllLibraryContent->WithMetadataValue('IsPrivate', 'True'))" />
</ItemGroup>
```
### Metadata Information

Each item in the `LibraryContentFiles` output includes the following metadata:

- `Package`: The package ID this file came from.
- `AssetType`: The type of asset, ie. `compile`, `runtime`, `contentfiles`, etc.
- `IsPrivate`: "True" if this asset is marked as private, "False" otherwise.
- `TargetFramework`: For framework-specific assets, the target framework.

### Understanding Asset Privacy

Asset privacy is determined based on:

1. The default asset privacy rules (build, analyzers, contentfiles are private by default)
2. The `PrivateAssets` metadata on the `PackageReference` item


// Extended by Martin Egli

BuildParameters.Tasks.DotNetCorePackTask = Task("DotNetCore-Pack")
    .IsDependentOn("DotNetCore-Build")
    .WithCriteria(() => BuildParameters.ShouldRunDotNetCorePack)
    .Does(() =>
{
    var projects = GetFiles(BuildParameters.SourceDirectoryPath + "/**/*.csproj")
        - GetFiles(BuildParameters.SourceDirectoryPath + "/tools/**/*.csproj")
        - GetFiles(BuildParameters.SourceDirectoryPath + "/**/*.Tests.csproj");

    var msBuildSettings = new DotNetCoreMSBuildSettings()
                            .WithProperty("Version", BuildParameters.Version.SemVersion)
                            .WithProperty("AssemblyVersion", BuildParameters.Version.Version)
                            .WithProperty("FileVersion",  BuildParameters.Version.Version)
                            .WithProperty("AssemblyInformationalVersion", BuildParameters.Version.InformationalVersion);

    if(!IsRunningOnWindows())
    {
        var frameworkPathOverride = new FilePath(typeof(object).Assembly.Location).GetDirectory().FullPath + "/";

        // Use FrameworkPathOverride when not running on Windows.
        Information("Pack will use FrameworkPathOverride={0} since not building on Windows.", frameworkPathOverride);
        msBuildSettings.WithProperty("FrameworkPathOverride", frameworkPathOverride);
    }

    var settings = new DotNetCorePackSettings {
        NoBuild = true,
        Configuration = BuildParameters.Configuration,
        OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
        MSBuildSettings = msBuildSettings,
        ArgumentCustomization = (args) => {
            if (BuildParameters.ShouldBuildNugetSourcePackage)
            {
                args.Append("--include-source");
            }
            return args;
        }
    };

    foreach (var project in projects)
    {
        DotNetCorePack(project.ToString(), settings);
    }
});

BuildParameters.Tasks.CreateNuGetPackageTask = Task("Create-Nuget-Package")
    .IsDependentOn("Clean")
    .IsDependentOn("CodeDocumentation")
    .Does(() =>
{
	try{
		Debug("Start Create-Nuget-Package");

		if(BuildParameters.NuSpecFilePath != null) {
			Debug("NuSpecFilePath: {0}", BuildParameters.NuSpecFilePath);
			Debug("NuSpecFilePath: {0}", BuildParameters.NuSpecFilePath);

			// Create packages.
			NuGetPack(BuildParameters.NuSpecFilePath, new NuGetPackSettings {
				Version = BuildParameters.Version.SemVersion,
				OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
//				BasePath = BuildParameters.Paths.Directories.PublishedLibraries.Combine(BuildParameters.NuSpecFilePath.GetFilenameWithoutExtension().ToString()),
                Symbols = true,
				IncludeReferencedProjects = true,
				Properties = new Dictionary<string, string>
				{
					{ "Configuration", "Release" }
				},
				NoPackageAnalysis = true
			});
		}
		else if(BuildParameters.NuSpecFilePaths != null && BuildParameters.NuSpecFilePaths.Any()) {
		Debug("NuSpecFilePaths: {0}", BuildParameters.NuSpecFilePaths);
			
//			Debug("NuSpecFilePaths: {0}", String.Join(" ,", BuildParameters.NuSpecFilePaths));
			foreach(var nuSpecFilePath in BuildParameters.NuSpecFilePaths)
			{
				// Create packages.
				NuGetPack(nuSpecFilePath, new NuGetPackSettings {
					Version = BuildParameters.Version.SemVersion,
					OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
//					BasePath = BuildParameters.Paths.Directories.PublishedLibraries.Combine(nuSpecFilePath.GetFilenameWithoutExtension().ToString()),
					Symbols = true,
					IncludeReferencedProjects = true,
					Properties = new Dictionary<string, string>
					{
						{ "Configuration", "Release" }
					},
					NoPackageAnalysis = true
				});
			}
		}
		else
		{
			throw new Exception("NuSpecFilePath or NuSpecFilePaths are not been set");
		}
	}
	finally{
		Debug("End Create-Nuget-Package");
	}
});

BuildParameters.Tasks.CreateNuGetPackagesTask = Task("Create-NuGet-Packages")
    .IsDependentOn("Clean")
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.NugetNuspecDirectory))
    .Does(() =>
{
    var nuspecFiles = GetFiles(BuildParameters.Paths.Directories.NugetNuspecDirectory + "/**/*.nuspec");

    EnsureDirectoryExists(BuildParameters.Paths.Directories.NuGetPackages);

    foreach(var nuspecFile in nuspecFiles)
    {
        // TODO: Addin the release notes
        // ReleaseNotes = BuildParameters.ReleaseNotes.Notes.ToArray(),

        if(DirectoryExists(BuildParameters.Paths.Directories.PublishedLibraries.Combine(nuspecFile.GetFilenameWithoutExtension().ToString())))
        {
            // Create packages.
            NuGetPack(nuspecFile, new NuGetPackSettings {
                Version = BuildParameters.Version.SemVersion,
                BasePath = BuildParameters.Paths.Directories.PublishedLibraries.Combine(nuspecFile.GetFilenameWithoutExtension().ToString()),
                OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
                Symbols = false,
                NoPackageAnalysis = true
            });

            continue;
        }

        if(DirectoryExists(BuildParameters.Paths.Directories.PublishedApplications.Combine(nuspecFile.GetFilenameWithoutExtension().ToString())))
        {
            // Create packages.
            NuGetPack(nuspecFile, new NuGetPackSettings {
                Version = BuildParameters.Version.SemVersion,
                BasePath = BuildParameters.Paths.Directories.PublishedApplications.Combine(nuspecFile.GetFilenameWithoutExtension().ToString()),
                OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
                Symbols = false,
                NoPackageAnalysis = true
            });

            continue;
        }

            // Create packages.
            NuGetPack(nuspecFile, new NuGetPackSettings {
                Version = BuildParameters.Version.SemVersion,
                OutputDirectory = BuildParameters.Paths.Directories.NuGetPackages,
                Symbols = false,
                NoPackageAnalysis = true
            });
    }
});

BuildParameters.Tasks.PublishMyGetPackagesTask = Task("Publish-MyGet-Packages")
    .IsDependentOn("Package")
    .WithCriteria(() => BuildParameters.ShouldPublishMyGet)
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.NuGetPackages) || DirectoryExists(BuildParameters.Paths.Directories.ChocolateyPackages))
    .Does(() =>
{
    if(BuildParameters.CanPublishToMyGet)
    {
        var nupkgFiles = GetFiles(BuildParameters.Paths.Directories.NuGetPackages + "/**/*.nupkg");

        foreach(var nupkgFile in nupkgFiles)
        {
            // Push the package.
            NuGetPush(nupkgFile, new NuGetPushSettings {
                Source = BuildParameters.MyGet.SourceUrl,
                ApiKey = BuildParameters.MyGet.ApiKey
            });
        }

        nupkgFiles = GetFiles(BuildParameters.Paths.Directories.ChocolateyPackages + "/**/*.nupkg");

        foreach(var nupkgFile in nupkgFiles)
        {
            // Push the package.
            NuGetPush(nupkgFile, new NuGetPushSettings {
                Source = BuildParameters.MyGet.SourceUrl,
                ApiKey = BuildParameters.MyGet.ApiKey
            });
        }
    }
    else
    {
        Warning("Unable to publish to MyGet, as necessary credentials are not available");
    }
})
.OnError(exception =>
{
    Error(exception.Message);
    Information("Publish-MyGet-Packages Task failed, but continuing with next Task...");
    publishingError = true;
});

BuildParameters.Tasks.PublishNuGetPackagesTask = Task("Publish-Nuget-Packages")
    .IsDependentOn("Package")
    .WithCriteria(() => BuildParameters.ForcePublishNuGet || BuildParameters.ShouldPublishNuGet)
    .WithCriteria(() => DirectoryExists(BuildParameters.Paths.Directories.NuGetPackages))
    .Does(() =>
{
    Information("Publish-Nuget-Packages");
    Information("Directory : " + BuildParameters.Paths.Directories.NuGetPackages);

    if(BuildParameters.CanPublishToNuGet)
    {
        var nupkgFiles = GetFiles(BuildParameters.Paths.Directories.NuGetPackages + "/**/*.nupkg");

        foreach(var nupkgFile in nupkgFiles)
        {
            // Push the package.
            NuGetPush(nupkgFile, new NuGetPushSettings {
                Source = BuildParameters.NuGet.SourceUrl,
                ApiKey = BuildParameters.NuGet.ApiKey
            });
        }
    }
    else
    {
        Warning("Unable to publish to NuGet, as necessary credentials are not available");
    }
})
.OnError(exception =>
{
    Error(exception.Message);
    Information("Publish-Nuget-Packages Task failed, but continuing with next Task...");
    publishingError = true;
});

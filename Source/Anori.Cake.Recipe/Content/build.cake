///////////////////////////////////////////////////////////////////////////////
// GLOBAL VARIABLES
///////////////////////////////////////////////////////////////////////////////

var publishingError = false;
var currentSupportedCakeVersionNumber = "0.36.0.0";

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup<BuildData>(context =>
{
    Information(Figlet(BuildParameters.Title));

    Information("Starting Setup...");

    if(BuildParameters.IsMasterBranch && (context.Log.Verbosity != Verbosity.Diagnostic)) {
        Information("Increasing verbosity to diagnostic.");
        context.Log.Verbosity = Verbosity.Diagnostic;
    }

    Information("Versioning");
    RequireTool(GitVersionTool, () => {
        BuildParameters.SetBuildVersion(
            BuildVersion.CalculatingSemanticVersion(
                context: Context
            )
        );
		SolutionInfo.CreateSolutionInfo(context: Context);
    });
	
//	var packageName = @"Anori.Cake.Recipe";
//	var packageList = NuGetList(packageName);
//	if (!packageList.Any())
//	{
//		Warning("No " + packageName + " package found!");
//	}
//	else if (packageList.Count() == 1)
//	{
//		var package = packageList.First();
//		if (package.Version != BuildMetaData.Version)
//		{
//			Warning("Package with version " + package.Version + " available!");
//		}
//		else
//		{
//			Debug("Package with same version " + package.Version + " found.");
//		}
//	}
//	else
//	{
//		Warning("Multible packages found!");
//		foreach(var package in packageList)
//		{
//			Warning("Found package {0}, version {1}", package.Name, package.Version);
//		}
//	}
//	

    Information("Building version {0} of " + BuildParameters.Title + " ({1}, {2}) using version {3} of Cake, and version {4} of Anorisoft.Cake.Recipe. (IsTagged: {5})",
        BuildParameters.Version.SemVersion,
        BuildParameters.Configuration,
        BuildParameters.Target,
        BuildParameters.Version.CakeVersion,
        BuildMetaData.Version,
        BuildParameters.IsTagged);

    if(BuildParameters.Version.CakeVersion != currentSupportedCakeVersionNumber)
    {
        throw new Exception(string.Format("Cake.Recipe currently only supports building projects using version {0} of Cake.  Please update your packages.config file (or whatever method is used to pin to a specific version of Cake) to use this version.", currentSupportedCakeVersionNumber));
    }

    return new BuildData(context);
});

Teardown(context =>
{
    Information("Starting Teardown...");

    if(context.Successful)
    {
        if(!BuildParameters.IsLocalBuild && !BuildParameters.IsPullRequest && BuildParameters.IsMainRepository && (BuildParameters.IsMasterBranch || ((BuildParameters.IsReleaseBranch || BuildParameters.IsHotFixBranch) && BuildParameters.ShouldNotifyBetaReleases)) && BuildParameters.IsTagged)
        {
            if(BuildParameters.CanPostToTwitter && BuildParameters.ShouldPostToTwitter)
            {
                SendMessageToTwitter();
            }

            if(BuildParameters.CanPostToGitter && BuildParameters.ShouldPostToGitter)
            {
                SendMessageToGitterRoom();
            }

            if(BuildParameters.CanPostToMicrosoftTeams && BuildParameters.ShouldPostToMicrosoftTeams)
            {
                SendMessageToMicrosoftTeams();
            }
        }
    }
    else
    {
        if(!BuildParameters.IsLocalBuild && BuildParameters.IsMainRepository)
        {
            if(BuildParameters.CanPostToSlack && BuildParameters.ShouldPostToSlack)
            {
                SendMessageToSlackChannel("Continuous Integration Build of " + BuildParameters.Title + " just failed :-(");
            }
        }
    }

    // Clear nupkg files from tools directory
    if(DirectoryExists(Context.Environment.WorkingDirectory.Combine("tools")))
    {
        Information("Deleting nupkg files...");
        var nupkgFiles = GetFiles(Context.Environment.WorkingDirectory.Combine("tools") + "/**/*.nupkg");
        DeleteFiles(nupkgFiles);
    }

    Information("Finished running tasks.");
});

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

BuildParameters.Tasks.ShowInfoTask = Task("Show-Info")
    .Does(() =>
{
    Information("Target: {0}", BuildParameters.Target);
    Information("Configuration: {0}", BuildParameters.Configuration);
    Information("PrepareLocalRelease: {0}", BuildParameters.PrepareLocalRelease);
    Information("ShouldDownloadMilestoneReleaseNotes: {0}", BuildParameters.ShouldDownloadMilestoneReleaseNotes);
    Information("ShouldDownloadFullReleaseNotes: {0}", BuildParameters.ShouldDownloadFullReleaseNotes);
    Information("IsLocalBuild: {0}", BuildParameters.IsLocalBuild);
    Information("IsPullRequest: {0}", BuildParameters.IsPullRequest);
    Information("IsMainRepository: {0}", BuildParameters.IsMainRepository);
    Information("IsMasterBranch: {0}", BuildParameters.IsMasterBranch);
    Information("IsReleaseBranch: {0}", BuildParameters.IsReleaseBranch);
    Information("IsHotFixBranch: {0}", BuildParameters.IsHotFixBranch);
    Information("IsTagged: {0}", BuildParameters.IsTagged);

    Information("Solution FilePath: {0}", MakeAbsolute((FilePath)BuildParameters.SolutionFilePath));
    Information("Solution DirectoryPath: {0}", MakeAbsolute((DirectoryPath)BuildParameters.SolutionDirectoryPath));
    Information("Source DirectoryPath: {0}", MakeAbsolute(BuildParameters.SourceDirectoryPath));
    Information("Build DirectoryPath: {0}", MakeAbsolute(BuildParameters.Paths.Directories.Build));
});

BuildParameters.Tasks.CleanTask = Task("Clean")
    .IsDependentOn("Show-Info")
    .IsDependentOn("Print-AppVeyor-Environment-Variables")
    .Does(() =>
{
    Information("Cleaning...");

    CleanDirectories(BuildParameters.Paths.Directories.ToClean);
});

BuildParameters.Tasks.RestoreTask = Task("Restore")
    .Does(() =>
{
    Information("Restoring {0}...", BuildParameters.SolutionFilePath);

    NuGetRestore(
        BuildParameters.SolutionFilePath,
        new NuGetRestoreSettings
        {
            Source = BuildParameters.NuGetSources
        });
});

BuildParameters.Tasks.DotNetCoreRestoreTask = Task("DotNetCore-Restore")
    .Does(() =>
{
    var msBuildSettings = new DotNetCoreMSBuildSettings()
                            .WithProperty("Version", BuildParameters.Version.SemVersion)
                            .WithProperty("AssemblyVersion", BuildParameters.Version.Version)
                            .WithProperty("FileVersion",  BuildParameters.Version.Version)
                            .WithProperty("AssemblyInformationalVersion", BuildParameters.Version.InformationalVersion);

    if(!IsRunningOnWindows())
    {
        var frameworkPathOverride = new FilePath(typeof(object).Assembly.Location).GetDirectory().FullPath + "/";

        // Use FrameworkPathOverride when not running on Windows.
        Information("Restore will use FrameworkPathOverride={0} since not building on Windows.", frameworkPathOverride);
        msBuildSettings.WithProperty("FrameworkPathOverride", frameworkPathOverride);
    }

    DotNetCoreRestore(BuildParameters.SolutionFilePath.FullPath, new DotNetCoreRestoreSettings
    {
        Sources = BuildParameters.NuGetSources,
        MSBuildSettings = msBuildSettings
    });
});

BuildParameters.Tasks.BuildTask = Task("Build")
    .IsDependentOn("Clean")
    .IsDependentOn("Restore")
    .Does<BuildData>(data => RequireTool(MSBuildExtensionPackTool, () => {
        Information("Building {0}", BuildParameters.SolutionFilePath);

        if(BuildParameters.IsRunningOnWindows)
        {
            var msbuildSettings = new MSBuildSettings()
				.WithProperty("Version", BuildParameters.Version.SemVersion)
				.WithProperty("AssemblyVersion", BuildParameters.Version.Version)
				.WithProperty("FileVersion",  BuildParameters.Version.Version)
				.WithProperty("AssemblyInformationalVersion", BuildParameters.Version.InformationalVersion)
                .SetPlatformTarget(ToolSettings.BuildPlatformTarget)
                .UseToolVersion(ToolSettings.BuildMSBuildToolVersion)
                .WithProperty("TreatWarningsAsErrors", BuildParameters.TreatWarningsAsErrors.ToString())
                .WithTarget("Build")
                .SetMaxCpuCount(ToolSettings.MaxCpuCount)
                .SetConfiguration(BuildParameters.Configuration)
                .WithLogger(
                    Context.Tools.Resolve("MSBuild.ExtensionPack.Loggers.dll").FullPath,
                    "XmlFileLogger",
                    string.Format(
                        "logfile=\"{0}\";invalidCharReplacement=_;verbosity=Detailed;encoding=UTF-8",
                        BuildParameters.Paths.Files.BuildLogFilePath)
                );

            MSBuild(BuildParameters.SolutionFilePath, msbuildSettings);

            // Parse warnings.
            var issues = ReadIssues(
                MsBuildIssuesFromFilePath(
                    BuildParameters.Paths.Files.BuildLogFilePath,
                    MsBuildXmlFileLoggerFormat),
                data.RepositoryRoot);

            Information("{0} MsBuild warnings are found.", issues.Count());
            data.AddIssues(issues);
        }
        else
        {
            var xbuildSettings = new XBuildSettings()
                .SetConfiguration(BuildParameters.Configuration)
                .WithTarget("Build")
                .WithProperty("TreatWarningsAsErrors", "true");

            XBuild(BuildParameters.SolutionFilePath, xbuildSettings);
        }

        if(BuildParameters.ShouldExecuteGitLink)
        {
            ExecuteGitLink();
        }

        CopyBuildOutput();
    }));


BuildParameters.Tasks.DotNetCoreBuildTask = Task("DotNetCore-Build")
    .IsDependentOn("Clean")
    .IsDependentOn("DotNetCore-Restore")
    .Does(() => {
        Information("Building {0}", BuildParameters.SolutionFilePath);

        var msBuildSettings = new DotNetCoreMSBuildSettings()
                            .WithProperty("Version", BuildParameters.Version.SemVersion)
                            .WithProperty("AssemblyVersion", BuildParameters.Version.Version)
                            .WithProperty("FileVersion",  BuildParameters.Version.Version)
                            .WithProperty("AssemblyInformationalVersion", BuildParameters.Version.InformationalVersion);

        if(!IsRunningOnWindows())
        {
            var frameworkPathOverride = new FilePath(typeof(object).Assembly.Location).GetDirectory().FullPath + "/";

            // Use FrameworkPathOverride when not running on Windows.
            Information("Build will use FrameworkPathOverride={0} since not building on Windows.", frameworkPathOverride);
            msBuildSettings.WithProperty("FrameworkPathOverride", frameworkPathOverride);
        }

        DotNetCoreBuild(BuildParameters.SolutionFilePath.FullPath, new DotNetCoreBuildSettings
        {
            Configuration = BuildParameters.Configuration,
            MSBuildSettings = msBuildSettings
        });

        if(BuildParameters.ShouldExecuteGitLink)
        {
            ExecuteGitLink();
        }

        CopyBuildOutput();
    });

public void CopyBuildOutput()
{
    Information("Copying build output...");
	foreach(var project in ParseSolution(BuildParameters.SolutionFilePath).Projects)
    {
		Information(project.Path.FullPath.ToString());
		Information("Type {0}", project.Type);
		if (project.Type == "{2150E333-8FDC-42A3-9474-1A3956D46DE8}")
		{
			continue;
		}
		Information("Input BuildPlatformTarget: {0}", ToolSettings.BuildPlatformTarget.ToString());
        var platformTarget = ToolSettings.BuildPlatformTarget == PlatformTarget.MSIL ? "AnyCPU" : ToolSettings.BuildPlatformTarget.ToString();
        Information("Using BuildPlatformTarget: {0}", platformTarget);
        
		var parsedProject = ParseProject(project.Path, BuildParameters.Configuration, platformTarget);
		Information("IsNetStandard: {0}", parsedProject.IsNetStandard);
 		Information("IsNetFramework: {0}", parsedProject.IsNetFramework);
 		Information("IsNetCore: {0}", parsedProject.IsNetCore);
	}
	
    foreach(var project in ParseSolution(BuildParameters.SolutionFilePath).Projects)
    {
        // There is quite a bit of duplication in this function, that really needs to be tidied Upload
		if (project.Type == "{2150E333-8FDC-42A3-9474-1A3956D46DE8}")
		{
			continue;
		}
        Information("Input BuildPlatformTarget: {0}", ToolSettings.BuildPlatformTarget.ToString());
        var platformTarget = ToolSettings.BuildPlatformTarget == PlatformTarget.MSIL ? "AnyCPU" : ToolSettings.BuildPlatformTarget.ToString();
        Information("Using BuildPlatformTarget: {0}", platformTarget);
        var parsedProject = ParseProject(project.Path, BuildParameters.Configuration, platformTarget);
		
        if(project.Path.FullPath.ToLower().Contains("wixproj"))
        {
            Warning("Skipping wix project");
            continue;
        }

        if(project.Path.FullPath.ToLower().Contains("shproj"))
        {
            Warning("Skipping shared project");
            continue;
        }

		// OutputPath
        if(parsedProject.OutputPaths == null || !parsedProject.OutputPaths.Any() || parsedProject.RootNameSpace == null || parsedProject.OutputType == null)
        {
            if(parsedProject.OutputPaths == null || !parsedProject.OutputPaths.Any())
			{
				Information("OutputPath: null");
			}
			else
			{
				foreach(var outputPath in parsedProject.OutputPaths)
				{
					Information("OutputPath: {0}", outputPath);
				}
			}
			
			Information("RootNameSpace: {0}", parsedProject.RootNameSpace);
            Information("OutputType: {0}", parsedProject.OutputType);
            throw new Exception(string.Format("Unable to parse project file correctly: {0}", project.Path));
        }

        // If the project is an exe, then simply copy all of the contents to the correct output folder
        if(!parsedProject.IsLibrary())
        {
            Information("Project has an output type of exe: {0}", parsedProject.RootNameSpace);
            var outputFolder = BuildParameters.Paths.Directories.PublishedApplications.Combine(parsedProject.RootNameSpace);
            EnsureDirectoryExists(outputFolder);

            // If .NET SDK project, copy using dotnet publish for each target framework
            // Otherwise just copy
            if(parsedProject.IsVS2017ProjectFormat)
            {
                var msBuildSettings = new DotNetCoreMSBuildSettings()
                            .WithProperty("Version", BuildParameters.Version.SemVersion)
                            .WithProperty("AssemblyVersion", BuildParameters.Version.Version)
                            .WithProperty("FileVersion",  BuildParameters.Version.Version)
                            .WithProperty("AssemblyInformationalVersion", BuildParameters.Version.InformationalVersion);

                if(!IsRunningOnWindows())
                {
                    var frameworkPathOverride = new FilePath(typeof(object).Assembly.Location).GetDirectory().FullPath + "/";

                    // Use FrameworkPathOverride when not running on Windows.
                    Information("Publish will use FrameworkPathOverride={0} since not building on Windows.", frameworkPathOverride);
                    msBuildSettings.WithProperty("FrameworkPathOverride", frameworkPathOverride);
                }

                foreach(var targetFramework in parsedProject.NetCore.TargetFrameworks)
                {
                    DotNetCorePublish(project.Path.FullPath, new DotNetCorePublishSettings {
                        OutputDirectory = outputFolder.Combine(targetFramework),
                        Framework = targetFramework,
                        Configuration = BuildParameters.Configuration,
                        MSBuildSettings = msBuildSettings
                    });
                }
            }
            else
            {
			
				foreach(var outputPath in parsedProject.OutputPaths)
				{
					CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
				}
            }

            continue;
        }

        var isxUnitTestProject = false;
        var ismsTestProject = false;
        var isFixieProject = false;
        var isNUnitProject = false;

        // Now we need to test for whether this is a unit test project.  Currently, this is only testing for XUnit Projects.
        // It needs to be extended to include others, i.e. NUnit, MSTest, and VSTest
        // If this is found, move the output to the unit test folder, otherwise, simply copy to normal output folder

        ICollection<ProjectAssemblyReference> references = null;
        if(!BuildParameters.IsDotNetCoreBuild)
        {
            Information("Not a .Net Core Build");
            references = parsedProject.References;
        }
        else
        {
            Information("Is a .Net Core Build");
            references = new List<ProjectAssemblyReference>();
        }

        foreach(var reference in references)
        {
            Verbose("Reference Include: {0}", reference.Include);
            var referenceInclude = reference.Include.ToLower();
            if(referenceInclude.Contains("xunit.core"))
            {
                isxUnitTestProject = true;
                break;
            }
            else if(referenceInclude.Contains("unittestframework") || referenceInclude.Contains("visualstudio.testplatform"))
            {
                ismsTestProject = true;
                break;
            }
            else if(referenceInclude.Contains("fixie"))
            {
                isFixieProject = true;
                break;
            }
            else if(referenceInclude.Contains("nunit.framework"))
            {
                isNUnitProject = true;;
                break;
            }
        }

        ICollection<PackageReference> packageReferences = parsedProject.PackageReferences;

        foreach(var reference in packageReferences)
        {
            Verbose("PackageReference Name: {0}", reference.Name);
            var referenceName = reference.Name.ToLower();
            if(referenceName.Contains("xunit") || referenceName.Contains("xunit.runner.visualstudio"))
            {
                isxUnitTestProject = true;
                break;
            }
            else if(referenceName.Contains("mstest.testframework") || referenceName.Contains("mstest.testadapter"))
            {
                ismsTestProject = true;
                break;
            }
            else if(referenceName.Contains("fixie"))
            {
                isFixieProject = true;
                break;
            }
            else if(referenceName.Contains("nunit") || referenceName.Contains("nunit3testadapter"))
            {
                isNUnitProject = true;;
                break;
            }
        }

        if(parsedProject.IsLibrary() && isxUnitTestProject)
        {
            Information("Project has an output type of library and is an xUnit Test Project: {0}", parsedProject.RootNameSpace);
            var outputFolder = BuildParameters.Paths.Directories.PublishedxUnitTests.Combine(parsedProject.RootNameSpace);
            EnsureDirectoryExists(outputFolder);
			foreach(var outputPath in parsedProject.OutputPaths)
			{
				CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
			}
			continue;
        }
        else if(parsedProject.IsLibrary() && ismsTestProject)
        {
            // We will use vstest.console.exe by default for MSTest Projects
            Information("Project has an output type of library and is an MSTest Project: {0}", parsedProject.RootNameSpace);
            var outputFolder = BuildParameters.Paths.Directories.PublishedVSTestTests.Combine(parsedProject.RootNameSpace);
            EnsureDirectoryExists(outputFolder);
			foreach(var outputPath in parsedProject.OutputPaths)
            {
				CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
			}
            continue;
        }
        else if(parsedProject.IsLibrary() && isFixieProject)
        {
            Information("Project has an output type of library and is a Fixie Project: {0}", parsedProject.RootNameSpace);
            var outputFolder = BuildParameters.Paths.Directories.PublishedFixieTests.Combine(parsedProject.RootNameSpace);
            EnsureDirectoryExists(outputFolder);
			foreach(var outputPath in parsedProject.OutputPaths)
            {
				CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
            }
			continue;
        }
        else if(parsedProject.IsLibrary() && isNUnitProject)
        {
            Information("Project has an output type of library and is a NUnit Test Project: {0}", parsedProject.RootNameSpace);
            var outputFolder = BuildParameters.Paths.Directories.PublishedNUnitTests.Combine(parsedProject.RootNameSpace);
            EnsureDirectoryExists(outputFolder);
			foreach(var outputPath in parsedProject.OutputPaths)
			{
				CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
			}
			continue;
        }
        else
        {
            Information("Project has an output type of library: {0}", parsedProject.RootNameSpace);

            // If .NET SDK project, copy for each output path
            // Otherwise just copy
            if(parsedProject.IsVS2017ProjectFormat)
            {
                foreach(var outputPath in parsedProject.OutputPaths)
                {
                    var outputFolder = BuildParameters.Paths.Directories.PublishedLibraries.Combine(parsedProject.RootNameSpace).Combine(outputPath.GetDirectoryName());
                    EnsureDirectoryExists(outputFolder);
                    Information(outputPath);
                    CopyFiles(GetFiles(outputPath + "/**/*"), outputFolder, true);
                }
            }
            else
            {
                foreach(var outputPath in parsedProject.OutputPaths)
                {
					var outputFolder = BuildParameters.Paths.Directories.PublishedLibraries.Combine(parsedProject.RootNameSpace);
					EnsureDirectoryExists(outputFolder);
					Information(outputPath.FullPath);
					CopyFiles(GetFiles(outputPath.FullPath + "/**/*"), outputFolder, true);
				}
			}
            continue;
        }
    }
}

BuildParameters.Tasks.PackageTask = Task("Package")
    .IsDependentOn("Export-Release-Notes");

BuildParameters.Tasks.DefaultTask = Task("Default")
    .IsDependentOn("Package");

BuildParameters.Tasks.AppVeyorTask = Task("AppVeyor")
    .IsDependentOn("Upload-AppVeyor-Artifacts")
    .IsDependentOn("Publish-MyGet-Packages")
    .IsDependentOn("Publish-Nuget-Packages")
    .IsDependentOn("Publish-GitHub-Release")
    .IsDependentOn("Publish-Documentation")
    .Finally(() =>
{
    if(publishingError)
    {
        throw new Exception("An error occurred during the publishing of " + BuildParameters.Title + ".  All publishing tasks have been attempted.");
    }
});

BuildParameters.Tasks.UploadCoverageReportTask = Task("Upload-Coverage-Report")
  .IsDependentOn("Upload-Coveralls-Report")
  .IsDependentOn("Upload-Codecov-Report");

BuildParameters.Tasks.ReleaseNotesTask = Task("ReleaseNotes")
  .IsDependentOn("Create-Release-Notes");

BuildParameters.Tasks.ClearCacheTask = Task("ClearCache")
  .IsDependentOn("Clear-AppVeyor-Cache");

BuildParameters.Tasks.PreviewTask = Task("Preview")
  .IsDependentOn("Preview-Documentation");

BuildParameters.Tasks.PublishDocsTask = Task("PublishDocs")
    .IsDependentOn("Force-Publish-Documentation");

///////////////////////////////////////////////////////////////////////////////
// EXECUTION
///////////////////////////////////////////////////////////////////////////////

public Builder Build
{
    get
    {
        return new Builder(target => RunTarget(target));
    }
}

public class Builder
{
    private Action<string> _action;

    public Builder(Action<string> action)
    {
        _action = action;
    }

    public void Run()
    {
        BuildParameters.IsDotNetCoreBuild = false;
        BuildParameters.IsNuGetBuild = false;
        SetupTasks(BuildParameters.IsDotNetCoreBuild);
        _action(BuildParameters.Target);
    }

    public void RunDotNetCore()
    {
        BuildParameters.IsDotNetCoreBuild = true;
        BuildParameters.IsNuGetBuild = false;
		SetupTasks(BuildParameters.IsDotNetCoreBuild);
		_action(BuildParameters.Target);
    }

    public void RunNuGet()
    {
        BuildParameters.IsDotNetCoreBuild = false;
        BuildParameters.IsNuGetBuild = true;
		SetupTasks(BuildParameters.IsDotNetCoreBuild);
		BuildParameters.Tasks.PackageTask.IsDependentOn("Create-NuGet-Package");
		_action(BuildParameters.Target);
    }
	
	public void RunDotNetCoreNuGet()
    {
        BuildParameters.IsDotNetCoreBuild = true;
        BuildParameters.IsNuGetBuild = true;
		SetupTasks(BuildParameters.IsDotNetCoreBuild);
		_action(BuildParameters.Target);
    }


    private static void SetupTasks(bool isDotNetCoreBuild)
    {
        var prefix = isDotNetCoreBuild ? "DotNetCore-" : "";
        BuildParameters.Tasks.CreateNuGetPackagesTask.IsDependentOn(prefix + "Build");
        BuildParameters.Tasks.CreateChocolateyPackagesTask.IsDependentOn(prefix + "Build");
        BuildParameters.Tasks.TestTask.IsDependentOn(prefix + "Build");
        BuildParameters.Tasks.DupFinderTask.IsDependentOn(prefix + "Build");
        BuildParameters.Tasks.InspectCodeTask.IsDependentOn(prefix + "Build");
        BuildParameters.Tasks.PackageTask.IsDependentOn("Analyze");
        BuildParameters.Tasks.PackageTask.IsDependentOn("Test");
        BuildParameters.Tasks.PackageTask.IsDependentOn("Create-NuGet-Packages");
        BuildParameters.Tasks.PackageTask.IsDependentOn("Create-Chocolatey-Packages");
        BuildParameters.Tasks.UploadCodecovReportTask.IsDependentOn("Test");
        BuildParameters.Tasks.UploadCoverallsReportTask.IsDependentOn("Test");
        BuildParameters.Tasks.AppVeyorTask.IsDependentOn("Upload-Coverage-Report");
        BuildParameters.Tasks.AppVeyorTask.IsDependentOn("Publish-Chocolatey-Packages");
        BuildParameters.Tasks.InstallReportGeneratorTask.IsDependentOn(prefix + "Build");

        if (!isDotNetCoreBuild)
        {
            if (BuildParameters.TransifexEnabled)
            {
                BuildParameters.Tasks.BuildTask.IsDependentOn("Transifex-Pull-Translations");
            }
            BuildParameters.Tasks.TestTask.IsDependentOn("Test-NUnit");
            BuildParameters.Tasks.TestTask.IsDependentOn("Test-xUnit");
            BuildParameters.Tasks.TestTask.IsDependentOn("Test-MSTest");
            BuildParameters.Tasks.TestTask.IsDependentOn("Test-VSTest");
            BuildParameters.Tasks.TestTask.IsDependentOn("Test-Fixie");
            BuildParameters.Tasks.InstallOpenCoverTask.IsDependentOn("Install-ReportUnit");
        }
        else
        {
            if (BuildParameters.TransifexEnabled)
            {
                BuildParameters.Tasks.DotNetCoreBuildTask.IsDependentOn("Transifex-Pull-Translations");
            }
            BuildParameters.Tasks.TestTask.IsDependentOn(prefix + "Test");
            BuildParameters.Tasks.InstallOpenCoverTask.IsDependentOn("Install-ReportGenerator");
            BuildParameters.Tasks.PackageTask.IsDependentOn(prefix + "Pack");
        }
    }
}

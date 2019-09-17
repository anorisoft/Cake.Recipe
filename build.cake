//#load nuget:https://www.myget.org/F/cake-contrib/api/v2?package=Cake.Recipe&prerelease
#load nuget:?package=Cake.Recipe&version=1.1.0

Environment.SetVariableNames();


BuildParameters.SetParameters(
	context: Context,
	buildSystem: BuildSystem,
	sourceDirectoryPath: "./Source",
	title: "Anori.Cake.Recipe",
//	forcePublishNuGet: true,
	repositoryOwner: "anorisoft",
	repositoryName: "Cake.Recipe",
	appVeyorAccountName: "anorisoft",
	shouldRunGitVersion: true, 
	nuspecFilePath: "./Source/Anori.Cake.Recipe/Anori.Cake.Recipe.nuspec"
);

BuildParameters.PrintParameters(Context);

ToolSettings.SetToolSettings(context: Context);

BuildParameters.Tasks.CleanTask
    .IsDependentOn("Generate-Version-File");

Task("Generate-Version-File")
    .Does(() => {
        var buildMetaDataCodeGen = TransformText(@"
        public class BuildMetaData
        {
            public static string Date { get; } = ""<%date%>"";
            public static string Version { get; } = ""<%version%>"";
        }",
        "<%",
        "%>"
        )
   .WithToken("date", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"))
   .WithToken("version", BuildParameters.Version.SemVersion)
   .ToString();

    System.IO.File.WriteAllText(
        "./Source/Anori.Cake.Recipe/Content/version.cake",
        buildMetaDataCodeGen
        );
    });

//Task("Run-Local-Integration-Tests")
//    .IsDependentOn("Default")
//    .Does(() => {
//    CakeExecuteScript("./test.cake",
//        new CakeSettings {
//            Arguments = new Dictionary<string, string>{
//                { "recipe-version", BuildParameters.Version.SemVersion },
//                { "verbosity", Context.Log.Verbosity.ToString("F") }
//            }});
//});

//BuildParameters.Tasks.PackageTask
//	.IsDependentOn("Publish-Nuget-Packages")
//	.Does(() => {});


Build.RunNuGet();

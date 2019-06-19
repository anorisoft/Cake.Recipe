// -----------------------------------------------------------------------
//  <copyright file="solutioninfo.cake" company="Anori Soft">
//      Copyright (c) Anori Soft Martin Egli. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
// Setup and Update SolutionInfo.cs
//

public class SolutionInfo
{
	public static void CreateSolutionInfo(ICakeContext context)
	{
		if (context.DirectoryExists(BuildParameters.SourceDirectoryPath)){
		var file = "./SolutionInfo.cs";
		var version = BuildParameters.Version.SemVersion;
		var assemblyVersion = BuildParameters.Version.AssemblyVersion;
		var fileVersion = BuildParameters.Version.AssemblyFileVersion ?? BuildParameters.Version.Version;
		var informationalVersion = BuildParameters.Version.InformationalVersion;
		var product = BuildParameters.Title.Replace(".", " ");
		var copyright = BuildParameters.Copyright;
		
		var solutionInfoFile = BuildParameters.SourceDirectoryPath.CombineWithFilePath(file);
		context.Information("SolutionInfo File Path: " + BuildParameters.Paths.Files.SolutionInfoFilePath ?? solutionInfoFile);
		
		context.CreateAssemblyInfo(solutionInfoFile, new AssemblyInfoSettings {
			Product = product,
			Version = assemblyVersion,
			FileVersion = fileVersion,
			InformationalVersion = informationalVersion,
			Copyright = copyright
		});
		}
	}
}
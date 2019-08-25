///////////////////////////////////////////////////////////////////////////////
// DocFx Pdf Helper
///////////////////////////////////////////////////////////////////////////////
// Author Martin Egli
//
///////////////////////////////////////////////////////////////////////////////
	Debug("DocFxHelper");



void SetupDocFxEnvironment(){
	Debug("SetupDocFxEnvironment");
	RequireTool(WkHtmlToPdfTool, () => 
	{
		var path = EnvironmentVariable("PATH");
		if (path == null)
		{
			Error("Unknown Environment Variable PATH");
			return;
		}
		
		Debug(path);
		var resolve = Context.Tools.Resolve("wkhtmltopdf.exe");
		if (resolve == null)
		{
			Error("Unknown Tools WkHtmlToPdfPath");
			return;
		}
		
		var wkHtmlToPdfPath = new FilePath(resolve.FullPath)?
			.GetDirectory()?
			.FullPath;
		if (wkHtmlToPdfPath == null)
		{
			Error("Unknown Tools WkHtmlToPdfPath");
			return;
		}
		
		System.Environment.SetEnvironmentVariable("PATH", path + ";" + wkHtmlToPdfPath);
		path = EnvironmentVariable("PATH");
		Debug(path);
	});
}

///////////////////////////////////////////////////////////////////////////////
// TASK DEFINITIONS
///////////////////////////////////////////////////////////////////////////////

Task("CodeDocumentation")
	.Does(() => 
	{
		SetupDocFxEnvironment();
		RequireTool(DocFxTool, () => 
		{
			var documentationDirectoryPath = MakeAbsolute(BuildParameters.DocumentationDirectoryPath);
			Debug("Documentation DirectoryPath: {0}", documentationDirectoryPath);
//			RunMarkdownlintNodeJsForDirectory(@"./Documentation");
			var pdfDocFxFile = documentationDirectoryPath.CombineWithFilePath("docfx_pdf.json");
//			var pdfDocFxFile = @"./Documentation/docfx_pdf.json";
			Debug("Pdf DocFxFile: {0}", pdfDocFxFile);
			DocFxMetadata(pdfDocFxFile);
			DocFxPdf(pdfDocFxFile, 
				new DocFxPdfSettings(){
					OutputPath = MakeAbsolute(BuildParameters.Paths.Directories.PublishedDocumentation)
				}
			);
		}
		);
	});

///////////////////////////////////////////////////////////////////////////////
// Setup
///////////////////////////////////////////////////////////////////////////////

//SetupDocFxEnvironment();
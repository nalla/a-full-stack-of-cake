///////////////////////////////////////////////////////////////////////////
// This is the main build file.
//
// The following targets are the prefered entrypoints:
// * Build
// * Publish
//
// You can call these targets by using the bootstrapper powershell script
// next to this file: ./build -target <target>
///////////////////////////////////////////////////////////////////////////

///////////////////////////////////////////////////////////////////////////
// Load additional cake addins
///////////////////////////////////////////////////////////////////////////
#addin Cake.Json
#addin Cake.Endpoint
#addin Cake.Npm
#addin Cake.DoInDirectory

///////////////////////////////////////////////////////////////////////////
// Load additional tools
///////////////////////////////////////////////////////////////////////////
#tool GitVersion.CommandLine
#tool OctopusTools

///////////////////////////////////////////////////////////////////////////
// Commandline argument handling
///////////////////////////////////////////////////////////////////////////
var target = Argument( "target", "Default" );
var configuration = Argument( "configuration", "Release" );
var framework = Argument( "framework", "netcoreapp1.1" );

///////////////////////////////////////////////////////////////////////////
// Project definition
///////////////////////////////////////////////////////////////////////////
FilePath webApiProject = "./src/webapi/webapi.csproj";
FilePath webApiTestProject = "./src/webapi.test/webapi.test.csproj";
DirectoryPath webAppProject = "./src/webapp";

///////////////////////////////////////////////////////////////////////////
// Constants, initial variables
///////////////////////////////////////////////////////////////////////////
DirectoryPath artifacts = "./artifacts";
GitVersion version = GitVersion();
var endpoints = DeserializeJsonFromFile<IEnumerable<Endpoint>>( "./endpoints.json" );

///////////////////////////////////////////////////////////////////////////
// Task: Clean
///////////////////////////////////////////////////////////////////////////
Task( "Clean" )
	.Does( () =>
{
	// clean artifacts folder
	CleanDirectory( artifacts );

	// clean webapp output folder
	CleanDirectory( webAppProject.Combine( "dist" ) );

	// clean webapi output folders
	CleanDirectories( "./src/**/bin/" + configuration );
	CleanDirectories( "./src/**/obj" );
} );

///////////////////////////////////////////////////////////////////////////
// Task: Restore
///////////////////////////////////////////////////////////////////////////
Task( "Restore" )
	.Does( () =>
{
	// run dotnet restore for webapi projects
	DotNetCoreRestore( webApiProject.FullPath );
	DotNetCoreRestore( webApiTestProject.FullPath );

	// run npm install for webapp project
	DoInDirectory( webAppProject, () =>
	{
		NpmInstall();
	} );
} );

///////////////////////////////////////////////////////////////////////////
// Task: Build
///////////////////////////////////////////////////////////////////////////
Task( "Build" )
	.IsDependentOn( "Clean" )
	.IsDependentOn( "Restore" )
	.Does( () =>
{
	// run dotnet publish to build webapi output
	DotNetCorePublish( webApiProject.FullPath, new DotNetCorePublishSettings
	{
		Configuration = configuration,
		VersionSuffix = version.PreReleaseTag
	} );

	// run npm build to build bundled and minified webapp output
	DoInDirectory( webAppProject, () =>
	{
		NpmRunScript( "build" );
	} );
} );

///////////////////////////////////////////////////////////////////////////
// Task: Test
///////////////////////////////////////////////////////////////////////////
Task( "Test" )
	.IsDependentOn( "Build" )
	.Does( () =>
{
	// run webapi tests via dotnet test
	DotNetCoreTest( webApiTestProject.FullPath );

	// run webapp tests via npm test
	DoInDirectory( webAppProject, () =>
	{
		NpmRunScript( "test" );
	} );
} );

///////////////////////////////////////////////////////////////////////////
// Task: Publish
///////////////////////////////////////////////////////////////////////////
Task( "Publish" )
	.IsDependentOn( "Test" )
	.Does( () =>
{
	// orchestrate package contents for octopus packages
	EndpointCreate( endpoints, new EndpointCreatorSettings()
	{
		TargetRootPath = artifacts.Combine( "publish" ).FullPath,
		TargetPathPostFix = "." + version.NuGetVersion,
		BuildConfiguration = configuration,
		ZipTargetPath = false
	} );

	// iterate endpoints, postfix resulting packages and run octo pack
	foreach( var endpoint in endpoints )
	{
		var publishPath = artifacts.Combine( "publish" ).Combine( endpoint.Id + "." + version.NuGetVersion );
		var packageId = endpoint.Id + ".Deploy";

		OctoPack( packageId, new OctopusPackSettings
		{
			BasePath = publishPath,
			Description = endpoint.Id,
			Title = packageId,
			OutFolder = artifacts.Combine( "nuget" ),
			Version = version.NuGetVersion
		} );
	}
} );

///////////////////////////////////////////////////////////////////////////
// Task: Run
///////////////////////////////////////////////////////////////////////////
Task( "Run" )
	.IsDependentOn( "Restore" )
	.Does( () =>
{
	// start dotnet run task
	var webApi = System.Threading.Tasks.Task.Factory.StartNew( () =>
	{
		DotNetCoreRun( webApiProject.GetFilename().ToString(), null, new DotNetCoreRunSettings
		{
			WorkingDirectory = webApiProject.GetDirectory().FullPath
		} );
	} );

	// start npm run task
	var webApp = System.Threading.Tasks.Task.Factory.StartNew( () =>
	{
		NpmRunScript( new NpmRunScriptSettings
		{
			ScriptName = "start",
			WorkingDirectory = webAppProject.FullPath
		} );
	} );
	System.Threading.Tasks.Task.WaitAll( webApi, webApp );
} );

Task( "Default" )
	.IsDependentOn( "Build" );

RunTarget( target );

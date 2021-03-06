﻿using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.Cli.Build.Framework;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.CoreSetup.Test.HostActivation.MultilevelSharedFxLookup
{
    public class GivenThatICareAboutMultilevelSharedFxLookup : IDisposable
    {
        private const string SystemCollectionsImmutableFileVersion = "88.2.3.4";
        private const string SystemCollectionsImmutableAssemblyVersion = "88.0.1.2";

        private RepoDirectoriesProvider RepoDirectories;
        private TestProjectFixture PreviouslyBuiltAndRestoredPortableTestProjectFixture;

        private string _currentWorkingDir;
        private string _userDir;
        private string _executableDir;
        private string _globalDir;
        private string _cwdSharedFxBaseDir;
        private string _cwdSharedUberFxBaseDir;
        private string _userSharedFxBaseDir;
        private string _userSharedUberFxBaseDir;
        private string _exeSharedFxBaseDir;
        private string _exeSharedUberFxBaseDir;
        private string _globalSharedFxBaseDir;
        private string _globalSharedUberFxBaseDir;
        private string _builtSharedFxDir;
        private string _builtSharedUberFxDir;

        private string _cwdSelectedMessage;
        private string _userSelectedMessage;
        private string _exeSelectedMessage;
        private string _globalSelectedMessage;

        private string _cwdFoundUberFxMessage;
        private string _userFoundUberFxMessage;
        private string _exeFoundUberFxMessage;
        private string _globalFoundUberFxMessage;

        private string _sharedFxVersion;
        private string _multilevelDir;
        private string _builtDotnet;
        private string _hostPolicyDllName;

        public GivenThatICareAboutMultilevelSharedFxLookup()
        {
            // From the artifacts dir, it's possible to find where the sharedFrameworkPublish folder is. We need
            // to locate it because we'll copy its contents into other folders
            string artifactsDir = Environment.GetEnvironmentVariable("TEST_ARTIFACTS");
            _builtDotnet = Path.Combine(artifactsDir, "sharedFrameworkPublish");

            // The dotnetMultilevelSharedFxLookup dir will contain some folders and files that will be
            // necessary to perform the tests
            string baseMultilevelDir = Path.Combine(artifactsDir, "dotnetMultilevelSharedFxLookup");
            _multilevelDir = SharedFramework.CalculateUniqueTestDirectory(baseMultilevelDir);

            // The three tested locations will be the cwd, the user folder and the exe dir. Both cwd and exe dir
            // are easily overwritten, so they will be placed inside the multilevel folder. The actual user location will
            // be used during tests
            _currentWorkingDir = Path.Combine(_multilevelDir, "cwd");
            _userDir = Path.Combine(_multilevelDir, "user");
            _executableDir = Path.Combine(_multilevelDir, "exe");
            _globalDir = Path.Combine(_multilevelDir, "global");

            RepoDirectories = new RepoDirectoriesProvider(builtDotnet: _executableDir);

            // SharedFxBaseDirs contain all available version folders
            _cwdSharedFxBaseDir = Path.Combine(_currentWorkingDir, "shared", "Microsoft.NETCore.App");
            _userSharedFxBaseDir = Path.Combine(_userDir, ".dotnet", RepoDirectories.BuildArchitecture, "shared", "Microsoft.NETCore.App");
            _exeSharedFxBaseDir = Path.Combine(_executableDir, "shared", "Microsoft.NETCore.App");
            _globalSharedFxBaseDir = Path.Combine(_globalDir, "shared", "Microsoft.NETCore.App");

            _cwdSharedUberFxBaseDir = Path.Combine(_currentWorkingDir, "shared", "Microsoft.UberFramework");
            _userSharedUberFxBaseDir = Path.Combine(_userDir, ".dotnet", RepoDirectories.BuildArchitecture, "shared", "Microsoft.UberFramework");
            _exeSharedUberFxBaseDir = Path.Combine(_executableDir, "shared", "Microsoft.UberFramework");
            _globalSharedUberFxBaseDir = Path.Combine(_globalDir, "shared", "Microsoft.UberFramework");

            // Create directories. It's necessary to copy the entire publish folder to the exe dir because
            // we'll need to build from it. The CopyDirectory method automatically creates the dest dir
            Directory.CreateDirectory(_cwdSharedFxBaseDir);
            Directory.CreateDirectory(_userSharedFxBaseDir);
            Directory.CreateDirectory(_globalSharedFxBaseDir);
            Directory.CreateDirectory(_cwdSharedUberFxBaseDir);
            Directory.CreateDirectory(_userSharedUberFxBaseDir);
            Directory.CreateDirectory(_globalSharedUberFxBaseDir);
            SharedFramework.CopyDirectory(_builtDotnet, _executableDir);

            //Copy dotnet to global directory
            File.Copy(Path.Combine(_builtDotnet, $"dotnet{Constants.ExeSuffix}"), Path.Combine(_globalDir, $"dotnet{Constants.ExeSuffix}"), true);

            // Restore and build SharedFxLookupPortableApp from exe dir
            PreviouslyBuiltAndRestoredPortableTestProjectFixture = new TestProjectFixture("SharedFxLookupPortableApp", RepoDirectories)
                .EnsureRestored(RepoDirectories.CorehostPackages)
                .BuildProject();
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture;

            // The actual framework version can be obtained from the built fixture. We'll use it to
            // locate the builtSharedFxDir from which we can get the files contained in the version folder
            string greatestVersionSharedFxPath = fixture.BuiltDotnet.GreatestVersionSharedFxPath;
            _sharedFxVersion = (new DirectoryInfo(greatestVersionSharedFxPath)).Name;
            _builtSharedFxDir = Path.Combine(_builtDotnet, "shared", "Microsoft.NETCore.App", _sharedFxVersion);
            _builtSharedUberFxDir = Path.Combine(_builtDotnet, "shared", "Microsoft.UberFramework", _sharedFxVersion);
            SharedFramework.CreateUberFrameworkArtifacts(_builtSharedFxDir, _builtSharedUberFxDir, SystemCollectionsImmutableAssemblyVersion, SystemCollectionsImmutableFileVersion);

            _hostPolicyDllName = Path.GetFileName(fixture.TestProject.HostPolicyDll);

            // Trace messages used to identify from which folder the framework was picked
            _cwdSelectedMessage = $"The expected {_hostPolicyDllName} directory is [{_cwdSharedFxBaseDir}";
            _userSelectedMessage = $"The expected {_hostPolicyDllName} directory is [{_userSharedFxBaseDir}";
            _exeSelectedMessage = $"The expected {_hostPolicyDllName} directory is [{_exeSharedFxBaseDir}";
            _globalSelectedMessage = $"The expected {_hostPolicyDllName} directory is [{_globalSharedFxBaseDir}";

            _cwdFoundUberFxMessage = $"Chose FX version [{_cwdSharedUberFxBaseDir}";
            _userFoundUberFxMessage = $"Chose FX version [{_userSharedUberFxBaseDir}";
            _exeFoundUberFxMessage = $"Chose FX version [{_exeSharedUberFxBaseDir}";
            _globalFoundUberFxMessage = $"Chose FX version [{_globalSharedUberFxBaseDir}";
        }

        public void Dispose()
        {
            PreviouslyBuiltAndRestoredPortableTestProjectFixture.Dispose();

            if (!TestProject.PreserveTestRuns())
            {
                Directory.Delete(_multilevelDir, true);
            }
        }

        [Fact]
        public void SharedFxLookup_Must_Verify_Folders_in_the_Correct_Order()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.0.0");

            // Add version in the exe dir
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0");

            // Version: 9999.0.0
            // User: empty
            // Exe: 9999.0.0
            // Expected: 9999.0.0 from exe dir
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .WithUserProfile(_userDir)
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(_exeSelectedMessage);

            // Add a dummy version in the user dir
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _userSharedFxBaseDir, "9999.0.0");

            // Version: 9999.0.0
            // User: 9999.0.0 --> should not be picked
            // Exe: 9999.0.0
            // Expected: 9999.0.0 from user dir
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .WithUserProfile(_userDir)
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(_exeSelectedMessage);

            // Add a dummy version in the cwd
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _cwdSharedFxBaseDir, "9999.0.0");

            // Version: 9999.0.0
            // CWD: 9999.0.0   --> should not be picked
            // User: 9999.0.0
            // Exe: 9999.0.0
            // Expected: 9999.0.0 from user Exe
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .WithUserProfile(_userDir)
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(_exeSelectedMessage);

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .WithUserProfile(_userDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0");
        }

        [Fact]
        public void SharedFxLookup_Must_Not_Roll_Forward_If_Framework_Version_Is_Specified_Through_Argument()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Add some dummy versions
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0", "9999.0.2", "9999.0.0-dummy2", "9999.0.3", "9999.0.0-dummy3");

            // Version: 9999.0.0 (through --fx-version arg)
            // Exe: 9999.0.2, 9999.0.0-dummy2, 9999.0.0, 9999.0.3, 9999.0.0-dummy3
            // global: empty
            // Expected: 9999.0.0 from exe dir
            dotnet.Exec("--fx-version", "9999.0.0", appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.0.0"));

            // Version: 9999.0.0-dummy1 (through --fx-version arg)
            // Exe: 9999.0.2, 9999.0.0-dummy2,9999.0.0, 9999.0.3, 9999.0.0-dummy3
            // global: empty
            // Expected: no compatible version
            dotnet.Exec("--fx-version", "9999.0.0-dummy1", appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0-dummy2")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.2")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.3")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0-dummy3");
        }

        [Fact]
        public void Roll_Forward_On_No_Candidate_Fx_Must_Happen_If_Compatible_Patch_Version_Is_Not_Available()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.0.0");

            // Add some dummy versions in the exe
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "10000.1.1", "10000.1.3");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' enabled with value 2 (major+minor) through env var
            // exe: 10000.1.1, 10000.1.3
            // Expected: 10000.1.3 from exe
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "2")
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "10000.1.3"));

            // Add a dummy version in the exe dir 
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.1.1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' enabled with value 2 (major+minor) through env var
            // exe: 9999.1.1, 10000.1.1, 10000.1.3
            // Expected: 9999.1.1 from exe
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "2")
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.1"));

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.1.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 10000.1.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 10000.1.3");
        }

        [Fact]
        public void Roll_Forward_On_No_Candidate_Fx_Minor_And_Disabled()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.0.0");

            // Add some dummy versions in the exe
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "10000.1.1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 10000.1.1
            // Expected: fail with no framework
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Add a dummy version in the exe dir 
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.1.1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 9999.1.1, 10000.1.1
            // Expected: 9999.1.1 from exe
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.1"));

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' disabled through env var
            // exe: 9999.1.1, 10000.1.1
            // Expected: fail with no framework
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "0")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.1.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 10000.1.1");
        }

        [Fact]
        public void Roll_Forward_On_No_Candidate_Fx_Production_To_Preview()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.0.0");

            // Add preview version in the exe
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.1.1-dummy1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 9999.1.1-dummy1
            // Expected: 9999.1.1-dummy1 since there is no production version
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.1-dummy1"));

            // Add a production version with higher value
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.2.1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 9999.1.1-dummy1, 9999.2.1
            // Expected: 9999.2.1 since we favor production over preview
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.2.1"));

            // Add a preview version with same major.minor as production
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.2.1-dummy1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 9999.1.1-dummy1, 9999.2.1, 9999.2.1-dummy1
            // Expected: 9999.2.1 since we favor production over preview
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.2.1"));

            // Add a preview version with same major.minor as production but higher patch version
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.2.2-dummy1");

            // Version: 9999.0.0
            // 'Roll forward on no candidate fx' default value of 1 (minor)
            // exe: 9999.1.1-dummy1, 9999.2.1, 9999.2.1-dummy1, 9999.2.2-dummy1
            // Expected: 9999.2.1 since we favor production over preview
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.2.1"));

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.1.1-dummy1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.2.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.2.1-dummy1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.2.2-dummy1");
        }

        [Fact]
        public void Roll_Forward_On_No_Candidate_Fx_Preview_To_Production()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.0.0-dummy1
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.0.0-dummy1");

            // Add dummy versions in the exe
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0", "9999.0.1-dummy1");

            // Version: 9999.0.0-dummy1
            // exe: 9999.0.0, 9999.0.1-dummy1
            // Expected: fail since we don't roll forward unless match on major.minor.patch and never roll forward to production
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Add preview versions in the exe with name major.minor.patch
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0-dummy2", "9999.0.0-dummy3");

            // Version: 9999.0.0-dummy1
            // exe: 9999.0.0-dummy2, 9999.0.0-dummy3, 9999.0.0, 9999.0.1-dummy1
            // Expected: 9999.0.0-dummy2
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.0.0-dummy2"));

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("9999.0.0-dummy2")
                .And
                .HaveStdOutContaining("9999.0.0-dummy3")
                .And
                .HaveStdOutContaining("9999.0.0")
                .And
                .HaveStdOutContaining("9999.0.1-dummy1");
        }

        [Fact]
        public void Roll_Forward_On_No_Candidate_Fx_Fails_If_No_Higher_Version_Is_Available()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 9999.1.1
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "9999.1.1");

            // Add some dummy versions in the exe
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9998.0.1", "9998.1.0", "9999.0.0", "9999.0.1", "9999.1.0");

            // Version: 9999.1.1
            // exe: 9998.0.1, 9998.1.0, 9999.0.0, 9999.0.1, 9999.1.0
            // Expected: no compatible version
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9998.0.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9998.1.0")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.1")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.1.0");
        }

        [Fact]
        public void Multiple_SharedFxLookup_Independent_Roll_Forward()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", null, useUberFramework: true);

            // Add versions in the exe folders
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.0.0", null, "7777.0.0");

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            // Exe: NetCoreApp 9999.0.0
            //      UberFramework 7777.0.0
            // Expected: 9999.0.0
            //           7777.0.0
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.0.0"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.0"));

            // Add a newer version to verify roll-forward
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.1");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.0.0", null, "7777.0.1");

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            // Exe: NetCoreApp 9999.0.0, 9999.0.1
            //      UberFramework 7777.0.0, 7777.0.1
            // Expected: 9999.0.1
            //           7777.0.1
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.0.1"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.1"));

            // Verify we have the expected runtime versions
            dotnet.Exec("--list-runtimes")
                .WorkingDirectory(_currentWorkingDir)
                .WithUserProfile(_userDir)
                .CaptureStdOut()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.0")
                .And
                .HaveStdOutContaining("Microsoft.NETCore.App 9999.0.1")
                .And
                .HaveStdOutContaining("Microsoft.UberFramework 7777.0.0")
                .And
                .HaveStdOutContaining("Microsoft.UberFramework 7777.0.1");
        }

        [Fact]
        public void Multiple_SharedFxLookup_Propagated_Global_RuntimeConfig_Values()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", null, useUberFramework: true);

            // Add versions in the exe folders
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.1.0");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.0.0", "UberValue", "7777.0.0");

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            // 'Roll forward on no candidate fx' disabled through env var
            // Exe: NetCoreApp 9999.1.0
            //      UberFramework 7777.0.0
            // Expected: no compatible version
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "0")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");

            // Enable rollForwardOnNoCandidateFx on app's config, which will be used as the default for Uber's config
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", rollFwdOnNoCandidateFx: 1, testConfigPropertyValue: null, useUberFramework: true);

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            //          'Roll forward on no candidate fx' enabled through config
            // Exe: NetCoreApp 9999.1.0
            //      UberFramework 7777.0.0
            // Expected: 9999.1.0
            //           7777.0.0
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "0")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.0"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.0"))
                .And
                .HaveStdErrContaining("Property TestProperty = UberValue");

            // Change the app's TestProperty value which should override the uber's config value
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", rollFwdOnNoCandidateFx: 1, testConfigPropertyValue: "AppValue", useUberFramework: true);

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            //          'Roll forward on no candidate fx' enabled through config
            // Exe: NetCoreApp 9999.1.0
            //      UberFramework 7777.0.0
            // Expected: 9999.1.0
            //           7777.0.0
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "0")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.0"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.0"))
                .And
                .HaveStdErrContaining("Property TestProperty = AppValue");
        }

        [Fact]
        public void Multiple_SharedFxLookup_Propagated_Additional_Framework_RuntimeConfig_Values()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");

            var additionalfxs = new JArray();
            additionalfxs.Add(GetAdditionalFramework("Microsoft.NETCore.App", "9999.1.0", applyPatches: false, rollForwardOnNoCandidateFx: 0));
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", null, useUberFramework: true, additionalFrameworks: additionalfxs);

            // Add versions in the exe folders
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.1.0");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.5.5", "UberValue", "7777.0.0");

            // Version: NetCoreApp 9999.5.5 (in framework section)
            //          NetCoreApp 9999.1.0 (in app's additionalFrameworks section)
            //          UberFramework 7777.0.0
            // Exe: NetCoreApp 9999.1.0
            //      UberFramework 7777.0.0
            // Expected: 9999.1.0
            //           7777.0.0
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.0"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.0"));

            // Change the additionalFrameworks to allow roll forward, overriding Uber's global section and ignoring Uber's framework section
            additionalfxs.Clear();
            additionalfxs.Add(GetAdditionalFramework("Microsoft.NETCore.App", "9999.0.0", applyPatches: false, rollForwardOnNoCandidateFx: 1));
            additionalfxs.Add(GetAdditionalFramework("UberFx", "7777.0.0", applyPatches: false, rollForwardOnNoCandidateFx: 0));
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", rollFwdOnNoCandidateFx: 0, useUberFramework: true, additionalFrameworks: additionalfxs);

            // Version: NetCoreApp 9999.5.5 (in framework section)
            //          NetCoreApp 9999.0.0 (in app's additionalFrameworks section)
            //          UberFramework 7777.0.0
            //          UberFramework 7777.0.0 (in app's additionalFrameworks section)
            // 'Roll forward on no candidate fx' disabled through env var
            // 'Roll forward on no candidate fx' disabled through Uber's global runtimeconfig
            // 'Roll forward on no candidate fx' enabled for NETCore.App enabled through additionalFrameworks section
            // Exe: NetCoreApp 9999.1.0
            //      UberFramework 7777.0.0
            // Expected: 9999.1.0
            //           7777.0.0
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "0")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining(Path.Combine(_exeSelectedMessage, "9999.1.0"))
                .And
                .HaveStdErrContaining(Path.Combine(_exeFoundUberFxMessage, "7777.0.0"));

            // Same as previous except use of '--roll-forward-on-no-candidate-fx'
            // Expected: Fail since '--roll-forward-on-no-candidate-fx' should apply to all layers
            dotnet.Exec(
                    "exec",
                    "--roll-forward-on-no-candidate-fx", "0",
                    appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .EnvironmentVariable("DOTNET_ROLL_FORWARD_ON_NO_CANDIDATE_FX", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute(fExpectedToFail: true)
                .Should()
                .Fail()
                .And
                .HaveStdErrContaining("It was not possible to find any compatible framework version");
        }

        [Fact]
        public void SharedFx_Wins_Against_App_On_RollForward_And_Version_Tie()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 7777.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", null, useUberFramework: true);

            // Add versions in the exe folder
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.0.0", null, "7777.1.0");

            // Copy NetCoreApp's copy of the assembly to the app location
            string netcoreAssembly = Path.Combine(_exeSharedFxBaseDir, "9999.0.0", "System.Collections.Immutable.dll");
            string appAssembly = Path.Combine(fixture.TestProject.OutputDirectory, "System.Collections.Immutable.dll");
            File.Copy(netcoreAssembly, appAssembly);

            // Modify the app's deps.json to add System.Collections.Immmutable
            string appDepsJson = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.deps.json");
            JObject versionInfo = new JObject();
            versionInfo.Add(new JProperty("assemblyVersion", SystemCollectionsImmutableAssemblyVersion));
            versionInfo.Add(new JProperty("fileVersion", SystemCollectionsImmutableFileVersion));
            SharedFramework.AddReferenceToDepsJson(appDepsJson, "SharedFxLookupPortableApp/1.0.0", "System.Collections.Immutable", "1.0.0", versionInfo);

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            // Exe: NetCoreApp 9999.0.0
            //      UberFramework 7777.1.0
            // Expected: 9999.0.0
            //           7777.1.0
            // Expected: the framework's version of System.Collections.Immutable is used
            string uberAssembly = Path.Combine(_exeSharedUberFxBaseDir, "7777.1.0", "System.Collections.Immutable.dll");
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining($"Replacing deps entry [{appAssembly}, AssemblyVersion:{SystemCollectionsImmutableAssemblyVersion}, FileVersion:{SystemCollectionsImmutableFileVersion}] with [{uberAssembly}, AssemblyVersion:{SystemCollectionsImmutableAssemblyVersion}, FileVersion:{SystemCollectionsImmutableFileVersion}]")
                .And
                // Verify final selection in TRUSTED_PLATFORM_ASSEMBLIES
                .HaveStdErrContaining($"{uberAssembly}{Path.PathSeparator}")
                .And
                .NotHaveStdErrContaining($"{netcoreAssembly}{Path.PathSeparator}")
                .And
                .NotHaveStdErrContaining($"{appAssembly}{Path.PathSeparator}");
        }

        [Fact]
        public void SharedFx_With_Higher_Version_Wins_Against_App_On_NoRollForward()
        {
            var fixture = PreviouslyBuiltAndRestoredPortableTestProjectFixture
                .Copy();

            var dotnet = fixture.BuiltDotnet;
            var appDll = fixture.TestProject.AppDll;

            // Set desired version = 7777.0.0
            string runtimeConfig = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.runtimeconfig.json");
            SharedFramework.SetRuntimeConfigJson(runtimeConfig, "7777.0.0", null, useUberFramework: true);

            // Add versions in the exe folder
            SharedFramework.AddAvailableSharedFxVersions(_builtSharedFxDir, _exeSharedFxBaseDir, "9999.0.0");
            SharedFramework.AddAvailableSharedUberFxVersions(_builtSharedUberFxDir, _exeSharedUberFxBaseDir, "9999.0.0", null, "7777.0.0");

            // Copy NetCoreApp's copy of the assembly to the app location
            string netcoreAssembly = Path.Combine(_exeSharedFxBaseDir, "9999.0.0", "System.Collections.Immutable.dll");
            string appAssembly = Path.Combine(fixture.TestProject.OutputDirectory, "System.Collections.Immutable.dll");
            File.Copy(netcoreAssembly, appAssembly);

            // Modify the app's deps.json to add System.Collections.Immmutable
            string appDepsJson = Path.Combine(fixture.TestProject.OutputDirectory, "SharedFxLookupPortableApp.deps.json");
            // Use lower numbers for the app
            JObject versionInfo = new JObject();
            versionInfo.Add(new JProperty("assemblyVersion", "0.0.0.1"));
            versionInfo.Add(new JProperty("fileVersion", "0.0.0.2"));
            SharedFramework.AddReferenceToDepsJson(appDepsJson, "SharedFxLookupPortableApp/1.0.0", "System.Collections.Immutable", "1.0.0", versionInfo);

            // Version: NetCoreApp 9999.0.0
            //          UberFramework 7777.0.0
            // Exe: NetCoreApp 9999.0.0
            //      UberFramework 7777.0.0
            // Expected: 9999.0.0
            //           7777.0.0
            // Expected: the framework's version of System.Collections.Immutable is used
            string uberAssembly = Path.Combine(_exeSharedUberFxBaseDir, "7777.0.0", "System.Collections.Immutable.dll");
            dotnet.Exec(appDll)
                .WorkingDirectory(_currentWorkingDir)
                .EnvironmentVariable("COREHOST_TRACE", "1")
                .CaptureStdOut()
                .CaptureStdErr()
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdErrContaining($"Adding tpa entry: {uberAssembly}, AssemblyVersion: {SystemCollectionsImmutableAssemblyVersion}, FileVersion: {SystemCollectionsImmutableFileVersion}")
                .And
                // Verify final selection in TRUSTED_PLATFORM_ASSEMBLIES
                .HaveStdErrContaining($"{uberAssembly}{Path.PathSeparator}")
                .And
                .NotHaveStdErrContaining($"{netcoreAssembly}{Path.PathSeparator}")
                .And
                .NotHaveStdErrContaining($"{appAssembly}{Path.PathSeparator}");
        }

        static private JObject GetAdditionalFramework(string fxName, string fxVersion, bool? applyPatches, int? rollForwardOnNoCandidateFx)
        {
            var jobject = new JObject(new JProperty("name", fxName));

            if (fxVersion != null)
            {
                jobject.Add(new JProperty("version", fxVersion));
            }

            if (applyPatches.HasValue)
            {
                jobject.Add(new JProperty("applyPatches", applyPatches.Value));
            }

            if (rollForwardOnNoCandidateFx.HasValue)
            {
                jobject.Add(new JProperty("rollForwardOnNoCandidateFx", rollForwardOnNoCandidateFx));
            }

            return jobject;
        }

        static private string CreateAStore(TestProjectFixture testProjectFixture)
        {
            var storeoutputDirectory = Path.Combine(testProjectFixture.TestProject.ProjectDirectory, "store");
            if (!Directory.Exists(storeoutputDirectory))
            {
                Directory.CreateDirectory(storeoutputDirectory);
            }

            testProjectFixture.StoreProject(outputDirectory: storeoutputDirectory);

            return storeoutputDirectory;
        }
    }
}

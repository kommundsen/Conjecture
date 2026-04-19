// Copyright (c) 2026 Kim Ommundsen. Licensed under the MPL-2.0.
// See LICENSE.txt in the project root or https://mozilla.org/MPL/2.0/

using System;
using System.Threading.Tasks;

using Microsoft.Testing.Platform.Builder;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Conjecture.TestingPlatform.Tests;

public class ConjectureTestingPlatformExtensionsTests
{
    // ---- fakes ---------------------------------------------------------------

    private sealed class FakeCommandLineManager : ICommandLineManager
    {
        public int AddProviderCallCount { get; private set; }

        public void AddProvider(Func<ICommandLineOptionsProvider> providerFactory)
        {
            AddProviderCallCount++;
        }

        public void AddProvider(Func<IServiceProvider, ICommandLineOptionsProvider> providerFactory)
        {
            AddProviderCallCount++;
        }
    }

    private sealed class FakeTestApplicationBuilder : ITestApplicationBuilder
    {
        private readonly FakeCommandLineManager commandLineManager = new();

        public int RegisterTestFrameworkCallCount { get; private set; }

        public ICommandLineManager CommandLine => commandLineManager;

        public int CommandLineAddProviderCallCount => commandLineManager.AddProviderCallCount;

        public ITestHostManager TestHost => throw new NotSupportedException();

        public ITestHostControllersManager TestHostControllers => throw new NotSupportedException();

#pragma warning disable TPEXP
        public IConfigurationManager Configuration => throw new NotSupportedException();

        public ILoggingManager Logging => throw new NotSupportedException();
#pragma warning restore TPEXP

        public ITestApplicationBuilder RegisterTestFramework(
            Func<IServiceProvider, ITestFrameworkCapabilities> capabilitiesFactory,
            Func<ITestFrameworkCapabilities, IServiceProvider, ITestFramework> frameworkFactory)
        {
            RegisterTestFrameworkCallCount++;
            return this;
        }

        public Task<ITestApplication> BuildAsync()
        {
            throw new NotSupportedException();
        }
    }

    // ---- tests ---------------------------------------------------------------

    [Fact]
    public void RegisterConjectureFramework_ReturnsSameBuilderInstance()
    {
        FakeTestApplicationBuilder builder = new();

        ITestApplicationBuilder returned = builder.RegisterConjectureFramework();

        Assert.Same(builder, returned);
    }

    [Fact]
    public void RegisterConjectureFramework_CallsAddProviderOnCommandLine()
    {
        FakeTestApplicationBuilder builder = new();

        builder.RegisterConjectureFramework();

        Assert.Equal(1, builder.CommandLineAddProviderCallCount);
    }

    [Fact]
    public void RegisterConjectureFramework_CallsRegisterTestFramework()
    {
        FakeTestApplicationBuilder builder = new();

        builder.RegisterConjectureFramework();

        Assert.Equal(1, builder.RegisterTestFrameworkCallCount);
    }
}
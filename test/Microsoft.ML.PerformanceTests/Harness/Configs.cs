﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using Microsoft.ML.PerformanceTests.Harness;

namespace Microsoft.ML.PerformanceTests
{
    public class RecommendedConfig : ManualConfig
    {
        public RecommendedConfig()
        {
            Add(DefaultConfig.Instance.WithOption(ConfigOptions.Default, true).WithBuildTimeout(TimeSpan.FromMinutes(5))); // this config contains all of the basic settings (exporters, columns etc)

            AddJob(GetJobDefinition()// job defines how many times given benchmark should be executed
                .WithCustomBuildConfiguration(GetBuildConfigurationName())
                .WithToolchain(CreateToolchain())); // toolchain is responsible for generating, building and running dedicated executable per benchmark

            AddColumn(new ExtraMetricColumn()); // an extra column that can display additional metric reported by the benchmarks
        }

        protected virtual Job GetJobDefinition()
            => Job.Default
                .WithWarmupCount(1) // ML.NET benchmarks are typically CPU-heavy benchmarks, 1 warmup is usually enough
                .WithMaxIterationCount(20)
                .AsDefault(); // this way we tell BDN that it's a default config which can be overwritten

        /// <summary>
        /// we need our own toolchain because MSBuild by default does not copy recursive native dependencies to the output
        /// </summary>
        private IToolchain CreateToolchain()
        {
#if NETFRAMEWORK
            var tfm = "net48";
            var csProj = CsProjClassicNetToolchain.From(tfm);
#else
            var frameworkName = new FrameworkName(AppContext.TargetFrameworkName);
            var frameworkVersion = frameworkName.Version.ToString(2);
            var settings = new NetCoreAppSettings($"net{frameworkVersion}", null, $".NET {frameworkVersion}");

            var tfm = settings.TargetFrameworkMoniker;
            var csProj = CsProjCoreToolchain.From(settings);
#endif
            return new Toolchain(
                tfm,
                new ProjectGenerator(tfm), // custom generator that copies native dependencies
                csProj.Builder,
                csProj.Executor);
        }

        protected static string GetBuildConfigurationName()
        {
#if NETFRAMEWORK
            return "Release-netfx";
#else
            return "Release";
#endif
        }
    }

    public class TrainConfig : RecommendedConfig
    {
        protected override Job GetJobDefinition()
            => Job.Dry // the "Dry" job runs the benchmark exactly once, without any warmup to mimic real-world scenario
                  .WithCustomBuildConfiguration(GetBuildConfigurationName())
                  .WithLaunchCount(3); // BDN will run 3 dedicated processes, sequentially
    }
}
